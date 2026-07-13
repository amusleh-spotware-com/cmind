---
title: Карта покривености failure-path
description: Сваки failure сценарио који мандат захтева, пресликан на тест(ове) који га заиста вежбају — тако да је јаз видљив, не претпостављен.
---

# Карта покривености failure-path

Мандат теста је експлицитан: **failure путање броје** — промена која може пукнути на прекинутој вези, одбијеном налогу, десинку, ротацији токена или мртвој чвори доставља тест за то, у истом комиту. Ова страна пресликава сваки потребан сценарио на тест(ове) који га вежбају, тако да прави јаз буде *видљив* уместо претпостављен. Када додатеш failure путање, додај ред овде.

## Потребни сценарији → тестови

| Сценарио | Нивој(и) | Тестови |
|---|---|---|
| **Connection drop → reconnect** | unit · stress · E2E | `OpenApiConnectionTests.Dropped_connection_reconnects_and_raises_reconnected`; `FakeTradingSession.Disconnect/ReconnectAsync` и `SyncTradingSession` (DST); `MiscUiTests` reconnect-modal stanja |
| **Order rejection** | unit · stress | `CopyTransparencyTests.A_rejected_open_emits_a_Failed_execution_fact_with_the_reason`; `CopyCircuitBreakerTests`; DST `CopyDstWorld.FailOrders` / `RejectMarketRange` |
| **Desync / resync** | unit · stress | `CopyPartialFillTests.Resync_tops_up_a_broker_partial_fill…`; `CopyEngineHostTests.Reconnect_resync_closes_orphaned_destination_positions` (+ `…tolerates_a_position_not_found…`); `CopyAdvancedScenariosTests.Reconnect_resync_opens_missing_copies_and_closes_orphans_after_a_desync`; `CopyChaosDstTests` |
| **Token rotation / invalidation** | unit · integration · stress | `OpenApiAuthorizationTests.MarkRefreshFailed_*` (escalation window); `FakeTradingSession.InvalidateToken`; `TokenRotationSignatureTests`, `LiveTokenBootstrapTests`, `OpenApiTokenRefreshPersistenceTests` (integration); DST `RotateTokens` |
| **Node death → lease reclaim** | unit · integration · stress | `NodeInstanceReclaimerTests` (unit + integration); `CopyRulesDomainTests.Lease_is_held_only_by_the_claiming_node_until_it_expires`; `CopyHostWatchdogTests`, `CopyNodeAffinityTests`, `PropFirmTrackingLeaseTests` (integration); `CopyLeaseReclaimStressTests` |
| **AI provider error (4xx/5xx/timeout/malformed)** | unit · integration | `AnthropicAiClientTests.Fails_gracefully_on_error_status` / `…on_malformed_json` / `…on_empty_content`; `AiHttpResilienceTests`, `AiRecommendDisabledTests` (integration) |
| **AI fully disabled (no key)** | unit · integration · E2E | `AiFeatureServiceTests`; `AiRecommendDisabledTests`; `AiPagesTests` |
| **Database transient failure / migration lock** | integration | `DatabaseResilienceTests`; `MigrationLockTests` |
| **Node HTTP agent failure / retry** | integration | `NodeAgentHttpResilienceTests` |
| **Container self-exit reconciliation** | unit | `BacktestCompletionPollerTests`; `RunCompletionPoller` покривеност у `ContainerCommandHelpersTests` |
| **Prop-firm breach** | unit · integration | `PropFirmChallengeRulesTests`; `PropFirmAlertNotifierTests`; `PropFirmChallengePersistenceTests` |
| **Invalid input / auth reject (UI + branding)** | unit · integration · E2E | `LoginTests.Invalid_credentials_show_an_error`; `HexColorTests.Rejects_invalid_hex`; `BrandingOptionsValidatorTests` |

## Танке тачке — верификуј пре него што претпостави покривено

Ово су вредне експлицитне провере (додај ред изнад једном потврђено или попуњено):

- **MCP tool auth rejection** — `McpKeyAuthHandler` одбија лош/одсутан кључ. Посвећени тест није пронађен; додај интеграциони тест који позива крајњу тачку MCP tool-а са недостајућим/невалидним кључем и потврди 401.
- **CBot build failure surfacing** — compile грешка мора да слети на instance/UI као `Failed` са build излазом. `CBotLifecycleTests` покрива happy path; потврди да је failure гран потврђена.
- **Live order execution** — end-to-end copy извршавање против правих cTrader акредитива остаје врата (требају акредитиви + node кластер); видите [Live copy trading](./live-copy-trading.md).

## Како се ово намеће

Детерминистички stress пакет (DST, `tests/StressTests`) преиграва ове грешке на сабијеном часовнику и мора остати зелена — **никад не ослабиш DST сценарио да би прошао; исправи код**. [FakeTradingSession](./fake-trading-session.md) је cTrader-верна симулација коју ови unit тестови вожају; проширити за ново брокер понашање уместо релаксирања потврде.
