---
title: Beleženje in opazovanje
description: "Vse tri storitve (Web, MCP, CtraderCliNode) beležijo prek Serilog kot kompakten JSON na stdout — container runtimes in zbiralci dnevnikov (Loki, ELK, CloudWatch…)…"
---

# Beleženje in opazovanje

Vse tri storitve (Web, MCP, CtraderCliNode) beležijo prek **Serilog** kot **kompakten JSON na stdout** —
container runtimes in zbiralci dnevnikov (Loki, ELK, CloudWatch, Azure Log Analytics, Datadog) zaužijejo
strukturirane dogodke naravnost, brez brezplačnega razčlenjevanja besedila.

## Cevovod

- **Ponor 1 — Console (kompakten JSON).** `RenderedCompactJsonFormatter`; vsak dogodek nosi polno
  OpenTelemetry identiteto vira — `service.name` (`cmind-web` / `cmind-mcp` / `cmind-node-agent` /
  `cmind-copy-agent`), `service.version`, `service.namespace` (`cmind`), `deployment.environment` —
  plus `trace_id` / `span_id` iz ambient `Activity` (`ActivityEnricher`) in `LogContext` obsegi.
  Trace ids dovoljujejo CloudWatch Logs Insights in Azure Log Analytics pivotirati dnevnik↔trace **celo brez
  nameščenega zbiralca**.
- **Ponor 2 — OTLP (izbirno).** Ko je `OTEL_EXPORTER_OTLP_ENDPOINT` nastavljena, dnevniki prav tako izvozijo prek OTLP
  z istimi atributi vira, poleg OpenTelemetry **metrik** in **sledi** (ASP.NET Core,
  HttpClient, runtime instrumentacija) iz `AddAppTelemetry`.
- **Ponor 3 — Azure Monitor (izbirno).** Ko je `APPLICATIONINSIGHTS_CONNECTION_STRING` nastavljena, sledi
  in metrike izvozijo **naravno** v Application Insights (`AddAzureMonitorTraceExporter` /
  `AddAzureMonitorMetricExporter`) — brez zbiralca. Glej cloud-native odsek spodaj.
- **Viri-generirana sporočila.** App dnevniki uporabljajo močno-tipizirane `LogMessages` razširitve
  (`Core/Logging/LogMessages.cs`) s stabilnimi `EventId` — nikoli ad-hoc `ILogger.LogInformation`.
- **Beleženje zahtev.** `UseSerilogRequestLogging()` oddaja en strukturiran povzetek na HTTP zahtevo
  (metoda, pot, status, porabljen ms).

## Konfiguracija

Ravni prilagodljive na storitev prek `Serilog` odseka v `appsettings.json` (brano skozi
`ReadFrom.Configuration`), npr.:

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

Prepišite v runtime s spremenljivkami okolja, npr. `Serilog__MinimumLevel__Default=Debug`.

## Pošiljanje zbiralcu

Nastavite eno spremenljivko okolja na storitev, kažite na OTLP končno točko:

```
OTEL_EXPORTER_OTLP_ENDPOINT=http://otel-collector:4317
```

- Helm: `--set observability.otlpEndpoint=http://otel-collector:4317`.
- Compose / cloud: dodajte spremenljivko okolja vsaki storitvi.

Iz zbiralca, fan out v backend (Tempo/Jaeger sledi, Prometheus metrike, Loki dnevniki) z
trace↔log korelacijo nedotaknjeno.

## Cloud-native backends (brez extra zbiralca)

Obe upravljani namestitvi sta ožičeni za native observability stack iz škatle — brez OTLP zbiralca potrebnega.

### Azure — Application Insights + Log Analytics

`deploy/azure/main.bicep` določa **workspace-based Application Insights** komponento, posreduje njeno
connection string Web in MCP kot `APPLICATIONINSIGHTS_CONNECTION_STRING`. Rezultat:

- **Sledi + metrike** tečejo naravno v App Insights (Application Map, live metrike, end-to-end
  transakcijsko iskanje), korelirane z `trace_id`.
- **Dnevniki** (kompakten JSON na stdout) pristanejo v **isti Log Analytics workspace** prek Container Apps
  `appLogsConfiguration`, torej `AppTraces` / `ContainerAppConsoleLogs_CL` se pridružita na trace id.
- Nastavite izbirni `otlpEndpoint` Bicep parameter da **prav tako** fan out v zbiralec.

Poizvedujte dnevnike v Log Analytics (JSON vrstica v `Log_s`):

```kusto
ContainerAppConsoleLogs_CL
| extend log = parse_json(Log_s)
| where log.["service.name"] == "cmind-web"
| project TimeGenerated, level = log["@l"], msg = log["@m"], trace_id = log.trace_id
```

### AWS — X-Ray + CloudWatch (ADOT sidecar)

`deploy/aws/main.tf` poganja **AWS Distro za OpenTelemetry (ADOT) collector kot sidecar** v vsaki
Fargate nalogi. App izvozi OTLP v `http://localhost:4317`; sidecar ladi:

- **sledi → AWS X-Ray** (`awsxray` exporter),
- **metrike → CloudWatch** (`awsemf`, namespace `cmind`, log group `/ecs/<prefix>/metrics`).
- **Dnevniki** ostanejo na `awslogs` gonilniku kot kompakten JSON; CloudWatch Logs Insights avto-odkrije JSON
  polja, torej `filter` / `stats` na `trace_id`, `service.name`, `@l`, itd.

Nalogova vloga (`aws_iam_role.task`) nosi `AWSXRayDaemonWriteAccess` + `CloudWatchAgentServerPolicy`.

Poizveduj dnevnike v CloudWatch Logs Insights:

```
fields @timestamp, @l, @m, trace_id, `service.name`
| filter `service.name` = "cmind-web"
| sort @timestamp desc
```

## Končne točke zdravja (prav tako uporabljane s sondami)

| Končna točka | Storitev | Pomen |
|-------------|---------|-------|
| `/alive` | Web | Liveness — samo proces. |
| `/health` | Web | Readiness — vključuje preverjanje zbirke podatkov. |
| `/version` | Web, MCP | Verzija izdelka in protokola (MCP liveness/readiness). |

Preslikane v **vseh** okoljih (prej samo Dev) torej Kubernetes in cloud probe delujejo v
produkciji.
