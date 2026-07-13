---
description: "tests/UnitTests/CopyTrading/FakeTradingSession.cs = IOpenApiTradingSession en mémoire que tous les tests unitaires de copie commerciale exécutent. Tâche : imiter le serveur API Open cTrader réel…"
---

# FakeTradingSession — contrat de fidélité API Open cTrader

`tests/UnitTests/CopyTrading/FakeTradingSession.cs` = `IOpenApiTradingSession` en mémoire que tous les tests unitaires de copie commerciale exécutent. Tâche : imiter le **serveur API Open cTrader réel** assez proche pour que les tests unitaires couvrent le comportement que seul le tier en direct avait l'habitude de détecter. Ce doc = contrat de fidélité : quels faux modèles, à quel point fidèlement, et la règle gardant honnête.

> **Règle contraignante (CLAUDE.md) :** le faux reste fidèle cTrader. **Étendez-le, ne l'affaiblissez jamais** pour passer un test. Chaque nouveau comportement réel sur lequel vous comptez est modélisé ici, épinglé par test de fidélité.

## Matrice de fidélité (F1–F13)

Suit le plan `plans/copy-trading-overhaul.md` §7.6. Légende : ✅ modélisé · ◑ partiel (opt-in / extension) · ⬜ pas encore modélisé.

| # | Comportement API Open réel | Statut faux | Comment c'est modélisé |
|---|------------------------|-----------|-------------------|
| F1 | L'ordre marché peut **partial-fill** | ◑ | `PartialFillFractionForCtid[ctid] = f` remplit uniquement `f×volume` ; la réconciliation affiche ensuite Phase‑1 true‑up (G5) fermetures. La paire accept→fill à venir. |
| F2 | Volume normalisé à **step**, rejeté en dessous de **min** / au-dessus de **max** | ✅ | `VolumeBoundsForCtid[ctid] = (Step, Min, Max)` arrondit vers le bas à l'étape, lève `CtraderRejectException(VolumeTooLow/High)`. |
| F3 | **Invalid SL/TP** rejeté (côté + chiffres) | ⬜ | Planifié Phase 0a/1 (appaires avec M6 normalisation de précision SL/TP). |
| F4 | Les prix **integer-scalés par chiffres** ; `pipPosition` | ◑ | `SymbolDetails` porte maintenant `Digits` (et `MaxVolume`), peuplé à partir du symbole réel ; `PipPosition` conduit la tolérance de plage de marché, `Digits` conduit la normalisation de précision SL/TP (M6). La mise à l'échelle du prix entier complet toujours en attente. |
| F5 | **Market-range** remplit uniquement si le spot est dans `base ± slippage`, sinon rejette | ✅ | `IsMarketRangeRejected` compare le spot en direct (`SetSpot`) à `baseSlippagePrice ± slippageInPoints`. Le drapeau hérité `RejectMarketRangeForCtid` force toujours le rejet. |
| F6 | **Pending trigger→fill** double événement (Order porte `positionId` + Position OPEN) | ◑ | `PushOpen(..., orderId:)` reproduit événement de pending rempli ; la déduplication FX‑Blue/cMAM couverte en `CopyEngineHostTests.Filled_pending_does_not_double_open`. |
| F7 | **Closes pilotés par le serveur** (SL/TP hit, stop-out) | ⬜ | Aujourd'hui ferme test-poussé (`PushClose`) ; fermetures driven-par-prix SL/TP-hit + stop-out planifiées. |
| F8 | **Par-compte** tableaux de symboles / détails | ◑ | Noms/ids de symboles par-faux ; tableaux divergents par-compte (cross-broker) en attente. |
| F9 | **État de compte** complet (balance, équité, marge, marge libre) | ◑ | `Balance` + `LoadPositionValuationsAsync` (entrée/swap/commission via `SetPositionValuation`) + `SetSpot` alimente l'équité réelle dans le dimensionnement proportionnel-équité (G2, unit-testé en `CopyEquitySizingTests`). La marge utilisée n'est pas exposée par l'API de réconciliation, donc la marge libre est rapportée comme équité. |
| F10 | Les événements portent les **horodatages du serveur** | ✅ | `ExecutionEvent.ServerTimestamp` (unix ms) — la session réelle lit de `ExecutionTimestamp` de la transaction ; `PushOpen`/`PushPending` acceptent `serverTimestamp:` donc le test driven-par-`FakeTimeProvider` conduit la véritable latence de copie (G1). |
| F11 | **Mode de trading / horaire** (désactivé / close-only / fermé) | ⬜ | Planifié Phase 2b. |
| F12 | **Taxonomie d'erreur typée** (codes `ProtoOAErrorRes`) | ✅ | `RejectReasonForCtid[ctid] = CtraderRejectReason.X` lève une seule tentative `CtraderRejectException(reason)` (NotEnoughMoney, MarketClosed, PositionNotFound, …). |
| F13 | **Invalidation de token** — token obsolète → erreur d'auth | ✅ | `InvalidateToken(ctid)` marque le token attaché obsolète ; les appels de trading lèvent **vrai** `OpenApiException` avec `OpenApiErrorKind.TokenInvalid` (code `CH_ACCESS_TOKEN_INVALID`), exactement comme le serveur en direct, jusqu'à ce que `SwapAccessTokenAsync` installe le token frais. Alimente le test M1 de robustesse de token. |

Les tests de fidélité vivent en `tests/UnitTests/CopyTrading/FakeTradingSessionFidelityTests.cs`.

## Opt-in, les défauts conservent le comportement hérité

Chaque bouton de fidélité est **désactivé par défaut** de sorte que le faux reste simple pour le comportement always-fill pour les tests qui ne se soucient pas. Le test se fait opter par compte :

```csharp
session.VolumeBoundsForCtid[slave]        = (Step: 10, Min: 10, Max: 1000); // F2
session.PartialFillFractionForCtid[slave] = 0.6;                            // F1 / G5
session.RejectReasonForCtid[slave]        = CtraderRejectReason.NotEnoughMoney; // F12 (une seule tentative)
session.InvalidateToken(slave);                                             // F13
```

## Caractérisation + conformité (planifiées, gardent le faux ≡ réel)

Deux mécanismes gardent le faux honnête contre le serveur réel en mouvement (suivi, atterrissage entre Phase 0a) :

1. **Caractérisation en direct** (`LiveApiCharacterization`, comptes démo, gates secrets, `Inconclusive` sur le marché fermé) : conduire l'API Open réel, enregistrer la vérité exacte du câble (séquences d'événements, mise à l'échelle, codes de rejet) dans les accessoires d'or vérifiés au projet de test. Pas de secrets dans les accessoires — seulement les formes observées.
2. **Harnais de conformité** : exécutez la *même* suite de scénarios deux fois — une fois contre `FakeTradingSession`, une fois contre la session en direct (quand les secrets présents) — affirmez des résultats observables identiques. Les changements du serveur réel → la jambe en direct échoue → mettez à jour le faux. Cela rend « les tests unitaires couvrent tout » digne de confiance.

Les identifiants en direct : `secrets/dev-credentials.local.json` (ou fichiers divisés hérités) — voir `docs/testing/dev-credentials.md`.
