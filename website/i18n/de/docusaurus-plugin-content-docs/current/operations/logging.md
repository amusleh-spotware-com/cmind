---
description: "Alle drei Services (Web, MCP, CtraderCliNode) loggen via Serilog als kompaktes JSON auf stdout — Container-Runtimes und Log-Collector (Loki, ELK, CloudWatch…"
---

# Logging & Beobachtbarkeit

Alle drei Services (Web, MCP, CtraderCliNode) loggen via **Serilog** als **kompaktes JSON auf stdout** — Container-Runtimes und Log-Collector (Loki, ELK, CloudWatch, Azure Log Analytics, Datadog) nehmen strukturierte Events direkt auf, kein Free-Text-Parsing.

## Pipeline

- **Sink 1 — Console (kompaktes JSON).** `RenderedCompactJsonFormatter`; jedes Event trägt volle OpenTelemetry-Resource-Identität — `service.name` (`cmind-web` / `cmind-mcp` / `cmind-node-agent` / `cmind-copy-agent`), `service.version`, `service.namespace` (`cmind`), `deployment.environment` — plus `trace_id` / `span_id` aus ambient `Activity` (`ActivityEnricher`) und `LogContext`-Geltungsbereiche. Trace-IDs lassen CloudWatch Logs Insights und Azure Log Analytics Log↔Trace drehen **selbst ohne Collector eingesetzt**.
- **Sink 2 — OTLP (optional).** Wenn `OTEL_EXPORTER_OTLP_ENDPOINT` gesetzt ist, exportieren auch Logs über OTLP mit gleichen Resource-Attributen, neben OpenTelemetry **Metriken** und **Traces** (ASP.NET Core, HttpClient, Runtime-Instrumentierung) aus `AddAppTelemetry`.
- **Sink 3 — Azure Monitor (optional).** Wenn `APPLICATIONINSIGHTS_CONNECTION_STRING` gesetzt ist, exportieren Traces und Metriken **nativ** zu Application Insights (`AddAzureMonitorTraceExporter` / `AddAzureMonitorMetricExporter`) — kein Collector. Siehe Cloud-Native-Abschnitt unten.
- **Source-Generated-Nachrichten.** App-Logs verwenden stark-typisierte `LogMessages`-Erweiterungen (`Core/Logging/LogMessages.cs`) mit stabilen `EventId`s — nie Ad-Hoc `ILogger.LogInformation`.
- **Request-Logging.** `UseSerilogRequestLogging()` sendet eine strukturierte Zusammenfassung pro HTTP-Anfrage (Methode, Pfad, Status, verstrichene ms).

## Konfiguration

Levels-Abstimmung pro Service via `Serilog`-Abschnitt in `appsettings.json` (lesen durch `ReadFrom.Configuration`), z. B.:

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

Override zur Laufzeit mit Umgebungs-Variablen, z. B. `Serilog__MinimumLevel__Default=Debug`.

## Versand an einen Collector

Setzen Sie eine Umgebungs-Variable pro Service, zeigen Sie auf OTLP-Endpunkt:

```
OTEL_EXPORTER_OTLP_ENDPOINT=http://otel-collector:4317
```

- Helm: `--set observability.otlpEndpoint=http://otel-collector:4317`.
- Compose / Cloud: Fügen Sie die Umgebungs-Variable zu jedem Service hinzu.

Vom Collector aus, Fan-Out zu Backend (Tempo/Jaeger Traces, Prometheus Metriken, Loki Logs) mit Trace↔Log-Korrelation intakt.

## Cloud-Native-Backends (kein extra Collector)

Beide verwalteten Bereitstellungen für native Observability-Stack aus der Box verdrahtet — kein OTLP-Collector benötigt.

### Azure — Application Insights + Log Analytics

`deploy/azure/main.bicep` bestellt **Workspace-basierte Application Insights**-Komponente, bestanden seine Verbindungs-String zu Web und MCP als `APPLICATIONINSIGHTS_CONNECTION_STRING`. Resultat:

- **Traces + Metriken** fließen nativ in App Insights (Application Map, Live-Metriken, End-to-End-Transaktions-Suche), korreliert durch `trace_id`.
- **Logs** (kompaktes JSON auf stdout) landen im **gleichen Log Analytics Workspace** via Container Apps `appLogsConfiguration`, sodass `AppTraces` / `ContainerAppConsoleLogs_CL` auf Trace-ID beitreten.
- Setzen Sie optionales `otlpEndpoint`-Bicep-Parameter zu *auch* Fan-Out zu einem Collector.

Query Logs in Log Analytics (JSON-Linie in `Log_s`):

```kusto
ContainerAppConsoleLogs_CL
| extend log = parse_json(Log_s)
| where log.["service.name"] == "cmind-web"
| project TimeGenerated, level = log["@l"], msg = log["@m"], trace_id = log.trace_id
```

### AWS — X-Ray + CloudWatch (ADOT Sidecar)

`deploy/aws/main.tf` läuft **AWS Distro für OpenTelemetry (ADOT)-Collector als Sidecar** in jeder Fargate-Aufgabe. App exportiert OTLP nach `http://localhost:4317`; Sidecar versendet:

- **Traces → AWS X-Ray** (`awsxray`-Exporter),
- **Metriken → CloudWatch** (`awsemf`, Namespace `cmind`, Log-Gruppe `/ecs/<prefix>/metrics`).
- **Logs** bleiben auf `awslogs`-Treiber als kompaktes JSON; CloudWatch Logs Insights Auto-Erkennung JSON-Felder, sodass Sie `filter` / `stats` auf `trace_id`, `service.name`, `@l`, etc.

Task-Rolle (`aws_iam_role.task`) trägt `AWSXRayDaemonWriteAccess` + `CloudWatchAgentServerPolicy`.

Query Logs in CloudWatch Logs Insights:

```
fields @timestamp, @l, @m, trace_id, `service.name`
| filter `service.name` = "cmind-web"
| sort @timestamp desc
```

## Health-Endpunkte (auch von Probes verwendet)

| Endpunkt | Service | Bedeutung |
|----------|---------|---------|
| `/alive` | Web | Liveness — nur Prozess. |
| `/health` | Web | Readiness — einschließlich der Datenbank-Überprüfung. |
| `/version` | Web, MCP | Produkt + Protokoll-Version (MCP Liveness/Readiness). |

Ordnet in **allen** Umgebungen (zuvor nur Dev) zu, sodass Kubernetes und Cloud-Probes in der Produktion funktionieren.
