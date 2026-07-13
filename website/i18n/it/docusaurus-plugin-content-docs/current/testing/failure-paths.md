---
title: Mappa di copertura failure-path
description: Ogni scenario di failure che il mandate richiede, mappato ai test che effettivamente lo esercitano — così un gap è visibile, non assunto."
---

# Mappa di copertura failure-path

Il test mandate è esplicito: **i failure paths contano** — una modifica che può rompersi su una connessione caduta,
un ordine rifiutato, una desync, una rotazione token, o un nodo morto spedisce con un test per quello,
nella stessa commit. Questa pagina mappa ogni scenario richiesto ai test che lo esercitano, così un vero
gap è *visibile* piuttosto che assunto. Quando aggiungi un failure path, aggiungi una riga qui.

## Scenari richiesti → test

| Scenario | Tier(s) | Test |
|---|---|---|
| **Connection drop → reconnect** | unit · stress · E2E | `OpenApiConnectionTests.Dropped_connection_reconnects_and_raises_reconnected`; `FakeTradingSession.Disconnect/ReconnectAsync` and `SyncTradingSession` (DST); `MiscUiTests` reconnect-modal states |
| **Order rejection** | unit · stress | `CopyTransparencyTests.A_rejected_open_emits_a_Failed_execution_fact_with_the_reason`; `CopyCircuitBreakerTests`; DST `CopyDstWorld.FailOrders` / `RejectMarketRange` |
| **Desync / resync** | unit · stress | `CopyPartialFillTests.Resync_tops_up_a_broker_partial_fill…`; `CopyEngineHostTests.Reconnect_resync_closes_orphaned_destination_positions` (+ `…tolerates_a_position_not_found…`); `CopyAdvancedScenariosTests.Reconnect_resync_opens_missing_copies_and_closes_orphans_after_a_desync`; `CopyChaosDstTests` |
| **Token rotation / invalidation** | unit · integration · stress | `OpenApiAuthorizationTests.MarkRefreshFailed_*` (escalation window); `FakeTradingSession.InvalidateToken`; `TokenRotationSignatureTests`, `LiveTokenBootstrapTests`, `OpenApiTokenRefreshPersistenceTests` (integration); DST `RotateTokens` |
| **Node death → lease reclaim** | unit · integration · stress | `NodeInstanceReclaimerTests` (unit + integration); `CopyRulesDomainTests.Lease_is_held_only_by_the_claiming_node_until_it_expires`; `CopyHostWatchdogTests`, `CopyNodeAffinityTests`, `PropFirmTrackingLeaseTests` (integration); `CopyLeaseReclaimStressTests` |
| **AI provider error (4xx/5xx/timeout/malformed)** | unit · integration | `AnthropicAiClientTests.Fails_gracefully_on_error_status` / `…on_malformed_json` / `…on_empty_content`; `AiHttpResilienceTests`, `AiRecommendDisabledTests` (integration) |
| **AI fully disabled (no key)** | unit · integration · E2E | `AiFeatureServiceTests`; `AiRecommendDisabledTests`; `AiPagesTests` |
| **Database transient failure / migration lock** | integration | `DatabaseResilienceTests`; `MigrationLockTests` |
| **Node HTTP agent failure / retry** | integration | `NodeAgentHttpResilienceTests` |
| **Container self-exit reconciliation** | unit | `BacktestCompletionPollerTests`; `RunCompletionPoller` coverage in `ContainerCommandHelpersTests` |
| **Prop-firm breach** | unit · integration | `PropFirmChallengeRulesTests`; `PropFirmAlertNotifierTests`; `PropFirmChallengePersistenceTests` |
| **Invalid input / auth reject (UI + branding)** | unit · integration · E2E | `LoginTests.Invalid_credentials_show_an_error`; `HexColorTests.Rejects_invalid_hex`; `BrandingOptionsValidatorTests` |

## Thin spots — verifica prima di assumere coperto

Questi meritano un check esplicito (aggiungi una riga sopra una volta confermato o riempito):

- **MCP tool auth rejection** — `McpKeyAuthHandler` rifiuta una key bad/absent. Non è stato trovato nessun test dedicato;
  aggiungi un integration test che chiama un endpoint tool MCP con una key mancante/invalida e assert 401.
- **CBot build failure surfacing** — un compile error deve atterrare sull'instance/UI come `Failed` con l'output
  del build. `CBotLifecycleTests` copre l'happy path; conferma che il branch failure è assertito.
- **Live order execution** — copy execution end-to-end contro credenziali cTrader reali rimane gated
  (necessita credenziali + un cluster nodo); vedere [Live copy trading](./live-copy-trading.md).

## Come questo è applicato

La suite stress deterministica (DST, `tests/StressTests`) riproduce questi failure su un clock compresso e deve
restare green — **mai indebolire uno scenario DST per farlo passare; fixare il codice**. Il
[FakeTradingSession](./fake-trading-session.md) è il simulatore cTrader-faithful su cui girano questi unit
test; estenderlo per nuovo broker behavior piuttosto che rilassare un'assertion.
