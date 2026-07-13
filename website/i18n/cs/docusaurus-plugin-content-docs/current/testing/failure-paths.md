---
title: Mapa pokrytí failure paths
description: Každý failure scénář který mandate vyžaduje, mapovaný na test(y) který ho skutečně cvičí — takže mezera je viditelná, ne předpokládaná.
---

# Mapa pokrytí failure-path

Test mandate je explicitní: **failure paths count** — změna která může selhat na dropped
connection, rejected order, desync, token rotation, nebo dead node ships with a test for that,
in the same commit. Tato stránka mapuje každý required scénář na test(y) který ho cvičí, takže reálná
mezera je *viditelná* spíše než předpokládaná. Když přidáte failure path, přidejte řádek sem.

## Required scénáře → testy

| Scénář | Tier(s) | Testy |
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

## Thin spots — verify before assuming covered

Tyto stojí za explicitní kontrolu (přidejte řádek výše once confirmed or filled):

- **MCP tool auth rejection** — `McpKeyAuthHandler` rejects a bad/absent key. Žádný dedicated test found;
  add an integration test that calls an MCP tool endpoint with a missing/invalid key and
  asserts 401.
- **CBot build failure surfacing** — a compile error must land on instance/UI as `Failed` with the
  build output. `CBotLifecycleTests` covers the happy path; confirm the failure branch is asserted.
- **Live order execution** — end-to-end copy execution proti reálným cTrader credentials zůstává gated
  (potřebuje credentials + node cluster); viz [Live copy trading](./live-copy-trading.md).

## Jak je toto enforced

Deterministic stress suite (DST, `tests/StressTests`) replayuje these failures on a compressed
clock and must stay green — **nikdy nezeslabujte DST scénář pro jeho průchod; opravte kód**.
[FakeTradingSession](./fake-trading-session.md) is the cTrader-faithful simulator tyto unit testy
řídí; rozšiřte ho pro nové broker behavior spíše než relaxujete assertion.
