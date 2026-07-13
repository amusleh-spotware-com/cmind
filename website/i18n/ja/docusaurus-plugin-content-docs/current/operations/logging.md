---
description: "すべての3つのサービス（Web、MCP、CtraderCliNode）はstdoutにコンパクトJSONとしてSerilogでログ — コンテナランタイムとログコレクタ（Loki、ELK、CloudWatch…）が構造化イベントを直接取り込み、フリーテキスト解析不要。"
---

# ロギング＆可観測性

すべての3つのサービス（Web、MCP、CtraderCliNode）は**Serilog**で**stdoutにコンパクトJSON**としてログ — コンテナランタイムとログコレクタ（Loki、ELK、CloudWatch、Azure Log Analytics、Datadog）が構造化イベントを直接取り込み、フリーテキスト解析不要。

## パイプライン

- **Sink 1 — コンソール（コンパクトJSON）。** `RenderedCompactJsonFormatter`；各eventは完全なOpenTelemetryリソースIDを持ち運ります — `service.name`（`cmind-web` / `cmind-mcp` / `cmind-node-agent` / `cmind-copy-agent`）、`service.version`、`service.namespace`（`cmind`）、`deployment.environment` — plus `trace_id` / `span_id`は周囲の`Activity`（`ActivityEnricher`）と`LogContext`スコープから。 Trace idsによりCloudWatch Logs InsightsとAzure Log Analyticsが**log↔traceをピボットできます** コレクタがデプロイされていなくても可能です。
- **Sink 2 — OTLP（オプション）。** `OTEL_EXPORTER_OTLP_ENDPOINT`設定時、ログは同等のリソース属性でOTLP上也export alongside OpenTelemetry **metrics**と**traces**（ASP.NET Core、HttpClient、ランタイム計装）`AddAppTelemetry`から。
- **Sink 3 — Azure Monitor（オプション）。** `APPLICATIONINSIGHTS_CONNECTION_STRING`設定時、トレースとメトリクスはApplication Insightsに**ネイティブに**export（`AddAzureMonitorTraceExporter` / `AddAzureMonitorMetricExporter`） — コレクタなし。以下参照cloud-nativeセクション。
- **ソース生成メッセージ。** アプリログは安定した`EventId`を持つ強く型付けされた`LogMessages`拡張機能を使用（`Core/Logging/LogMessages.cs`） — 而非 ad-hoc `ILogger.LogInformation`。
- **リクエストロギング。** `UseSerilogRequestLogging()`はHTTPリクエストごとに1つの構造化サマリーをemits（メソッド、パス、ステータス、経過ms）。

## 設定

レベルは`appsettings.json`の`Serilog`セクションでサービスごとに調整可能（`ReadFrom.Configuration`経由で読み取り）、例：

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": { "Microsoft.AspNetCore": "Warning", "Microsoft.EntityFrameworkCore": "Warning" }
    }
  }
}
```

起動時に環境変数でオーバーライド、例：`Serilog__MinimumLevel__Default=Debug`。

## コレクタへのShipping

1つenv varをサービスごとに設定、OTLPエンドポイントを指す：

```
OTEL_EXPORTER_OTLP_ENDPOINT=http://otel-collector:4317
```

- Helm: `--set observability.otlpEndpoint=http://otel-collector:4317`。
- Compose / cloud: 各サービスにenv varを追加。

コレクタから、トレース↔ログ相関を维持しながらバックエンドにファンアウト（Tempo/Jaegerトレース、Prometheusメトリクス、Lokiログ）。

## Cloud-nativeバックエンド（追加のコレクタ不要）

両方の管理されたデプロイメントは箱から出してすぐにネイティブ可観測性スタックに接続されます — OTLPコレクタ不要。

### Azure — Application Insights + Log Analytics

`deploy/azure/main.bicep`は**ワークスペースベースのApplication Insights**コンポーネントをプロビジョニングし、その接続文字列をWebとMCPに`APPLICATIONINSIGHTS_CONNECTION_STRING`として渡します。結果：

- **トレース + メトリクス**がApp Insightsにネイティブにフロー（Application Map、ライブメトリクス、エンドツーエンドトランザクション検索）、`trace_id`で相関。
- **ログ**（stdoutのコンパクトJSON）はContainer Apps `appLogsConfiguration`経由で同じLog Analyticsワークスペースに着地 Therefore `AppTraces` / `ContainerAppConsoleLogs_CL`がtrace idで結合。
- オプションのotlpEndpoint Bicep paramを*追加で*コレクタにもファンアウトするように設定。

Log Analyticsでクエリログ（`Log_s`のJSONライン）：

```kusto
ContainerAppConsoleLogs_CL
| extend log = parse_json(Log_s)
| where log.["service.name"] == "cmind-web"
| project TimeGenerated, level = log["@l"], msg = log["@m"], trace_id = log.trace_id
```

### AWS — X-Ray + CloudWatch（ADOT sidecar）

`deploy/aws/main.tf`は各Fargateタスクで**AWS Distro for OpenTelemetry（ADOT）コレクタをsidecar**として実行。AppはOTLPを`http://localhost:4317`にexport； sidecarは以下をshipします：

- **トレース → AWS X-Ray**（`awsxray` exporter）、
- **メトリクス → CloudWatch**（`awsemf`、namespace `cmind`、ロググループ`/ecs/<prefix>/metrics`）。
- **ログ**は`awslogs` driverとしてコンパクトJSONでstay； CloudWatch Logs InsightsはJSONフィールドを自動発見因此 `filter` / `stats` on `trace_id`、`service.name`、`@l`などを使用。

タスクロール（`aws_iam_role.task`）は`AWSXRayDaemonWriteAccess` + `CloudWatchAgentServerPolicy`を運ぶ。

CloudWatch Logs Insightsでログをクエリ：

```
fields @timestamp, @l, @m, trace_id, `service.name`
| filter `service.name` = "cmind-web"
| sort @timestamp desc
```

## ヘルスエンドポイント（プローブでも使用）

| エンドポイント | サービス | 意味 |
|----------|---------|---------|
| `/alive` | Web | ライブネス — プロセスのみ。 |
| `/health` | Web | 準備完了 — データベースチェックを含む。 |
| `/version` | Web、MCP | 商品 + プロトコルバージョン（MCPライブネス/準備完了）。 |

**すべて**的环境（previously Devのみ）でマッピング Therefore Kubernetesとcloudプローブが本番で動作します。
