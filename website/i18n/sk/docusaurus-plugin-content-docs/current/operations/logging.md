---
description: "Všetky tri služby (Web, MCP, CtraderCliNode) log cez Serilog ako kompaktný JSON na stdout — kontajner runtime a log zbierače (Loki, ELK, CloudWatch…"
---

# Logging & pozorovateľnosti

Všetky tri služby (Web, MCP, CtraderCliNode) log cez **Serilog** ako **kompaktný JSON na stdout** —
kontajner runtimes a log zbierače (Loki, ELK, CloudWatch, Azure Log Analytics, Datadog) prijímať
štruktúrované udalosti priamo, žádny free-text parsing.

## Pipeline

- **Sink 1 — Console (kompaktný JSON).** `RenderedCompactJsonFormatter`; každá udalosť niesol úplne
  OpenTelemetry zdroj identita — `service.name` (`cmind-web` / `cmind-mcp` / `cmind-node-agent` /
  `cmind-copy-agent`), `service.version`, `service.namespace` (`cmind`), `deployment.environment` —
  plus `trace_id` / `span_id` z obklopujúce `Activity` (`ActivityEnricher`) a `LogContext` rozsahy.
  Trace ids nechať CloudWatch Logs Insights a Azure Log Analytics pivot log↔trace **dokonca bez
  kolektor nasadený**.
- **Sink 2 — OTLP (voliteľne).** Keď `OTEL_EXPORTER_OTLP_ENDPOINT` nastavený, logy tiež export cez OTLP
  s rovnakými zdroj atribúty, spolu OpenTelemetry **metriky** a **stopy** (ASP.NET Core,
  HttpClient, runtime inštrumentácia) z `AddAppTelemetry`.
- **Sink 3 — Azure Monitor (voliteľne).** Keď `APPLICATIONINSIGHTS_CONNECTION_STRING` nastavený, stopy
  a metriky export **natívne** na Application Insights (`AddAzureMonitorTraceExporter` /
  `AddAzureMonitorMetricExporter`) — žádny kolektor. Vidieť cloud-native sekcia nižšie.
- **Source-generated správy.** App logy používajú silne-typovaný `LogMessages` rozšírenia
  (`Core/Logging/LogMessages.cs`) so stabilným `EventId`s — nikdy ad-hoc `ILogger.LogInformation`.
- **Request logging.** `UseSerilogRequestLogging()` emituje jeden štruktúrovaný súhrn na HTTP požiadavka
  (metódu, cestu, stav, čas ms).

## Konfigurácia

Úrovne nastaviteľný za službu cez `Serilog` sekcia v `appsettings.json` (čítať cez
`ReadFrom.Configuration`), napr.:

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

Override za runtime s env vars, napr. `Serilog__MinimumLevel__Default=Debug`.

## Dodávka na a kolektor

Nastaviť jeden env var za službu, bod na OTLP koncový bod:

```
OTEL_EXPORTER_OTLP_ENDPOINT=http://otel-collector:4317
```

- Helm: `--set observability.otlpEndpoint=http://otel-collector:4317`.
- Compose / cloud: pridať env var na každý službu.

Z kolektor, fan out na backend (Tempo/Jaeger stopy, Prometheus metriky, Loki logy) s
trace↔log korelácia intaktný.

## Cloud-native backendy (žádny extra kolektor)

Oba spravovaný nasadenia zapojené na natívne pozorovateľnosti stack z krabice — žádny OTLP kolektor potrebný.

### Azure — Application Insights + Log Analytics

`deploy/azure/main.bicep` zriaďuje **workspace-based Application Insights** komponent, prechádza Its
connection string na Web a MCP ako `APPLICATIONINSIGHTS_CONNECTION_STRING`. Výsledok:

- **Stopy + metriky** tok natívne do App Insights (Application Map, live metriky, end-to-end
  transakcia vyhľadávanie), korelácia podľa `trace_id`.
- **Logy** (kompaktný JSON na stdout) pristaň v **rovnaký Log Analytics workspace** cez Container Apps
  `appLogsConfiguration`, takže `AppTraces` / `ContainerAppConsoleLogs_CL` joinovať na trace id.
- Nastaviť voliteľný `otlpEndpoint` Bicep param na *tiež* fan out na kolektor.

Logy dopyt v Log Analytics (JSON linku v `Log_s`):

```kusto
ContainerAppConsoleLogs_CL
| extend log = parse_json(Log_s)
| where log.["service.name"] == "cmind-web"
| project TimeGenerated, level = log["@l"], msg = log["@m"], trace_id = log.trace_id
```

### AWS — X-Ray + CloudWatch (ADOT sidecar)

`deploy/aws/main.tf` beží **AWS Distro pre OpenTelemetry (ADOT) kolektor ako sidecar** v každý
Fargate úlohu. App exportuje OTLP na `http://localhost:4317`; sidecar dodáva:

- **stopy → AWS X-Ray** (`awsxray` exporter),
- **metriky → CloudWatch** (`awsemf`, namespace `cmind`, log skupina `/ecs/<prefix>/metrics`).
- **Logy** ostať na `awslogs` ovládač ako kompaktný JSON; CloudWatch Logs Insights auto-objavuje JSON
  polia, takže vy `filter` / `stats` na `trace_id`, `service.name`, `@l`, atď.

Úloha rola (`aws_iam_role.task`) niesol `AWSXRayDaemonWriteAccess` + `CloudWatchAgentServerPolicy`.

Logy dopyt v CloudWatch Logs Insights:

```
fields @timestamp, @l, @m, trace_id, `service.name`
| filter `service.name` = "cmind-web"
| sort @timestamp desc
```

## Health koncové body (tiež používaný sondy)

| Koncový bod | Služba | Zmysel |
|----------|---------|---------|
| `/alive` | Web | Liveness — proces iba. |
| `/health` | Web | Readiness — zahŕňa databáz kontrolu. |
| `/version` | Web, MCP | Produkt + protokol verzia (MCP liveness/readiness). |

Mapované v **všetky** prostredie (predtým Dev-only) takže Kubernetes a cloud sondy práca v
výroby.
