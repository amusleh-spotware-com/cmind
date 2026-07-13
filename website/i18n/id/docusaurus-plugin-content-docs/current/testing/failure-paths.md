---
title: Peta failure-path coverage
description: Setiap failure scenario yang mandate butuhkan, mapped ke test yang benar-benar exercise — jadi gap visible, tidak assumed.
---

# Peta failure-path coverage

Mandate test adalah explicit: **failure path count** — change yang dapat break di dropped connection, rejected order, desync, token rotation, atau dead node ships dengan test untuk itu, dalam same commit. Halaman ini map setiap required scenario ke test yang exercise, jadi real gap *visible* daripada assumed. Saat Anda tambahkan failure path, tambahkan row di sini.

## Required scenario → test

| Scenario | Tier | Test |
|---|---|---|
| **Connection drop → reconnect** | unit · stress · E2E | `OpenApiConnectionTests.Dropped_connection_reconnects_and_raises_reconnected`; `FakeTradingSession.Disconnect/ReconnectAsync` dan `SyncTradingSession` (DST); `MiscUiTests` reconnect-modal state |
| **Order rejection** | unit · stress | `CopyTransparencyTests.A_rejected_open_emits_a_Failed_execution_fact_with_the_reason`; `CopyCircuitBreakerTests`; DST `CopyDstWorld.FailOrders` / `RejectMarketRange` |
| **Desync / resync** | unit · stress | `CopyPartialFillTests.Resync_tops_up_a_broker_partial_fill…`; `CopyEngineHostTests.Reconnect_resync_closes_orphaned_destination_positions` (+ `…tolerates_a_position_not_found…`); `CopyAdvancedScenariosTests.Reconnect_resync_opens_missing_copies_and_closes_orphans_after_a_desync`; `CopyChaosDstTests` |
| **Token rotation / invalidation** | unit · integration · stress | `OpenApiAuthorizationTests.MarkRefreshFailed_*` (escalation window); `FakeTradingSession.InvalidateToken`; `TokenRotationSignatureTests`, `LiveTokenBootstrapTests`, `OpenApiTokenRefreshPersistenceTests` (integration); DST `RotateTokens` |
| **Node death → lease reclaim** | unit · integration · stress | `NodeInstanceReclaimerTests` (unit + integration); `CopyRulesDomainTests.Lease_is_held_only_by_the_claiming_node_until_it_expires`; `CopyHostWatchdogTests`, `CopyNodeAffinityTests`, `PropFirmTrackingLeaseTests` (integration); `CopyLeaseReclaimStressTests` |
| **AI provider error (4xx/5xx/timeout/malformed)** | unit · integration | `AnthropicAiClientTests.Fails_gracefully_on_error_status` / `…on_malformed_json` / `…on_empty_content`; `AiHttpResilienceTests`, `AiRecommendDisabledTests` (integration) |
| **AI fully disabled (tidak ada key)** | unit · integration · E2E | `AiFeatureServiceTests`; `AiRecommendDisabledTests`; `AiPagesTests` |
| **Database transient failure / migration lock** | integration | `DatabaseResilienceTests`; `MigrationLockTests` |
| **Node HTTP agent failure / retry** | integration | `NodeAgentHttpResilienceTests` |
| **Container self-exit reconciliation** | unit | `BacktestCompletionPollerTests`; `RunCompletionPoller` coverage dalam `ContainerCommandHelpersTests` |
| **Prop-firm breach** | unit · integration | `PropFirmChallengeRulesTests`; `PropFirmAlertNotifierTests`; `PropFirmChallengePersistenceTests` |
| **Invalid input / auth reject (UI + branding)** | unit · integration · E2E | `LoginTests.Invalid_credentials_show_an_error`; `HexColorTests.Rejects_invalid_hex`; `BrandingOptionsValidatorTests` |

## Thin spot — verify sebelum assuming covered

Ini worth explicit check (tambahkan row di atas sekali confirmed atau filled):

- **MCP tool auth rejection** — `McpKeyAuthHandler` reject bad/absent key. Tidak ada dedicated test ditemukan; tambahkan integration test yang call MCP tool endpoint dengan missing/invalid key dan assert 401.
- **CBot build failure surfacing** — compile error harus land di instance/UI sebagai `Failed` dengan build output. `CBotLifecycleTests` cover happy path; confirm failure branch asserted.
- **Live order execution** — end-to-end copy execution terhadap real cTrader credential tetap gated (butuh credential + node cluster); lihat [Live copy trading](./live-copy-trading.md).
