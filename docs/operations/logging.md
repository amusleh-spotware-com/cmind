# Logging & observability

All three services (Web, MCP, ExternalNode) log through **Serilog** as **compact JSON on stdout**,
so container runtimes and log collectors (Loki, ELK, CloudWatch, Azure Log Analytics, Datadog)
ingest structured events directly — no parsing of free-text lines.

## Pipeline

- **Sink 1 — Console (compact JSON).** `RenderedCompactJsonFormatter`; every event carries the full
  OpenTelemetry resource identity — `service.name` (`cmind-web` / `cmind-mcp` / `cmind-node-agent` /
  `cmind-copy-agent`), `service.version`, `service.namespace` (`cmind`), `deployment.environment` —
  plus `trace_id` / `span_id` from the ambient `Activity` (`ActivityEnricher`) and `LogContext`
  scopes. The trace ids let CloudWatch Logs Insights and Azure Log Analytics pivot log↔trace **even
  with no collector deployed**.
- **Sink 2 — OTLP (optional).** When `OTEL_EXPORTER_OTLP_ENDPOINT` is set, logs are also exported
  over OTLP carrying the same resource attributes, alongside the OpenTelemetry **metrics** and
  **traces** (ASP.NET Core, HttpClient, runtime instrumentation) emitted by `AddAppTelemetry`.
- **Sink 3 — Azure Monitor (optional).** When `APPLICATIONINSIGHTS_CONNECTION_STRING` is set,
  traces and metrics export **natively** to Application Insights (`AddAzureMonitorTraceExporter` /
  `AddAzureMonitorMetricExporter`) — no collector required. See the cloud-native section below.
- **Source-generated messages.** Application logs use the strongly-typed `LogMessages` extensions
  (`Core/Logging/LogMessages.cs`) with stable `EventId`s — never ad-hoc `ILogger.LogInformation`.
- **Request logging.** `UseSerilogRequestLogging()` emits one structured summary per HTTP request
  (method, path, status, elapsed ms).

## Configuration

Levels are tunable per service via the `Serilog` section in `appsettings.json` (read through
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

Set one env var on each service and point it at an OTLP endpoint:

```
OTEL_EXPORTER_OTLP_ENDPOINT=http://otel-collector:4317
```

- Helm: `--set observability.otlpEndpoint=http://otel-collector:4317`.
- Compose / cloud: add the env var to each service.

From the collector, fan out to your backend (Tempo/Jaeger for traces, Prometheus for metrics, Loki
for logs) with trace↔log correlation intact.

## Cloud-native backends (no extra collector)

Both managed deployments are wired for their native observability stack out of the box — you do not
have to stand up an OTLP collector.

### Azure — Application Insights + Log Analytics

`deploy/azure/main.bicep` provisions a **workspace-based Application Insights** component and passes
its connection string to Web and MCP as `APPLICATIONINSIGHTS_CONNECTION_STRING`. Result:

- **Traces + metrics** flow natively into App Insights (Application Map, live metrics, end-to-end
  transaction search), correlated by `trace_id`.
- **Logs** (compact JSON on stdout) land in the **same Log Analytics workspace** via Container Apps
  `appLogsConfiguration`, so `AppTraces` / `ContainerAppConsoleLogs_CL` join on the trace id.
- Set the optional `otlpEndpoint` Bicep param to *also* fan out to a collector.

Query logs in Log Analytics (the JSON line is in `Log_s`):

```kusto
ContainerAppConsoleLogs_CL
| extend log = parse_json(Log_s)
| where log.["service.name"] == "cmind-web"
| project TimeGenerated, level = log["@l"], msg = log["@m"], trace_id = log.trace_id
```

### AWS — X-Ray + CloudWatch (ADOT sidecar)

`deploy/aws/main.tf` runs an **AWS Distro for OpenTelemetry (ADOT) collector as a sidecar** in each
Fargate task. The app exports OTLP to `http://localhost:4317`; the sidecar ships:

- **traces → AWS X-Ray** (`awsxray` exporter),
- **metrics → CloudWatch** (`awsemf`, namespace `cmind`, log group `/ecs/<prefix>/metrics`).
- **Logs** stay on the `awslogs` driver as compact JSON; CloudWatch Logs Insights auto-discovers the
  JSON fields, so you can `filter` / `stats` on `trace_id`, `service.name`, `@l`, etc.

The task role (`aws_iam_role.task`) carries `AWSXRayDaemonWriteAccess` + `CloudWatchAgentServerPolicy`.

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

These are mapped in **all** environments (previously Dev-only) so Kubernetes and cloud probes work
in production.
