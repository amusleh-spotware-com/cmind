---
description: "ทั้งสาม services (Web, MCP, CtraderCliNode) log ผ่าน Serilog เป็น compact JSON บน stdout — container runtimes และ log collectors (Loki, ELK, CloudWatch…)"
---

# Logging & observability

ทั้งสาม services (Web, MCP, CtraderCliNode) log ผ่าน **Serilog** เป็น **compact JSON บน
stdout** — container runtimes และ log collectors (Loki, ELK, CloudWatch, Azure Log Analytics,
Datadog) ingest structured events โดยตรง ไม่มี free-text parsing

## Pipeline

- **Sink 1 — Console (compact JSON).** `RenderedCompactJsonFormatter`; ทุก event มี full
  OpenTelemetry resource identity — `service.name` (`cmind-web` / `cmind-mcp` /
  `cmind-node-agent` / `cmind-copy-agent`), `service.version`, `service.namespace` (`cmind`),
  `deployment.environment` — บวก `trace_id` / `span_id` จาก ambient `Activity`
  (`ActivityEnricher`) และ `LogContext` scopes Trace ids ให้ CloudWatch Logs Insights และ
  Azure Log Analytics pivot log↔trace **แม้กับไม่มี collector deployed**
- **Sink 2 — OTLP (optional).** เมื่อ `OTEL_EXPORTER_OTLP_ENDPOINT` ถูกตั้ง, logs ยัง export
  over OTLP พร้อม same resource attributes, ข้าง OpenTelemetry **metrics** และ **traces**
  (ASP.NET Core, HttpClient, runtime instrumentation) จาก `AddAppTelemetry`
- **Sink 3 — Azure Monitor (optional).** เมื่อ `APPLICATIONINSIGHTS_CONNECTION_STRING`
  ถูกตั้ง, traces และ metrics export **natively** ไปยัง Application Insights
  (`AddAzureMonitorTraceExporter` / `AddAzureMonitorMetricExporter`) — ไม่มี collector ดู
  cloud-native section ข้างล่าง
- **Source-generated messages.** App logs ใช้ strongly-typed `LogMessages` extensions
  (`Core/Logging/LogMessages.cs`) พร้อม stable `EventId`s — ไม่เคย ad-hoc
  `ILogger.LogInformation`
- **Request logging.** `UseSerilogRequestLogging()` emit หนึ่ง structured summary ต่อ HTTP
  request (method, path, status, elapsed ms)

## Configuration

Levels tunable ต่อ service ผ่าน `Serilog` section ใน `appsettings.json` (อ่านผ่าน
`ReadFrom.Configuration`), เช่น:

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

Override at runtime ด้วย env vars, เช่น `Serilog__MinimumLevel__Default=Debug`

## Shipping ไปยัง collector

Set หนึ่ง env var ต่อ service, ชี้ไปที่ OTLP endpoint:

```
OTEL_EXPORTER_OTLP_ENDPOINT=http://otel-collector:4317
```

- Helm: `--set observability.otlpEndpoint=http://otel-collector:4317`
- Compose / cloud: เพิ่ม env var ไปยังแต่ละ service

จาก collector, fan out ไปยัง backend (Tempo/Jaeger traces, Prometheus metrics, Loki logs)
ด้วย trace↔log correlation ที่ intact

## Cloud-native backends (ไม่มี extra collector)

ทั้งสอง managed deployments wired สำหรับ native observability stack out of box — ไม่ต้องการ
OTLP collector

### Azure — Application Insights + Log Analytics

`deploy/azure/main.bicep` provisions **workspace-based Application Insights** component,
ส่ง connection string ไปยัง Web และ MCP เป็น `APPLICATIONINSIGHTS_CONNECTION_STRING`
ผลลัพธ์:

- **Traces + metrics** ไหล natively เข้า App Insights (Application Map, live metrics,
  end-to-end transaction search), correlated โดย `trace_id`
- **Logs** (compact JSON บน stdout) ลงใน **Log Analytics workspace เดียวกัน** ผ่าน
  Container Apps `appLogsConfiguration`, ดังนั้น `AppTraces` / `ContainerAppConsoleLogs_CL`
  join on trace id
- ตั้ง optional `otlpEndpoint` Bicep param เพื่อ *also* fan out ไปยัง collector

Query logs ใน Log Analytics (JSON line ใน `Log_s`):

```kusto
ContainerAppConsoleLogs_CL
| extend log = parse_json(Log_s)
| where log["service.name"] == "cmind-web"
| project TimeGenerated, level = log["@l"], msg = log["@m"], trace_id = log.trace_id
```

### AWS — X-Ray + CloudWatch (ADOT sidecar)

`deploy/aws/main.tf` runs **AWS Distro for OpenTelemetry (ADOT) collector เป็น sidecar**
ในแต่ละ Fargate task App exports OTLP ไปที่ `http://localhost:4317`; sidecar ships:

- **traces → AWS X-Ray** (`awsxray` exporter),
- **metrics → CloudWatch** (`awsemf`, namespace `cmind`, log group `/ecs/<prefix>/metrics`)
- **Logs** stays on `awslogs` driver เป็น compact JSON; CloudWatch Logs Insights
  auto-discovers JSON fields, ดังนั้นคุณ `filter` / `stats` บน `trace_id`, `service.name`,
  `@l`, etc

Task role (`aws_iam_role.task`) carries `AWSXRayDaemonWriteAccess` + `CloudWatchAgentServerPolicy`

Query logs ใน CloudWatch Logs Insights:

```
fields @timestamp, @l, @m, trace_id, `service.name`
| filter `service.name` = "cmind-web"
| sort @timestamp desc
```

## Health endpoints (ใช้โดย probes ด้วย)

| Endpoint | Service | ความหมาย |
|----------|---------|---------|
| `/alive` | Web | Liveness — process เท่านั้น |
| `/health` | Web | Readiness — รวม database check |
| `/version` | Web, MCP | Product + protocol version (MCP liveness/readiness) |

Mapped ใน **ทุก** environments (ก่อนหน้า Dev-only) ดังนั้น Kubernetes และ cloud probes
ทำงานใน production
