---
title: Карта покрытия failure-path
description: Каждый required failure scenario, отображённый на тест(ы), который его реально проверяет — чтобы gap был виден, а не предполагаем.
---

# Карта покрытия failure-path

Test mandate однозначен: **failure paths count** — изменение, которое может сломаться при dropped
connection, rejected order, desync, token rotation или dead node, ships with a test for that,
в том же commit. Эта страница отображает каждый required scenario на тест(ы), который его
проверяет, чтобы реальный gap был *видим*, а не предполагаем. Когда добавляете failure path,
добавьте строку сюда.

## Required сценарии → тесты

| Сценарий | Tier(s) | Тесты |
|---|---|---|
| **Connection drop → reconnect** | unit · stress · E2E | `OpenApiConnectionTests.Dropped_connection_reconnects_and_raises_reconnected`; `FakeTradingSession.Disconnect/ReconnectAsync` and `SyncTradingSession` (DST); `MiscUiTests` reconnect-modal states |
| **Order rejection** | unit · stress | `CopyTransparencyTests.A_rejected_open_emits_a_Failed_execution_fact_with_the_reason`; `CopyCircuitBreakerTests`; DST `CopyDstWorld.FailOrders` / `RejectMarketRange` |
| **Desync / resync** | unit · stress | `CopyPartialFillTests.Resync_tops_up_a_broker_partial_fill…`; `CopyEngineHostTests.Reconnect_resync_closes_orphaned_destination_positions` (+ `…tolerates_a_position_not_found…`); `CopyAdvancedScenariosTests.Reconnect_resync_opens_missing_copies_and_closes_orphans_after_a_desync`; `CopyChaosDstTests` |
| **Token rotation / invalidation** | unit · integration · stress | `OpenApiAuthorizationTests.MarkRefreshFailed_*` (escalation window); `FakeTradingSession.InvalidateToken`; `TokenRotationSignatureTests`, `LiveTokenBootstrapTests`, `OpenApiTokenRefreshPersistenceTests` (integration); DST `RotateTokens` |
| **Node death → lease reclaim** | unit · integration · stress | `NodeInstanceReclaimerTests` (unit + integration); `CopyRulesDomainTests.Lease_is_held_only_by_the_claiming_node_until_it_expires`; `CopyHostWatchdogTests`, `CopyNodeAffinityTests`, `PropFirmTrackingLeaseTests` (integration); `CopyLeaseReclaimStressTests` |
| **AI provider error (4xx/5xx/timeout/malformed)** | unit · integration | `AnthropicAiClientTests.Fails_gracefully_on_error_status` / `…on_malformed_json` / `…on_empty_content`; `AiHttpResilienceTests`, `AiRecommendDisabledTests` (integration) |
| **AI полностью отключён (нет key)** | unit · integration · E2E | `AiFeatureServiceTests`; `AiRecommendDisabledTests`; `AiPagesTests` |
| **Database transient failure / migration lock** | integration | `DatabaseResilienceTests`; `MigrationLockTests` |
| **Node HTTP agent failure / retry** | integration | `NodeAgentHttpResilienceTests` |
| **Container self-exit reconciliation** | unit | `BacktestCompletionPollerTests`; `RunCompletionPoller` coverage in `ContainerCommandHelpersTests` |
| **Prop-firm breach** | unit · integration | `PropFirmChallengeRulesTests`; `PropFirmAlertNotifierTests`; `PropFirmChallengePersistenceTests` |
| **Invalid input / auth reject (UI + branding)** | unit · integration · E2E | `LoginTests.Invalid_credentials_show_an_error`; `HexColorTests.Rejects_invalid_hex`; `BrandingOptionsValidatorTests` |

## Thin spots — проверьте перед тем как предполагать покрытие

Эти стоит явно проверить (добавьте строку выше когда подтверждено или заполнено):

- **MCP tool auth rejection** — `McpKeyAuthHandler` отклоняет bad/absent key. Выделенный тест не найден;
  добавьте integration тест, который вызывает MCP tool endpoint с missing/invalid key и assert'ит 401.
- **CBot build failure surfacing** — compile error должен отображаться на instance/UI как `Failed` с
  build output. `CBotLifecycleTests` покрывает happy path; подтвердите что failure branch assert'ится.
- **Live order execution** — end-to-end copy execution против реальных cTrader credentials остаётся gated
  (нужны credentials + node cluster); см. [Live copy trading](./live-copy-trading.md).

## Как это enforced

Deterministic stress suite (DST, `tests/StressTests`) replay'ит эти failures на compressed clock и
должна оставаться зелёной — **никогда не ослабляйте DST сценарий чтобы он прошёл; чините код**.
[FakeTradingSession](./fake-trading-session.md) — это cTrader-faithful simulator, который эти unit
тесты используют; расширяйте его для нового broker-поведения, а не relax'ьте assertion.
