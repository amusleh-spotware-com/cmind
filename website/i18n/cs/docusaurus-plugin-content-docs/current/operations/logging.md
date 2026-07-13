---
description: "All three services (Web, MCP, CtraderCliNode) log via Serilog as compact JSON on stdout — container runtimes and log collectors (Loki, ELK, CloudWatch…"
---

# Logging & observability

All three services (Web, MCP, CtraderCliNode) log via **Serilog** as **compact JSON on stdout** —
container runtimes and log collectors (Loki, ELK, CloudWatch, Azure Log Analytics, Datadog) ingest
structured events directly, no free-text parsing.

## Pipeline

- **Sink 1 — Console (compact JSON).** `RenderedCompactJsonFormatter`; every event carries full
  OpenTelemetry resource identity — `service.name` (`cmind-web` / `cmind-mcp` / `cmind-node-agent` /
  `cmind-copy-agent`), `service.version`, `service.namespace` (`cmind`), `deployment.environment` —
  plus `trace_id` / `span_id` from ambient `Activity` (`ActivityEnricher`) and `LogContext` scopes.
  Trace ids let CloudWatch Logs Insights and Azure Log Analytics pivot log↔trace **even with no
  collector deployed**.
- **Sink 2 — OTLP (optional).** When `OTEL_EXPORTER_OTLP_ENDPOINT` set, logs also export over OTLP
  with same resource attributes, alongside OpenTelemetry **metrics** and **traces** (ASP.NET Core,
  HttpClient, runtime instrumentation) from `AddAppTelemetry`.
- **Sink 3 — Azure Monitor (optional).** When `APPLICATIONINSIGHTS_CONNECTION_STRING` set, traces
  and metrics export **natively** to Application Insights (`AddAzureMonitorTraceExporter` /
  `AddAzureMonitorMetricExporter`) — no collector. See cloud-native section below.
- **Source-generated messages.** App logs use strongly-typed `LogMessages` extensions
  (`Core/Logging/LogMessages.cs`) with stable `EventId`s — never ad-hoc `ILogger.LogInformation`.
- **Request logging.** `UseSerilogRequestLogging()` emits one structured summary per HTTP request
  (method, path, status, elapsed ms).

## Konfigurace

Levels tunable per service via `Serilog` section in `appsettings.json` (read through
`ReadFrom.Configuration`), e.g.:

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

Override at runtime with env vars, e.g. `Serilog__MinimumLevel__Default=Debug`.

## Shipping to a collector

Set one env var per service, point at OTLP endpoint:

```
OTEL_EXPORTER_OTLP_ENDPOINT=http://otel-collector:4317
```

- Helm: `--set observability.otlpEndpoint=http://otel-collector:4317`.
- Compose / cloud: add env var to each service.

From collector, fan out to backend (Tempo/Jaeger traces, Prometheus metrics, Loki logs) with
trace↔log correlation intact.

## Cloud-native backends (no extra collector)

Both managed deployments wired for native observability stack out of box — no OTLP collector needed.

### Azure — Application Insights + Log Analytics

`deploy/azure/main.bicep` provisions **workspace-based Application Insights** component, passes its
connection string to Web and MCP as `APPLICATIONINSIGHTS_CONNECTION_STRING`. Result:

- **Traces + metrics** flow natively into App Insights (Application Map, live metrics, end-to-end
  transaction search), correlated by `trace_id`.
- **Logs** (compact JSON on stdout) land in **same Log Analytics workspace** via Container Apps
  `appLogsConfiguration`, so `AppTraces` / `ContainerAppConsoleLogs_CL` join on trace id.
- Set optional `otlpEndpoint` Bicep param to *also* fan out to a collector.

Query logs in Log Analytics (JSON line in `Log_s`):

```kusto
ContainerAppConsoleLogs_CL
| extend log = parse_json(Log_s)
| where log.["service.name"] == "cmind-web"
| project TimeGenerated, level = log["@l"], msg = log["@m"], trace_id = log.trace_id
```

### AWS — X-Ray + CloudWatch (ADOT sidecar)

`deploy/aws/main.tf` runs **AWS Distro for OpenTelemetry (ADOT) collector as sidecar** in each
Fargate task. App exports OTLP to `http://localhost:4317`; sidecar ships:

- **traces → AWS X-Ray** (`awsxray` exporter),
- **metrics → CloudWatch** (`awsemf`, namespace `cmind`, log group `/ecs/<prefix>/metrics`).
- **Logs** stay on `awslogs` driver as compact JSON; CloudWatch Logs Insights auto-discovers JSON
  fields, so you `filter` / `stats` on `trace_id`, `service.name`, `@l`, etc.

Task role (`aws_iam_role.task`) carries `AWSXRayDaemonWriteAccess` + `CloudWatchAgentServerPolicy`.

Query logs in CloudWatch Logs Insights:

```
fields @timestamp, @l, @m, trace_id, `service.name`
| filter `service.name` = "cmind-web"
| sort @timestamp desc
```

## Health endpoints (also used by probes)

| Endpoint | Service | Meaning |
|----------|---------|---------|
| `/alive` | Web | Liveness — process only. |
| `/health` | Web | Readiness — includes the database check. |
| `/version` | Web, MCP | Product + protocol version (MCP liveness/readiness). |

Mapped in **all** environments (previously Dev-only) so Kubernetes and cloud probes work in
production.