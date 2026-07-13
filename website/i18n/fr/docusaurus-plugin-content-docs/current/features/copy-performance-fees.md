---
description: "Frais de performance du money-manager sur une marque d'eau élevée, le modèle standard de copie commerciale (cTrader Copy, Darwinex, partage des bénéfices ZuluTrade) : un fournisseur charge…"
---

# Frais de performance de copie (Phase 4)

**Frais de performance** du money-manager sur une **marque d'eau élevée**, le modèle standard de copie commerciale (cTrader Copy,
Darwinex, partage des bénéfices ZuluTrade) : un fournisseur charge un pourcentage des *nouveaux* bénéfices au-dessus du
pic d'équité de chaque follower — jamais sur le solde d'ouverture, et jamais deux fois pour le sol déjà récupéré. **Opt-in** via
`App:Copy:FeesEnabled` (désactivé par défaut).

## Le modèle (marque d'eau élevée)

Par destination (compte follower), chaque règlement :

1. **Premier règlement** ensemence la marque d'eau élevée (HWM) à l'équité actuelle → pas de frais (un follower n'est
   jamais facturé sur son dépôt).
2. **Nouveau pic** (équité > HWM) : `fee = performanceFeePercent × (équité − HWM)`, puis `HWM ← équité`.
3. **Au pic ou en dessous** : pas de frais, HWM inchangé — le follower doit d'abord se rétablir au-delà de l'ancien pic, donc
   il n'est jamais facturé deux fois pour les mêmes gains.

L'arithmétique des frais est un invariant de domaine sur `CopyDestination.SettleFee(equity)` — l'agrégat en est propriétaire ; le
service de règlement ne fournit que l'équité interrogée et enregistre le montant retourné. `PerformanceFee` est un
objet de valeur plafonné à 50% de sorte qu'une mauvaise configuration ne peut pas débiter les gains entiers d'un follower.

## Comment ça s'arrange

```
CopyFeeSettlementService (BackgroundService, uniquement quand FeesEnabled)
   │  chaque App:Copy:FeeSettlementInterval
   ├─ charger les profils en cours avec une destination configurée avec des frais
   ├─ ICopyEquityReader.ReadEquityAsync(ctid)   ← OpenApiCopyEquityReader ouvre une session,
   │                                               calcule le solde + P&L flottant (PropFirmEquityCalculator)
   ├─ destination.SettleFee(equity)             ← Logique HWM sur l'agrégat
   └─ persister HWM avancé + ajouter CopyFeeAccrual (uniquement sur un nouveau pic)
```

- `ICopyEquityReader` est une abstraction Core ; l'implémentation en direct (`OpenApiCopyEquityReader`) est la seule
  pièce d'infra — donc la logique de règlement + HWM est exercée dans les tests avec un lecteur faux, pas de courtier en direct.
- `CopyFeeAccrual` est un journal append-only (HWM-avant, équité, % de frais, montant des frais, réglé à) — un journal des faits pour
  le rapport de frais et la facturation, pas un agrégat.

## Configuration & API

| Paramètre `App:Copy` | Défaut | Effet |
|--------------------|---------|--------|
| `FeesEnabled` | `false` | Exécuter le service de règlement. |
| `FeeSettlementInterval` | `1h` | À quelle fréquence l'équité est interrogée et les frais réglés. |

Par destination : `PerformanceFeePercent` (0–50) est défini sur la destination (demande d'ajout/édition de destination).

- `GET /api/copy/profiles/{id}/fees` — les accrues de frais du profil + total facturé.

## Tests

- **Unit** (`CopyPerformanceFeeTests`) — l'invariant HWM : le premier règlement ensemence + ne charge rien ; un nouveau
  pic ne charge que le gain au-dessus du pic ; au pic ou en dessous ne charge rien et le pic ne recule jamais ;
  après un tirage à la baisse seulement la récupération au-delà de l'ancien pic est facturée ; 0% ne charge jamais ; la VO rejette
  les pourcentages hors limites.
- **Integration** (`CopyFeeSettlementTests`, Postgres réel, lecteur d'équité faux) — ensemencement→10k (pas de charge, marque
  ensemencée), 12k (facture 400, marque avance), 11k (pas de charge, marque tenue) ; accrues persistée avec le bon
  propriétaire/montant.

L'hôte de copie ne touche pas aux frais (le règlement est une tâche BD séparée), donc la suite de stress DST de copie
n'est pas affectée (23/23).
