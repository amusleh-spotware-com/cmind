---
title: Failure-path coverage map
description: Every failure scenario the mandate requires, mapped to the test(s) that actually exercise it — so a gap is visible, not assumed.
---

# Failure-path coverage map

The test mandate is explicit: **failure paths count** — a change that can break on a dropped
connection, a rejected order, a desync, a token rotation, or a dead node ships with a test for that,
in the same commit. This page maps each required scenario to the test(s) that exercise it, so a real
gap is *visible* rather than assumed. When you add a failure path, add a row here.

## Required scenarios → tests

| Scenario | Tier(s) | Tests |
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

These are worth an explicit check (add a row above once confirmed or filled):

- **MCP tool auth rejection** — `McpKeyAuthHandler` rejects a bad/absent key. No dedicated test was
  found; add an integration test that calls an MCP tool endpoint with a missing/invalid key and
  asserts 401.
- **CBot build failure surfacing** — a compile error must land on the instance/UI as `Failed` with the
  build output. `CBotLifecycleTests` covers the happy path; confirm the failure branch is asserted.
- **Live order execution** — end-to-end copy execution against real cTrader credentials remains gated
  (needs credentials + a node cluster); see [Live copy trading](./live-copy-trading.md).

## How this is enforced

The deterministic stress suite (DST, `tests/StressTests`) replays these failures on a compressed
clock and must stay green — **never weaken a DST scenario to make it pass; fix the code**. The
[FakeTradingSession](./fake-trading-session.md) is the cTrader-faithful simulator these unit tests
drive; extend it for new broker behavior rather than relaxing an assertion.
