---
description: "cMind は Model Context Protocol（MCP）サーバーを別プロセス/デプロイメントとして出荷 — Web アプリから独立スケール+再デプロイ。cBot、インスタンス、AI ツールを公開…"
---

# MCP サーバー

cMind は Model Context Protocol（MCP）サーバーを**別プロセス/デプロイメント**として出荷 — Web アプリから独立スケール+再デプロイ。cBot、インスタンス、AI ツールを MCP クライアント（例えば AI アシスタント）に HTTP + SSE トランスポート上で公開。

## 認証

- ユーザーごとの API キー`mcpk_<hex>`、SHA-256 ハッシュ、プレフィックス インデックス（`McpKeyAuthHandler`）。**Mcp** ページから管理（`McpApiKey`アグリゲート）。
- `AddHttpContextAccessor`を持つステートレス HTTP トランスポート — ツール呼び出しは認証ユーザーとして実行。

## ツール

- `CBotTools` — cBot の作成/ビルド。
- `InstanceTools` — インスタンスの実行/バックテスト/検査。
- `AiTools` — 生成、レビュー、センチメント、バックテスト分析、コピー ツール。

## Ops

`/version`を公開；ヘルスエンドポイント（`/health`、`/alive`）はすべての環境でマップ（K8s/クラウド プローブ用）。構造化 Serilog JSON + OpenTelemetry、Web アプリと同じ。
