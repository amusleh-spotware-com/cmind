---
title: Failure-path покрытие карта
description: Каждый failure сценарий mandate требует, mapped в тест(ы) это фактически exercise это — поэтому gap это видимо, не assumed.
---

# Failure-path покрытие карта

Тест mandate это explicit: **failure paths count** — изменение которая может break на dropped connection, rejected order, desync, token rotation, или dead узел ships с тестом для это, в том же commit. Эта страница maps каждый требуемый сценарий в тест(ы) это exercise это, поэтому real gap это *видимо* вместо assumed. Когда вы добавляете failure path, добавьте строку здесь.

## Требуемые сценарии → тесты

| Сценарий | Tier(s) | Тесты |
|---|---|---|
| **Connection drop → reconnect** | unit · stress · E2E | `OpenApiConnectionTests.Dropped_connection_reconnects_and_raises_reconnected`; `FakeTradingSession.Disconnect/ReconnectAsync` и `SyncTradingSession` (DST); `MiscUiTests` reconnect-modal states |
| **Order rejection** | unit · stress | `CopyTransparencyTests.A_rejected_open_emits_a_Failed_execution_fact_with_the_reason`; `CopyCircuitBreakerTests`; DST `CopyDstWorld.FailOrders` / `RejectMarketRange` |
| **Desync / resync** | unit · stress | `CopyPartialFillTests.Resync_tops_up_a_broker_partial_fill…`; `CopyEngineHostTests.Reconnect_resync_closes_orphaned_destination_positions` (+ `…tolerates_a_position_not_found…`); `CopyAdvancedScenariosTests.Reconnect_resync_opens_missing_copies_and_closes_orphans_after_a_desync`; `CopyChaosDstTests` |
| **Token rotation / invalidation** | unit · integration · stress | `OpenApiAuthorizationTests.MarkRefreshFailed_*` (escalation window); `FakeTradingSession.InvalidateToken`; `TokenRotationSignatureTests`, `LiveTokenBootstrapTests`, `OpenApiTokenRefreshPersistenceTests` (integration); DST `RotateTokens` |
| **Node death → lease reclaim** | unit · integration · stress | `NodeInstanceReclaimerTests` (unit + integration); `CopyRulesDomainTests.Lease_is_held_only_by_the_claiming_node_until_it_expires`; `CopyHostWatchdogTests`, `CopyNodeAffinityTests`, `PropFirmTrackingLeaseTests` (integration); `CopyLeaseReclaimStressTests` |
| **AI provider ошибка (4xx/5xx/timeout/malformed)** | unit · integration | `AnthropicAiClientTests.Fails_gracefully_on_error_status` / `…on_malformed_json` / `…on_empty_content`; `AiHttpResilienceTests`, `AiRecommendDisabledTests` (integration) |
| **AI полностью отключен (нет key)** | unit · integration · E2E | `AiFeatureServiceTests`; `AiRecommendDisabledTests`; `AiPagesTests` |
| **Database transient failure / migration lock** | integration | `DatabaseResilienceTests`; `MigrationLockTests` |
| **Node HTTP agent failure / retry** | integration | `NodeAgentHttpResilienceTests` |
| **Container self-exit reconciliation** | unit | `BacktestCompletionPollerTests`; `RunCompletionPoller` coverage в `ContainerCommandHelpersTests` |
| **Prop-firm breach** | unit · integration | `PropFirmChallengeRulesTests`; `PropFirmAlertNotifierTests`; `PropFirmChallengePersistenceTests` |
| **Invalid input / auth reject (UI + branding)** | unit · integration · E2E | `LoginTests.Invalid_credentials_show_an_error`; `HexColorTests.Rejects_invalid_hex`; `BrandingOptionsValidatorTests` |

## Thin spots — verify перед assuming covered

Это worth an explicit check (добавьте строку выше один раз confirmed или filled):

- **MCP tool auth rejection** — `McpKeyAuthHandler` отклоняет bad/absent key. Нет dedicated тест был найден; добавьте integration тест это calls MCP tool endpoint с missing/invalid key и asserts 401.
- **CBot build failure surfacing** — compile ошибка должна land на instance/UI как `Failed` с build output. `CBotLifecycleTests` покрывает happy path; confirm failure branch это asserted.
- **Live order execution** — end-to-end copy выполнение против real cTrader учетных данных остается gated (требует учетные данные + node cluster); смотрите [Live copy trading](./live-copy-trading.md).

## Как это enforced

Deterministic stress suite (DST, `tests/StressTests`) replays эти failures на compressed clock и должен остаться green — **никогда не ослабляй DST сценарий чтобы сделать это pass; fix код**. [FakeTradingSession](./fake-trading-session.md) это cTrader-faithful simulator эти unit тесты drive; extend это для новый broker поведение вместо relaxing assertion.
