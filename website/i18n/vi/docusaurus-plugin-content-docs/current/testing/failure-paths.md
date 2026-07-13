---
title: Bản đồ coverage failure-path
description: Mọi failure scenario mandate yêu cầu, được ánh xạ đến test(s) thực sự exercise nó — vì vậy một khoảng trống nhìn thấy được, không phải giả định.
---

# Bản đồ coverage failure-path

Mandate test rõ ràng: **failure paths được tính** — một thay đổi có thể break trên dropped
connection, rejected order, desync, token rotation, hoặc dead node ship với một test cho nó,
trong cùng một commit. Trang này ánh xạ mỗi scenario được yêu cầu đến test(s) exercise nó, vì vậy một khoảng trống thực
*sẽ thấy được* hơn là giả định. Khi bạn thêm một failure path, thêm một hàng ở đây.

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

## Vùng mỏng — xác minh trước khi giả định đã cover

Những thứ này đáng để kiểm tra rõ ràng (thêm hàng ở trên khi đã xác nhận hoặc đã fill):

- **MCP tool auth rejection** — `McpKeyAuthHandler` từ chối key sai/vắng mặt. Không tìm thấy test chuyên dụng;
  thêm một integration test gọi endpoint MCP tool với key thiếu/không hợp lệ và assert 401.
- **CBot build failure surfacing** — compile error phải hiển thị trên instance/UI là `Failed` với
  build output. `CBotLifecycleTests` cover happy path; xác nhận failure branch được assert.
- **Live order execution** — copy execution end-to-end với tài khoản cTrader thực vẫn bị gated
  (cần credentials + node cluster); xem [Live copy trading](./live-copy-trading.md).

## Cách điều này được enforce

Deterministic stress suite (DST, `tests/StressTests`) replay các failure này trên compressed
clock và phải giữ green — **không bao giờ weaken một DST scenario để nó pass; sửa code**. [FakeTradingSession](./fake-trading-session.md) là cTrader-faithful simulator các unit test này drive; extend nó cho broker behavior mới hơn là relax một assertion.
