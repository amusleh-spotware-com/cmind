---
description: "Faits d'exécution de copie par copie — latence, glissement réalisé, remplissage vs échec — capturés à chaque tentative de copie, présentés comme rapport de transparence par profil. Désactivé par…"
---

# Transparence d'exécution de copie (Phase 3)

Faits d'exécution de copie par copie — latence, glissement réalisé, remplissage vs échec — capturés à chaque tentative de copie,
présentés comme rapport de transparence par profil. **Désactivé par défaut** ; activez avec
`App:Copy:TransparencyEnabled=true`. Lorsque désactivé, le moteur de copie est inchangé octet pour octet : l'hôte émet
vers un puits non-op, rien n'est écrit.

## Comment ça marche

```
CopyEngineHost ──Record(fact)──▶ ICopyEventSink
                                   │
             (transparence désactivée) NullCopyEventSink   → rejette (par défaut ; zéro coût chemin chaud)
             (transparence activée)  ChannelCopyEventSink → canal bornée en mémoire (DropOldest)
                                   │
                                   ▼
                          CopyExecutionDrainer (BackgroundService)
                                   │  par lots chaque intervalle d'écoulement App
                                   ▼
                          Table CopyExecution append-only  ◀── GET /api/copy/profiles/{id}/transparency
```

- **Le chemin chaud reste libre d'E/S.** L'hôte appelle `ICopyEventSink.Record(...)` — non-bloquant,
  enqueue qui ne lève jamais. N'attend jamais, ne touche jamais la BD, ne bloque jamais l'exécution des ordres.
- **Perte préférée à la contre-pression.** Canal borné (`CopyExecutionChannelCapacity`) avec
  `DropOldest` : si l'écoulement BD stalle, les rangées de transparence les *plus anciennes* sont supprimées plutôt que de retarder
  une copie. Transparence = télémétrie best-effort, pas dépendance commerciale.
- **Persistance hors bande.** `CopyExecutionDrainer` écoule le canal par lots
  (`CopyExecutionDrainBatchSize`) sur `CopyExecutionDrainInterval`, écrit les rangées `CopyExecution` via
  `DataContext` scoped. Écoulement final à l'arrêt.
- **Faits, pas commandes.** `CopyExecution` = journal append-only (comme `InstanceLog`/`AuditLog`), pas
  agrégat. Le modèle de lecture l'interroge directement (CQRS-lite), les agrégats en mémoire.

## Ce qui est enregistré

Un `CopyExecutionRecord` par tentative de copie sur une destination :

| Genre | Quand | Transporte |
|------|------|---------|
| `Opened` | ordre de copie placé | symbole, côté, volume câblé, prix maître, glissement réalisé (points), latence (ms) |
| `Failed` | l'ouverture de copie a levé/rejeté | symbole, côté, volume/prix maître, latence, raison d'échec (type d'exception) |

(`Closed`/`Skipped`/`Reconciled` existent dans l'énumération pour expansion future.)

## Le rapport

`GET /api/copy/profiles/{id}/transparency` (scoped propriétaire) retourne, sur les 500 faits les plus récents :

- **Résumé** — total, ouvert, échoué, **taux de remplissage**, **latence moyenne (ms)**, **glissement moyen (points)**.
- **Récent** — faits récents bruts (destination, position source, symbole, côté, volume, prix maître,
  glissement, latence, raison, horodatage).

## Configuration (`App:Copy`)

| Paramètre | Défaut | Effet |
|---------|---------|--------|
| `TransparencyEnabled` | `false` | Activez la capture de faits par copie + écoulement pour le nœud. |

Capacité de canal, taille de lot d'écoulement, intervalle d'écoulement = constantes `CopyDefaults`
(`CopyExecutionChannelCapacity` / `CopyExecutionDrainBatchSize` / `CopyExecutionDrainInterval`).

## Tests

- **Unit** (`CopyTransparencyTests`) — l'ouverture réussie émet un fait `Opened` avec le bon
  symbole/côté/volume/latence ; l'ouverture rejetée émet un fait `Failed` avec raison. Conduit par un puits
  de capture.
- **Integration** (`CopyExecutionDrainerTests`, Postgres réel) — l'écoulement persiste les faits tamponnés vers
  le journal `CopyExecution` ; le puits vide n'écrit rien.
- **DST** — l'hôte change fire-and-forget avec le puits non-op par défaut, donc la suite de stress déterministe de copie
  reste verte (23/23).
