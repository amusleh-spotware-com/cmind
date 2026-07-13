---
description: "Все три сервиса (Web, MCP, CtraderCliNode) логируют через Serilog как compact JSON на stdout — container runtimes и log collectors (Loki, ELK, CloudWatch…"
---

# Logging & observability

Все три сервиса (Web, MCP, CtraderCliNode) логируют через **Serilog** как **compact JSON на stdout** — container runtimes и log collectors (Loki, ELK, CloudWatch, Azure Log Analytics, Datadog) absorb структурированные события directly, нет free-text parsing.

## Pipeline

- **Sink 1 — Console (compact JSON).** `RenderedCompactJsonFormatter`; каждое событие носит full OpenTelemetry resource identity — `service.name` (`cmind-web` / `cmind-mcp` / `cmind-node-agent` / `cmind-copy-agent`), `service.version`, `service.namespace` (`cmind`), `deployment.environment` — плюс `trace_id` / `span_id` из ambient `Activity` (`ActivityEnricher`) и `LogContext` scopes. Trace ids позволяют CloudWatch Logs Insights и Azure Log Analytics pivot log↔trace **даже с нет collector deployed**.
- **Sink 2 — OTLP (optional).** Когда `OTEL_EXPORTER_OTLP_ENDPOINT` set, логи также export через OTLP с то же resource attributes, наряду с OpenTelemetry **metrics** и **traces** (ASP.NET Core, HttpClient, runtime instrumentation) из `AddAppTelemetry`.
- **Sink 3 — Azure Monitor (optional).** Когда `APPLICATIONINSIGHTS_CONNECTION_STRING` set, traces и metrics export **natively** to Application Insights (`AddAzureMonitorTraceExporter` / `AddAzureMonitorMetricExporter`) — нет collector. See cloud-native секция ниже.
- **Source-generated messages.** App logs используют strongly-typed `LogMessages` extensions (`Core/Logging/LogMessages.cs`) с stable `EventId`s — никогда ad-hoc `ILogger.LogInformation`.
- **Request logging.** `UseSerilogRequestLogging()` emits один structured summary per HTTP request (method, path, status, elapsed ms).

## Configuration

Levels tunable per service через `Serilog` секцию в `appsettings.json` (read через `ReadFrom.Configuration`), например:

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

Override во время выполнения с env vars, например `Serilog__MinimumLevel__Default=Debug`.

## Shipping на collector

Set один env var per service, point на OTLP endpoint:

```
OTEL_EXPORTER_OTLP_ENDPOINT=http://otel-collector:4317
```

- Helm: `--set observability.otlpEndpoint=http://otel-collector:4317`.
- Compose / cloud: add env var к каждому service.

Из collector, fan out to backend (Tempo/Jaeger traces, Prometheus metrics, Loki logs) с trace↔log correlation intact.

## Cloud-native backends (нет extra collector)

Оба managed deployments wired для native observability stack out of box — нет OTLP collector needed.

### Azure — Application Insights + Log Analytics

`deploy/azure/main.bicep` provisions **workspace-based Application Insights** component, passes его connection string к Web и MCP как `APPLICATIONINSIGHTS_CONNECTION_STRING`. Result:

- **Traces + metrics** flow natively в App Insights (Application Map, live metrics, end-to-end transaction search), correlated by `trace_id`.
- **Logs** (compact JSON на stdout) land в **то же Log Analytics workspace** через Container Apps `appLogsConfiguration`, поэтому `AppTraces` / `ContainerAppConsoleLogs_CL` join на trace id.
- Set optional `otlpEndpoint` Bicep param для *также* fan out на collector.

Query логи в Log Analytics (JSON line в `Log_s`):

```kusto
ContainerAppConsoleLogs_CL
| extend log = parse_json(Log_s)
| where log.["service.name"] == "cmind-web"
| project TimeGenerated, level = log["@l"], msg = log["@m"], trace_id = log.trace_id
```

### AWS — X-Ray + CloudWatch (ADOT sidecar)

`deploy/aws/main.tf` runs **AWS Distro для OpenTelemetry (ADOT) collector как sidecar** в каждой Fargate задаче. App exports OTLP на `http://localhost:4317`; sidecar ships:

- **traces → AWS X-Ray** (`awsxray` exporter),
- **metrics → CloudWatch** (`awsemf`, namespace `cmind`, log group `/ecs/<prefix>/metrics`).
- **Logs** stay на `awslogs` driver как compact JSON; CloudWatch Logs Insights auto-discovers JSON fields, поэтому вы `filter` / `stats` на `trace_id`, `service.name`, `@l`, и т.д.

Task role (`aws_iam_role.task`) carries `AWSXRayDaemonWriteAccess` + `CloudWatchAgentServerPolicy`.

Query логи в CloudWatch Logs Insights:

```
fields @timestamp, @l, @m, trace_id, `service.name`
| filter `service.name` = "cmind-web"
| sort @timestamp desc
```

## Health endpoints (также используется probes)

| Endpoint | Service | Meaning |
|----------|---------|---------|
| `/alive` | Web | Liveness — process only. |
| `/health` | Web | Readiness — includes database check. |
| `/version` | Web, MCP | Product + protocol version (MCP liveness/readiness). |

Mapped во **все** environments (ранее Dev-only) поэтому Kubernetes и cloud probes работают в production.
