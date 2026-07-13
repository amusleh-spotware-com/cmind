---
title: Failure-path coverage map
description: Every failure scenario the mandate requires, mapped to the test(s) that actually exercise it — so a gap is visible, not assumed.
---

# Failure-path coverage map

test mandate explicit: **failure paths count** — change ที่สามารถ break on dropped
connection rejected order desync token rotation หรือ dead node ships ด้วย test สำหรับนั่น
ใน same commit page นี้ maps ทุก required scenario ไป test(s) ที่ exercise มัน ดังนั้น real
gap เป็น *visible* แทน assumed เมื่อคุณ add failure path add row ที่นี่

## Required scenarios → tests

| Scenario | Tier(s) | Tests |
|---|---|---|
| **Connection drop → reconnect** | unit · stress · E2E | `OpenApiConnectionTests.Dropped_connection_reconnects_and_raises_reconnected`; `FakeTradingSession.Disconnect/ReconnectAsync` และ `SyncTradingSession` (DST); `MiscUiTests` reconnect-modal states |
| **Order rejection** | unit · stress | `CopyTransparencyTests.A_rejected_open_emits_a_Failed_execution_fact_with_the_reason`; `CopyCircuitBreakerTests`; DST `CopyDstWorld.FailOrders` / `RejectMarketRange` |
| **Desync / resync** | unit · stress | `CopyPartialFillTests.Resync_tops_up_a_broker_partial_fill…`; `CopyEngineHostTests.Reconnect_resync_closes_orphaned_destination_positions` (+ `…tolerates_a_position_not_found…`); `CopyAdvancedScenariosTests.Reconnect_resync_opens_missing_copies_and_closes_orphans_after_a_desync`; `CopyChaosDstTests` |
| **Token rotation / invalidation** | unit · integration · stress | `OpenApiAuthorizationTests.MarkRefreshFailed_*` (escalation window); `FakeTradingSession.InvalidateToken`; `TokenRotationSignatureTests`, `LiveTokenBootstrapTests`, `OpenApiTokenRefreshPersistenceTests` (integration); DST `RotateTokens` |
| **Node death → lease reclaim** | unit · integration · stress | `NodeInstanceReclaimerTests` (unit + integration); `CopyRulesDomainTests.Lease_is_held_only_by_the_claiming_node_until_it_expires`; `CopyHostWatchdogTests`, `CopyNodeAffinityTests`, `PropFirmTrackingLeaseTests` (integration); `CopyLeaseReclaimStressTests` |
| **AI provider error (4xx/5xx/timeout/malformed)** | unit · integration | `AnthropicAiClientTests.Fails_gracefully_on_error_status` / `…on_malformed_json` / `…on_empty_content`; `AiHttpResilienceTests`, `AiRecommendDisabledTests` (integration) |
| **AI fully disabled (no key)** | unit · integration · E2E | `AiFeatureServiceTests`; `AiRecommendDisabledTests`; `AiPagesTests` |
| **Database transient failure / migration lock** | integration | `DatabaseResilienceTests`; `MigrationLockTests` |
| **Node HTTP agent failure / retry** | integration | `NodeAgentHttpResilienceTests` |
| **Container self-exit reconciliation** | unit | `BacktestCompletionPollerTests`; `RunCompletionPoller` coverage ใน `ContainerCommandHelpersTests` |
| **Prop-firm breach** | unit · integration | `PropFirmChallengeRulesTests`; `PropFirmAlertNotifierTests`; `PropFirmChallengePersistenceTests` |
| **Invalid input / auth reject (UI + branding)** | unit · integration · E2E | `LoginTests.Invalid_credentials_show_an_error`; `HexColorTests.Rejects_invalid_hex`; `BrandingOptionsValidatorTests` |

## Thin spots — verify ก่อน assuming covered

สิ่งเหล่านี้คุ้มค่า explicit check (add row ข้างบน once confirmed หรือ filled):

- **MCP tool auth rejection** — `McpKeyAuthHandler` rejects bad/absent key ไม่มี dedicated test
  found; add integration test ที่ calls MCP tool endpoint ด้วย missing/invalid key และ
  asserts 401
- **CBot build failure surfacing** — compile error must land on instance/UI เป็น `Failed` ด้วย
  build output `CBotLifecycleTests` covers happy path; confirm failure branch asserted
- **Live order execution** — end-to-end copy execution against real cTrader credentials remains gated
  (needs credentials + node cluster); ดู [Live copy trading](./live-copy-trading.md)

## How นี่ enforced

deterministic stress suite (DST `tests/StressTests`) replays failures เหล่านี้ on compressed
clock และ must stay green — **never weaken DST scenario ไป make มันpass; fix code** [FakeTradingSession](./fake-trading-session.md) cTrader-faithful simulator unit tests นี่
drive; extend มัน สำหรับ new broker behavior แทน relaxing assertion
