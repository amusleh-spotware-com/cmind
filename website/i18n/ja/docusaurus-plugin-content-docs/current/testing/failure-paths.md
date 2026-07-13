---
title: 失敗パス coverage マップ
description: " mandateが必要なすべての失敗シナリオを、それを実際に運動するテストにマッピング — gapが可視、而非 assumed."
---

# 失敗パス coverage マップ

テスト mandateは明確です：**失敗パスがカウント** — ドロップされた接続、拒否された注文、非同期/再同期、トークンローテーション、またはデッドノードで壊れる可能性のある変更は、同じコミットでそのようなテストをshipped伴います。このページは各必要なシナリオをそれを運動するテストにマッピング因此  реальнаяギャップは*可視而非 assumed*です。失敗パスを追加するとき、ここに行を追加してください。

## 必要なシナリオ → テスト

| シナリオ | 層 | テスト |
|---|---|---|
| **接続ドロップ → 再接続** | unit · stress · E2E | `OpenApiConnectionTests.Dropped_connection_reconnects_and_raises_reconnected`; `FakeTradingSession.Disconnect/ReconnectAsync`と`SyncTradingSession`（DST）; `MiscUiTests`再接続モーダル状態 |
| **注文拒否** | unit · stress | `CopyTransparencyTests.A_rejected_open_emits_a_Failed_execution_fact_with_the_reason`; `CopyCircuitBreakerTests`; DST `CopyDstWorld.FailOrders` / `RejectMarketRange` |
| **非同期 / 再同期** | unit · stress | `CopyPartialFillTests.Resync_tops_up_a_broker_partial_fill…`; `CopyEngineHostTests.Reconnect_resync_closes_orphaned_destination_positions`（+ `…tolerates_a_position_not_found…`）; `CopyAdvancedScenariosTests.Reconnect_resync_opens_missing_copies_and_closes_orphans_after_a_desync`; `CopyChaosDstTests` |
| **トークンローテーション / 無効化** | unit · integration · stress | `OpenApiAuthorizationTests.MarkRefreshFailed_*`（エスカレーションウィンドウ）; `FakeTradingSession.InvalidateToken`; `TokenRotationSignatureTests`、`LiveTokenBootstrapTests`、`OpenApiTokenRefreshPersistenceTests`（統合）; DST `RotateTokens` |
| **ノード死 → lease回収** | unit · integration · stress | `NodeInstanceReclaimerTests`（unit + 統合）; `CopyRulesDomainTests.Lease_is_held_only_by_the_claiming_node_until_it_expires`; `CopyHostWatchdogTests`、`CopyNodeAffinityTests`、`PropFirmTrackingLeaseTests`（統合）; `CopyLeaseReclaimStressTests` |
| **AIプロバイダーエラー（4xx/5xx/timeout/malformed）** | unit · integration | `AnthropicAiClientTests.Fails_gracefully_on_error_status` / `…on_malformed_json` / `…on_empty_content`; `AiHttpResilienceTests`、`AiRecommendDisabledTests`（統合） |
| **AI完全に無効（キーなし）** | unit · integration · E2E | `AiFeatureServiceTests`; `AiRecommendDisabledTests`; `AiPagesTests` |
| **データベースの一時的障害 / 移行ロック** | integration | `DatabaseResilienceTests`; `MigrationLockTests` |
| **ノードHTTPエージェント障害 / 再試行** | integration | `NodeAgentHttpResilienceTests` |
| **コンテナ自己終了照合** | unit | `BacktestCompletionPollerTests`; `RunCompletionPoller`のcoverage `ContainerCommandHelpersTests` |
| **Prop-firm侵害** | unit · integration | `PropFirmChallengeRulesTests`; `PropFirmAlertNotifierTests`; `PropFirmChallengePersistenceTests` |
| **無効な入力 / 認証拒否（UI + ブランディング）** | unit · integration · E2E | `LoginTests.Invalid_credentials_show_an_error`; `HexColorTests.Rejects_invalid_hex`; `BrandingOptionsValidatorTests` |

## 薄いスポット — assumecoveredの前に確認

これらは明示的に確認する価値があります（確認または埋められたらここに追加）：

- **MCPツール認証拒否** — `McpKeyAuthHandler`が悪い/欠落したキーを拒否します。専用テストが見つかりませんでしたが、欠落/無効なキーでMCPツールエンドポイントを呼び出し、401を主張する統合テストを追加してください。
- **cBotビルド失敗表面化** — コンパイルエラーは`Failed`としてインスタンス/UIに着地する必要があります + ビルド出力付き。`CBotLifecycleTests`幸せなパスをカバーします； 失敗分支がアサートされていることを確認してください。
- **ライブ注文実行** — 本当のcTrader認証情報に対する実際のコピー実行のエンドツーエンドは、認証情報 + ノードクラスターが必要です（まだgated）； [Live copy trading](./live-copy-trading.md)を参照。

## これが強制される方法

決定論的ストレススイート（DST、`tests/StressTests`）は圧縮されたクロックでこれらの失敗を再生し、グリーンを維持する必要があります — DSTシナリオを合格させるために弱めないでください; コードを修正してください。[FakeTradingSession](./fake-trading-session.md)はこれらのユニットテストが駆動するcTrader-faithfulシミュレーターです； 新しいbroker動作のために拡張してください而非 アサーションを緩和します。
