---
description: "White-labelデプロイはすべての機能を出荷するわけではありません。フィーチャートグルは主な商品機能をオン/オフできます — デプロイ時に設定で、または後でruntimeに再デプロイなしで。すべての機能デフォルト有効；デプロイメントは変更するもののみをリストします。"
---

# フィーチャートグル

White-labelデプロイはすべての機能を出荷するわけではありません。フィーチャートグルは主な商品機能をオン/オフできます — デプロイ時に設定で、または後でruntimeに再デプロイなしで。**すべての機能デフォルト有効**；デプロイメントは変更するもののみをリストします。

## モデル

- `Core.Features.FeatureFlag` — ゲート可能な機能のenum： `Authoring`、`Backtesting`、`Execution`、`CopyTrading`、`Ai`、`PortfolioAgent`、`Alerts`、`PropGuard`、`PropFirm`、`Accounts`、`OpenApi`、`Mcp`、`Compliance`。 Core管理 surface（ダッシュボード、ユーザー、ノード、auth）はゲート可能ではなく、ここには 없습니다。
- `Core.Options.FeaturesOptions` — 設定.baseline、`App:Features`からバインド。各プロパティはデフォルト`true`。
- `Core.Features.IFeatureGate` — **有效的**状態を解決：設定.baselineにオプションのowner-set runtime overrideをオーバーレイ。`Infrastructure.Features.FeatureGate`実装、overrideを brieflyキャッシュ（`FeatureSettings.OverrideCacheTtl`）、変更時に無効化。

Runtime overrideは`feature.<FeatureFlag>`をキーとする`AppSetting`行に保存（値`true`/`false`）。行なし = 「設定.baselineを使用」。

## 機能を無効にする2つの方法

### 1. デプロイメント設定（baseline）

フラグを`App:Features`配下falseに設定。例`appsettings.json`：

```json
{
  "App": {
    "Features": {
      "CopyTrading": false,
      "PropGuard": false
    }
  }
}
```

または環境変数で（double underscore）：

```
App__Features__CopyTrading=false
```

Baselineはバックグラウンドワーカー（`Nodes.AddNodes`）とMCPツール（`Mcp`サーバー）の**起動時登録**をゲートするため、設定で無効化されたfeatureはホストされたサービスを開始せず、MCPツールを露出しません。

### 2. Runtime override（owner）

所有者は**Settings → Features**（`/settings/features`）またはAPIから任意の機能をライブでFlipできます：

```
GET  /api/features            -> [{ "flag": "CopyTrading", "enabled": true }, ...]   (Owner)
PUT  /api/features/{flag}      body { "enabled": false }  -> set override             (Owner)
PUT  /api/features/{flag}      body { "enabled": null  }  -> clear override (revert)  (Owner)
```

Runtime変更はリクエスト時ゲート（ナビゲーション、API）に即座に有効。バックグラウンドワーカーとMCPツールは起動時にゲート、nextプロセス再起動時にruntime変更を選択。

## 各ゲートの強制内容

| レイヤー | メカニズム | タイミング |
|-------|-----------|--------|
| HTTP API | `RouteGroupBuilder.RequireFeature(flag)` エンドポイントフィルタ → 無効時`404` | Runtime |
| ナビゲーション | `NavMenu`は`IFeatureGate.IsEnabled`でリンクを非表示 | Runtime |
| バックグラウンドワーカー | `Nodes.AddNodes`での条件付き`AddHostedService` | 起動時（設定） |
| MCPツール | MCPサーバーの条件付き`WithTools<>` | 起動時（設定） |

深いリンクで無効化されたfeatureに到達すると空のページが 렌derされます — そのAPIは`404`を返します； navはそれをさらにsurfaceしません。

## フラグ→表面マップ

| フラグ | APIグループ | Nav | ワーカー / MCP |
|------|-----------|-----|----------------|
| Authoring | `/api/cbots`、`/api/paramsets`、`/api/builder` | cBotsグループ → cBots（per-cBotダイアログのパラメータセット） | MCP `CBotTools` |
| Backtesting | （`/api/instances`を共有） | cBotsグループ → バックテスト | — |
| Execution | `/api/instances` | cBotsグループ → 実行 | MCP `InstanceTools` |
| CopyTrading | `/api/copy` | Copy Trading | `CopyEngineSupervisor`、`OpenApiTokenRefreshService`、MCP `CopyTools` |
| Ai | `/api/ai` | AIグループ → AI； Settings → AI（キー） | `AiRiskGuard`、MCP `AiTools` |
| PortfolioAgent | `/api/agent` | AIグループ → Portfolio Agent | `PortfolioAgentService` |
| Alerts | `/api/alerts` | AIグループ → Alerts | `AlertEvaluator` |
| PropGuard | `/api/prop` | Propグループ → Prop Guard | `PropGuardService` |
| PropFirm | `/api/prop-firm` | Propグループ → Challenges | — |
| Accounts | `/api/ctids` | Trading Accounts | — |
| OpenApi | `/api/openapi` | Settings → Open API | — |
| Mcp | `/api/mcp-keys` | AIグループ → MCP Keys | — |
| Compliance | `/api/compliance` | Settings → Legal & Privacy | — |

## テスト

- **ユニット** — `UnitTests/Features/FeaturesOptionsTests.cs`: baselineデフォルト、per-flagマッピング。
- **統合** — `IntegrationTests/FeatureGateTests.cs`: 設定.baseline、runtime overrideが設定を打ち消し、real Postgresで永続化、クリアでbaselineに revert（real Postgres）。
- **E2E** — `E2ETests/FeatureToggleTests.cs`: runtimeで`CopyTrading`を無効にするとnavリンクが非表示になり`/api/copy`が`404`を返し、再有効化すると两者とも復元。
