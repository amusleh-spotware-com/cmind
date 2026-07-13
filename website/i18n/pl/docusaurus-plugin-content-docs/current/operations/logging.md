---
description: "Wszystkie trzy usługi (Web, MCP, CtraderCliNode) logują przez Serilog jako compact JSON na stdout — runtime'y kontenerów i kolektory logów (Loki, ELK, CloudWatch…"
---

# Logowanie i observability

Wszystkie trzy usługi (Web, MCP, CtraderCliNode) logują przez **Serilog** jako **compact JSON na
stdout** — runtime'y kontenerów i kolektory logów (Loki, ELK, CloudWatch, Azure Log Analytics,
Datadog) ingesting structured events directly, no free-text parsing.

## Pipeline

- **Sink 1 — Console (compact JSON).** `RenderedCompactJsonFormatter`; każde zdarzenie niesie pełną
  tożsamość zasobu OpenTelemetry — `service.name` (`cmind-web` / `cmind-mcp` / `cmind-node-agent` /
  `cmind-copy-agent`), `service.version`, `service.namespace` (`cmind`), `deployment.environment` —
  plus `trace_id` / `span_id` z ambient `Activity` (`ActivityEnricher`) i zakresów `LogContext`.
  Trace id pozwalają CloudWatch Logs Insights i Azure Log Analytics na przeglądanie log↔trace **nawet
  bez wdrożonego collectora**.
- **Sink 2 — OTLP (opcjonalny).** Gdy ustawione `OTEL_EXPORTER_OTLP_ENDPOINT`, logi są również
  eksportowane przez OTLP z tymi samymi atrybutami zasobu, obok **metryk** i **śladów** OpenTelemetry
  (ASP.NET Core, HttpClient, instrumentacja runtime) z `AddAppTelemetry`.
- **Sink 3 — Azure Monitor (opcjonalny).** Gdy ustawione `APPLICATIONINSIGHTS_CONNECTION_STRING`,
  ślady i metryki są eksportowane **natywnie** do Application Insights (`AddAzureMonitorTraceExporter`
  / `AddAzureMonitorMetricExporter`) — bez collectora. Patrz sekcja cloud-native poniżej.
- **Komunikaty generowane ze źródła.** Logi aplikacji używają silnie typowanych rozszerzeń
  `LogMessages` (`Core/Logging/LogMessages.cs`) ze stabilnymi `EventId` — nigdy ad-hoc
  `ILogger.LogInformation`.
- **Logowanie requestów.** `UseSerilogRequestLogging()` emituje jedno strukturalne podsumowanie per
  request HTTP (metoda, ścieżka, status, elapsed ms).

## Konfiguracja

Poziomy konfigurowalne per usługa przez sekcję `Serilog` w `appsettings.json` (czytane przez
`ReadFrom.Configuration`), np.:

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

Override w runtime przez zmienne env, np. `Serilog__MinimumLevel__Default=Debug`.

## Wysyłanie do collectora

Ustaw jedną zmienną env per usługę, wskaż na endpoint OTLP:

```
OTEL_EXPORTER_OTLP_ENDPOINT=http://otel-collector:4317
```

- Helm: `--set observability.otlpEndpoint=http://otel-collector:4317`.
- Compose / cloud: dodaj zmienną env do każdej usługi.

Z collectora, rozdzielaj do backendu (Tempo/Jaeger traces, Prometheus metrics, Loki logs) z
zachowaną korelacją trace↔log.

## Cloud-native backends (bez extra collectora)

Oba managed deployments są połączone pod observability stack out of box — bez potrzeby OTLP collectora.

### Azure — Application Insights + Log Analytics

`deploy/azure/main.bicep` provisioning **workspace-based Application Insights** component, przekazuje
jej connection string do Web i MCP jako `APPLICATIONINSIGHTS_CONNECTION_STRING`. Rezultat:

- **Ślady + metryki** płyną natywnie do App Insights (Application Map, live metrics, end-to-end
  transaction search), skorelowane przez `trace_id`.
- **Logi** (compact JSON na stdout) lądują w **tym samym Log Analytics workspace** przez Container Apps
  `appLogsConfiguration`, więc `AppTraces` / `ContainerAppConsoleLogs_CL` join na trace id.
- Ustaw opcjonalny parametr Bicep `otlpEndpoint`, aby *również* rozdzielić do collectora.

Kwerenduj logi w Log Analytics (JSON line w `Log_s`):

```kusto
ContainerAppConsoleLogs_CL
| extend log = parse_json(Log_s)
| where log.["service.name"] == "cmind-web"
| project TimeGenerated, level = log["@l"], msg = log["@m"], trace_id = log.trace_id
```

### AWS — X-Ray + CloudWatch (ADOT sidecar)

`deploy/aws/main.tf` uruchamia **AWS Distro for OpenTelemetry (ADOT) collector jako sidecar** w
każdym zadaniu Fargate. Aplikacja eksportuje OTLP do `http://localhost:4317`; sidecar wysyła:

- **traces → AWS X-Ray** (`awsxray` exporter),
- **metrics → CloudWatch** (`awsemf`, namespace `cmind`, log group `/ecs/<prefix>/metrics`).
- **Logi** zostają na sterowniku `awslogs` jako compact JSON; CloudWatch Logs Insights automatycznie
  odkoduje JSON fields, więc `filter` / `stats` na `trace_id`, `service.name`, `@l`, itp.

Rola zadania (`aws_iam_role.task`) niesie `AWSXRayDaemonWriteAccess` + `CloudWatchAgentServerPolicy`.

Kwerenduj logi w CloudWatch Logs Insights:

```
fields @timestamp, @l, @m, trace_id, `service.name`
| filter `service.name` = "cmind-web"
| sort @timestamp desc
```

## Endpoints health (używane również przez probki)

| Endpoint | Usługa | Znaczenie |
|----------|--------|----------|
| `/alive` | Web | Liveness — tylko proces. |
| `/health` | Web | Readiness — obejmuje check bazy danych. |
| `/version` | Web, MCP | Wersja produktu i protokołu (MCP liveness/readiness). |

Zamapowane w **wszystkich** środowiskach (poprzednio tylko Dev), więc probki Kubernetes i cloud działają
w produkcji.
