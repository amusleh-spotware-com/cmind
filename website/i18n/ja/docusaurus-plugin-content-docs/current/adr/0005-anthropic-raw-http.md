---
title: 0005 — AI クライアント 生HTTP 使用、Anthropic SDK ではなく
description: なぜ IAiClient Anthropic API 呼び出す 生HTTP 経由 SDK 代わり、AI なぜ 完全ゲート キー。
---

# 0005 — AI クライアント 生HTTP 使用、Anthropic SDK ではなく

## コンテキスト

すべて AI 機能 (戦略生成、自己修復、リスクガード、事後分析) Anthropic API 呼び出し。SDK 依存 追加 推移サーフェス コントロール外、結合 リリースケイデンス theirs、隠す 正確 配線契約 必要 推論 復元力 + コスト。

## 決定

`IAiClient` 呼び出し Anthropic **生HTTP** 経由 型 `HttpClient` — 意図的に **SDK ではなく**。`AiFeatureService` シングルオーケストレータ 共有 Web エンドポイント、MCP `AiTools`、`AiRiskGuard`。全サーフェス **ゲート** `AppOptions.Ai.ApiKey`: キー なし、すべて機能 戻り `AiResult.Fail` + アプリ 変更なし実行。

## 結果

- キー不要 ビルド、テスト、E2E — CI + ローカル開発 実行 完全アプリ AI なし。
- コントロール 要求/応答形状、再試行/タイムアウト ポリシー、トークン会計 明快。
- 新Anthropic 機能 配線 手動 by; トレード 便利 コントロール + 小さい依存サーフェス。`claude-api` リファレンス 現在 モデル id + パラメータ。
