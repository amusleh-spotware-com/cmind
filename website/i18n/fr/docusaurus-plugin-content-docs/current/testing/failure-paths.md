---
title: Carte de couverture des chemins de défaillance
description: Chaque scénario de défaillance que le mandat exige, mappé à le(s) test(s) qui l'exercent vraiment — afin qu'un écart soit visible, pas supposé.
---

# Carte de couverture des chemins de défaillance

Le mandat de test est explicite : **les chemins de défaillance comptent** — un changement qui peut casser sur une connexion abandonnée, une commande rejetée, une désync, une rotation de jetons ou un nœud mort est livré avec un test pour cela, dans le même commit. Cette page mappe chaque scénario requis au(x) test(s) qui l'exercent, afin qu'un écart réel soit *visible* plutôt que supposé. Quand vous ajoutez un chemin de défaillance, ajoutez une ligne ici.

## Scénarios requis → tests

| Scénario | Niveau(x) | Tests |
|---|---|---|
| **Connexion abandonnée → reconnexion** | unité · stress · E2E | `OpenApiConnectionTests.Dropped_connection_reconnects_and_raises_reconnected`; `FakeTradingSession.Disconnect/ReconnectAsync` et `SyncTradingSession` (DST); états de modal reconnexion `MiscUiTests` |
| **Rejet de commande** | unité · stress | `CopyTransparencyTests.A_rejected_open_emits_a_Failed_execution_fact_with_the_reason`; `CopyCircuitBreakerTests`; DST `CopyDstWorld.FailOrders` / `RejectMarketRange` |
| **Désync / resync** | unité · stress | `CopyPartialFillTests.Resync_tops_up_a_broker_partial_fill…`; `CopyEngineHostTests.Reconnect_resync_closes_orphaned_destination_positions` (+ `…tolerates_a_position_not_found…`); `CopyAdvancedScenariosTests.Reconnect_resync_opens_missing_copies_and_closes_orphans_after_a_desync`; `CopyChaosDstTests` |
| **Rotation / invalidation de jetons** | unité · intégration · stress | `OpenApiAuthorizationTests.MarkRefreshFailed_*` (fenêtre d'escalade); `FakeTradingSession.InvalidateToken`; `TokenRotationSignatureTests`, `LiveTokenBootstrapTests`, `OpenApiTokenRefreshPersistenceTests` (intégration); DST `RotateTokens` |
| **Mort du nœud → reprise de bail** | unité · intégration · stress | `NodeInstanceReclaimerTests` (unité + intégration); `CopyRulesDomainTests.Lease_is_held_only_by_the_claiming_node_until_it_expires`; `CopyHostWatchdogTests`, `CopyNodeAffinityTests`, `PropFirmTrackingLeaseTests` (intégration); `CopyLeaseReclaimStressTests` |
| **Erreur du fournisseur IA (4xx/5xx/timeout/malformed)** | unité · intégration | `AnthropicAiClientTests.Fails_gracefully_on_error_status` / `…on_malformed_json` / `…on_empty_content`; `AiHttpResilienceTests`, `AiRecommendDisabledTests` (intégration) |
| **IA entièrement désactivée (pas de clé)** | unité · intégration · E2E | `AiFeatureServiceTests`; `AiRecommendDisabledTests`; `AiPagesTests` |
| **Défaillance transitoire de base de données / verrouillage de migration** | intégration | `DatabaseResilienceTests`; `MigrationLockTests` |
| **Défaillance / retry de l'agent HTTP du nœud** | intégration | `NodeAgentHttpResilienceTests` |
| **Réconciliation de sortie autonome du conteneur** | unité | `BacktestCompletionPollerTests`; couverture `RunCompletionPoller` dans `ContainerCommandHelpersTests` |
| **Violation prop-firm** | unité · intégration | `PropFirmChallengeRulesTests`; `PropFirmAlertNotifierTests`; `PropFirmChallengePersistenceTests` |
| **Entrée invalide / rejet auth (UI + marque)** | unité · intégration · E2E | `LoginTests.Invalid_credentials_show_an_error`; `HexColorTests.Rejects_invalid_hex`; `BrandingOptionsValidatorTests` |

## Points faibles — vérifier avant de supposer couvert

Ceux-ci valent un contrôle explicite (ajouter une ligne ci-dessus une fois confirmé ou comblé) :

- **Rejet d'auth de l'outil MCP** — `McpKeyAuthHandler` rejette une clé mauvaise/absente. Aucun test dédié n'a été trouvé ; ajouter un test d'intégration qui appelle un point de terminaison d'outil MCP avec une clé manquante/invalide et affirme 401.
- **Défaillance de construction de cBot en surface** — une erreur de compilation doit atterrir sur l'instance/interface utilisateur en tant que `Failed` avec la sortie de la construction. `CBotLifecycleTests` couvre le chemin heureux ; confirmer que la branche d'échec est affirmée.
- **Exécution d'ordre en direct** — l'exécution complète de copie de bout en bout contre les identifiants cTrader réels reste gatée (besoins d'identifiants + grappe de nœuds) ; voir [Copie-trading en direct](./live-copy-trading.md).

## Comment ceci est appliqué

La suite de stress déterministe (DST, `tests/StressTests`) rejoue ces défaillances sur une horloge compressée et doit rester verte — **jamais affaiblir un scénario DST pour le faire passer ; corriger le code**. Le [FakeTradingSession](./fake-trading-session.md) est le simulateur fidèle cTrader que ces tests unitaires conduisent ; l'étendre pour le nouveau comportement du courtier plutôt que de détendre une affirmation.
