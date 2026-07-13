---
title: Failure-path coverage map
description: Każdy failure scenario mandate requires, mapowany do test(s) że actually exercise
to — więc gap jest visible, nie assumed.
---

# Failure-path coverage map

Mandate test jest explicit: **failure paths count** — zmiana że może break na dropped
connection, rejected order, desync, token rotation, albo dead node wysyła z test dla że,
w tym samym commit. Ta strona mapuje każdy required scenario do test(s) że exercise to,
więc real gap jest *visible* raniej niż assumed. Gdy ty add failure path, add row tutaj.

## Required scenarios → tests

| Scenario | Tier(s) | Tests |
|---|---|---|
| **Connection drop → reconnect** | unit · stress · E2E | `OpenApiConnectionTests.Dropped_connection_reconnects_and_raises_reconnected`; `FakeTradingSession.Disconnect/ReconnectAsync` i `SyncTradingSession` (DST); `MiscUiTests` reconnect-modal states |
| **Order rejection** | unit · stress | `CopyTransparencyTests.A_rejected_open_emits_a_Failed_execution_fact_with_the_reason`; `CopyCircuitBreakerTests`; DST `CopyDstWorld.FailOrders` / `RejectMarketRange` |
| **Desync / resync** | unit · stress | `CopyPartialFillTests.Resync_tops_up_a_broker_partial_fill…`; `CopyEngineHostTests.Reconnect_resync_closes_orphaned_destination_positions` (+ `…tolerates_a_position_not_found…`); `CopyAdvancedScenariosTests.Reconnect_resync_opens_missing_copies_and_closes_orphans_after_a_desync`; `CopyChaosDstTests` |
| **Token rotation / invalidation** | unit · integration · stress | `OpenApiAuthorizationTests.MarkRefreshFailed_*` (escalation window); `FakeTradingSession.InvalidateToken`; `TokenRotationSignatureTests`, `LiveTokenBootstrapTests`, `OpenApiTokenRefreshPersistenceTests` (integration); DST `RotateTokens` |
| **Node death → lease reclaim** | unit · integration · stress | `NodeInstanceReclaimerTests` (unit + integration); `CopyRulesDomainTests.Lease_is_held_only_by_the_claiming_node_until_it_expires`; `CopyHostWatchdogTests`, `CopyNodeAffinityTests`, `PropFirmTrackingLeaseTests` (integration); `CopyLeaseReclaimStressTests` |
| **AI provider error (4xx/5xx/timeout/malformed)** | unit · integration | `AnthropicAiClientTests.Fails_gracefully_on_error_status` / `…on_malformed_json` / `…on_empty_content`; `AiHttpResilienceTests`, `AiRecommendDisabledTests` (integration) |
| **AI fully disabled (brak key)** | unit · integration · E2E | `AiFeatureServiceTests`; `AiRecommendDisabledTests`; `AiPagesTests` |
| **Database transient failure / migration lock** | integration | `DatabaseResilienceTests`; `MigrationLockTests` |
| **Node HTTP agent failure / retry** | integration | `NodeAgentHttpResilienceTests` |
| **Container self-exit reconciliation** | unit | `BacktestCompletionPollerTests`; `RunCompletionPoller` coverage w `ContainerCommandHelpersTests` |
| **Prop-firm breach** | unit · integration | `PropFirmChallengeRulesTests`; `PropFirmAlertNotifierTests`; `PropFirmChallengePersistenceTests` |
| **Invalid input / auth reject (UI + branding)** | unit · integration · E2E | `LoginTests.Invalid_credentials_show_an_error`; `HexColorTests.Rejects_invalid_hex`; `BrandingOptionsValidatorTests` |

## Thin spots — verify zanim assuming covered

Te są worth explicit check (add row powyżej raz confirmed albo filled):

- **MCP tool auth rejection** — `McpKeyAuthHandler` rejects bad/absent key. Brak dedicated test było
  found; add integration test że calls MCP tool endpoint z missing/invalid key i
  asserts 401.
- **CBot build failure surfacing** — compile error musi land na instance/UI jako `Failed` z
  build output. `CBotLifecycleTests` covers happy path; confirm failure branch jest asserted.
- **Live order execution** — end-to-end copy execution przeciwko real cTrader credentials remains
  gated (potrzeby credentials + node cluster); zobacz [Live copy trading](./live-copy-trading.md).

## Jak to jest enforced

Deterministic stress suite (DST, `tests/StressTests`) replays te failures na compressed
clock i musi stay green — **nigdy nie weaken DST scenario aby make to pass; fix kod**.
[FakeTradingSession](./fake-trading-session.md) jest cTrader-faithful simulator te unit tests
drive; extend to dla new broker behavior raniej niż relaxing assertion.
