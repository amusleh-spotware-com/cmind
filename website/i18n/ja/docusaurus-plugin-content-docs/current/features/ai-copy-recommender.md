---
description: "AI ヘルパー。フォロワーのリスク プロファイル + ソース(マスター)アカウント説明からコピートレーディング宛先設定安全に推奨。REST API、MCP…経由公開"
---

# AI コピープロファイル推奨システム

AI ヘルパー。フォロワーのリスク プロファイル + ソース(マスター)アカウント説明からコピートレーディング宛先設定安全に推奨。REST API、MCP ツール、コピートレーディング ページ経由公開。忠告のみ — プロファイル作成/ミューテート決してしない; 人間（またはフォローアップ MCP 呼び出し）設定適用。

## モデル

- `IAiFeatureService.RecommendCopyProfileAsync(riskProfile, sourceDescription, ct)` — `AiPrompts.CopyProfileSystem` プロンプトからリクエスト構築、`AiResult` 返す テキスト = 推奨設定 JSON オブジェクト: `riskMode` (`MoneyManagementMode` 名)、`riskParameter`、`maxDrawdownPercent`、`dailyLossLimit`、`direction`、`copyStopLoss`、`copyTakeProfit`、`slippagePips`、短 `rationale`。
- 他のすべての AI 機能のように、`App:Ai:ApiKey` でゲート: キーなし → 呼び出し戻り `AiResult.Fail(disabled)`、アプリ影響なし。

## サーフェス

| サーフェス | エントリ |
|---------|-------|
| REST | `POST /api/ai/recommend-copy-profile` `{ riskProfile, sourceDescription }` → `AiResult` (機能 `Ai`、ロール User+) |
| MCP | `CopyTools.RecommendCopyProfile(riskProfile, sourceDescription)` (機能 `CopyTrading`、AI サービスに委譲) |
| UI | コピートレーディング ページ → **AI 提案** ボタン; 推奨インラインアラート内レンダリング |

推奨は意図的に自動適用されない: フォロワーレビュー、次にプロファイル作成/宛先 通常のコピートレーディング ダイアログ経由（または MCP クライアント解析 JSON + 作成エンドポイント呼び出し）。

## テスト

- **ユニット** — `UnitTests/Ai/AiFeatureServiceRecommendTests.cs`: リスク プロファイル + ソース説明 AI クライアント コピープロファイル システム プロンプト下配信（NSubstitute）。
- **統合** — `IntegrationTests/AiRecommendDisabledTests.cs`: API キーなし → 実際 `AnthropicAiClient` + `AiFeatureService` 障害結果に低下（キーなしアプリ実行）。
- **E2E** — `E2ETests/AiCopyRecommendTests.cs`: **AI 提案** ボタン呼び出しエンドポイント + レンダリング結果（テスト環境にて適正に「未設定」メッセージ）、UI → エンドポイント → AI パス証明。
