---
description: "Logging & observability — όλα τα τρία services (Web, MCP, CtraderCliNode) καταγράφουν μέσω Serilog ως compact JSON στο stdout — οι container runtimes και οι log collectors (Loki, ELK, CloudWatch…) τα καταναλώνουν απευθείας."
---

# Logging & observability

Και τα τρία services (Web, MCP, CtraderCliNode) καταγράφουν μέσω **Serilog** ως **compact JSON
στο stdout** — οι container runtimes και οι log collectors (Loki, ELK, CloudWatch, Azure Log
Analytics, Datadog) καταναλώνουν structured events απευθείας, χωρίς free-text parsing.

## Pipeline

- **Sink 1 — Console (compact JSON).** `RenderedCompactJsonFormatter`· κάθε event φέρει πλήρη
  OpenTelemetry resource identity — `service.name` (`cmind-web` / `cmind-mcp` / `cmind-node-agent` /
  `cmind-copy-agent`), `service.version`, `service.namespace` (`cmind`), `deployment.environment` —
  συν `trace_id` / `span_id` από ambient `Activity` (`ActivityEnricher`) και `LogContext` scopes.
  Τα trace ids επιτρέπουν στο CloudWatch Logs Insights και στο Azure Log Analytics να κάνουν
  pivot log↔trace **ακόμα και χωρίς collector deployed**.
- **Sink 2 — OTLP (προαιρετικό).** Όταν θέσετε `OTEL_EXPORTER_OTLP_ENDPOINT`, τα logs επίσης
  εξάγονται μέσω OTLP με τα ίδια resource attributes, μαζί με OpenTelemetry **metrics** και
  **traces** (ASP.NET Core, HttpClient, runtime instrumentation) από `AddAppTelemetry`.
- **Sink 3 — Azure Monitor (προαιρετικό).** Όταν θέσετε `APPLICATIONINSIGHTS_CONNECTION_STRING`,
  τα traces και metrics εξάγονται **native** στο Application Insights
  (`AddAzureMonitorTraceExporter` / `AddAzureMonitorMetricExporter`) — χωρίς collector. Δείτε
  το cloud-native section παρακάτω.
- **Source-generated messages.** Τα app logs χρησιμοποιούν strongly-typed `LogMessages` extensions
  (`Core/Logging/LogMessages.cs`) με stable `EventId`s — ποτέ ad-hoc `ILogger.LogInformation`.
- **Request logging.** `UseSerilogRequestLogging()` εκπέμπει ένα structured summary ανά HTTP
  request (method, path, status, elapsed ms).

## Διαμόρφωση

Τα επίπεδα ρυθμίζονται ανά service μέσω του `Serilog` section στο `appsettings.json`
(διαβάζεται μέσω `ReadFrom.Configuration`), π.χ.:

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

Override σε runtime με env vars, π.χ. `Serilog__MinimumLevel__Default=Debug`.

## Αποστολή σε collector

Θέστε ένα env var ανά service, στοχεύστε σε OTLP endpoint:

```
OTEL_EXPORTER_OTLP_ENDPOINT=http://otel-collector:4317
```

- Helm: `--set observability.otlpEndpoint=http://otel-collector:4317`.
- Compose / cloud: προσθέστε env var σε κάθε service.

Από τον collector, fan out σε backend (Tempo/Jaeger traces, Prometheus metrics, Loki logs) με
trace↔log correlation intact.

## Cloud-native backends (χωρίς επιπλέον collector)

Και οι δύο managed deployments είναι καλωδιωμένες για native observability stack out of box —
δεν χρειάζεται OTLP collector.

### Azure — Application Insights + Log Analytics

Το `deploy/azure/main.bicep` προμηθεύει **workspace-based Application Insights** component,
περνά το connection string του στο Web και MCP ως `APPLICATIONINSIGHTS_CONNECTION_STRING`.
Αποτέλεσμα:

- **Traces + metrics** ρέουν native στο App Insights (Application Map, live metrics, end-to-end
  transaction search), correlated by `trace_id`.
- **Logs** (compact JSON on stdout) προσγειώνονται στο **ίδιο Log Analytics workspace** μέσω
  Container Apps `appLogsConfiguration`, οπότε `AppTraces` / `ContainerAppConsoleLogs_CL` join
  on trace id.
- Θέστε προαιρετικό `otlpEndpoint` Bicep param για να **επίσης** fan out σε collector.

Query logs στο Log Analytics (JSON line in `Log_s`):

```kusto
ContainerAppConsoleLogs_CL
| extend log = parse_json(Log_s)
| where log["service.name"] == "cmind-web"
| project TimeGenerated, level = log["@l"], msg = log["@m"], trace_id = log.trace_id
```

### AWS — X-Ray + CloudWatch (ADOT sidecar)

Το `deploy/aws/main.tf` τρέχει **AWS Distro for OpenTelemetry (ADOT) collector ως sidecar** σε
κάθε Fargate task. Η εφαρμογή εξάγει OTLP στο `http://localhost:4317`· το sidecar ships:

- **traces → AWS X-Ray** (`awsxray` exporter),
- **metrics → CloudWatch** (`awsemf`, namespace `cmind`, log group `/ecs/<prefix>/metrics`).
- **Logs** μένουν στον `awslogs` driver ως compact JSON· το CloudWatch Logs Insights
  auto-discovers JSON fields, οπότε κάνετε `filter` / `stats` on `trace_id`, `service.name`,
  `@l`, etc.

Το task role (`aws_iam_role.task`) φέρει `AWSXRayDaemonWriteAccess` + `CloudWatchAgentServerPolicy`.

Query logs στο CloudWatch Logs Insights:

```
fields @timestamp, @l, @m, trace_id, `service.name`
| filter `service.name` = "cmind-web"
| sort @timestamp desc
```

## Health endpoints (επίσης χρησιμοποιούνται από probes)

| Endpoint | Service | Σημασία |
|----------|---------|---------|
| `/alive` | Web | Liveness — μόνο process. |
| `/health` | Web | Readiness — περιλαμβάνει το database check. |
| `/version` | Web, MCP | Product + protocol version (MCP liveness/readiness). |

Έχουν map σε **όλα** τα περιβάλλοντα (πρώην Dev-only) ώστε τα Kubernetes και cloud probes να
λειτουργούν στην παραγωγή.
