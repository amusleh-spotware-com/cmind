---
description: "Flux par propriétaire d'événements de copie pertinents pour la sécurité — destination actionnant le disjoncteur de rejet, violation de protection de compte ou règle prop, aplatissement de panique. Activé par…"
---

# Notifications opérationnelles de copie (Phase 2b)

Flux par propriétaire d'événements de copie pertinents pour la sécurité — destination actionnant le disjoncteur de rejet, violation de protection de compte ou règle prop, aplatissement de panique. **Activé par défaut** (`App:Copy:NotificationsEnabled`, défaut `true`) ; défini à false pour désactiver. Concept personnel dans le contexte Copy, séparé de l'agrégat `AlertRule` marché/IA.

## Comment ça marche

Même modèle hors bande hôte→puits→écoulement que le journal de transparence d'exécution :

```
CopyEngineHost ──Notify(record)──▶ ICopyNotificationSink
                                     │
             (notifications désactivées) NullCopyNotificationSink   → rejette (non-op ; hôte inchangé)
             (notifications activées)  ChannelCopyNotificationSink → canal DropOldest borné
                                     │
                                     ▼
                            CopyNotificationDrainer (BackgroundService)
                                     │  résout le propriétaire de chaque profil, par lots
                                     ▼
                            Flux CopyNotification  ◀── GET /api/copy/notifications
```

- L'hôte `Notify(...)` non-bloquant, jamais levé — ne touche jamais la BD, ne retarde jamais la copie.
- L'écoulement résout le `UserId` propriétaire de chaque notification ; la notification dont le profil a disparu (propriétaire non résolvable) est supprimée, pas orpheline.
- `CopyNotification` = flux append-only, acquittable par ligne (pas agrégat).

## Ce qui est levé

| Genre | Sévérité | Quand |
|------|----------|------|
| `DestinationTripped` | Warning | Budget de rejet G8 épuisé ; les nouvelles ouvertures en pause pour la fenêtre de refroidissement. |
| `AccountProtectionTriggered` | Critical | Plancher/plafond d'équité ZuluGuard dépassé ; ouvertures verrouillées (SellOut liquide). |
| `PropRuleBreached` | Critical | Perte quotidienne prop / tirage descendant traîné dépassé ; destination aplatie + verrouillée pour la journée. |
| `FlattenAll` | Critical | Aplatissement de panique exécuté ; chaque destination fermée + verrouillée. |
| `TokenInvalidated` | (réservé) | Le token d'une destination a été invalidé ; en attente de rotation. |

## API

- `GET /api/copy/notifications` (scoped propriétaire) — notifications récentes de l'utilisateur (200 les plus récentes) sur tous les profils, plus le compteur **non acquitté**.
- `POST /api/copy/notifications/{id}/acknowledge` — marquer un comme lu.

## Configuration (`App:Copy`)

| Paramètre | Défaut | Effet |
|---------|---------|--------|
| `NotificationsEnabled` | `true` | Émettre des notifications de sécurité + exécuter l'écoulement. `false` → puits non-op. |

## Tests

- **Unit** (`CopyNotificationTests`) — destination actionnée lève `DestinationTripped` ; aplatissement de panique lève `FlattenAll` au niveau profil. Via le puits de capture.
- **Integration** (`CopyNotificationDrainerTests`, Postgres réel) — l'écoulement résout le propriétaire + persiste ; la notification pour un profil inconnu est supprimée.
- **DST** — l'hôte émet fire-and-forget avec le puits non-op par défaut, donc la suite de stress de copie reste verte (23/23).
