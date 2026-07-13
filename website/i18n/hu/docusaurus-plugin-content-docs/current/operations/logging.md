---
title: Naplozas es observabilitas
description: "Mindharom szolgalat (Web, MCP, CtraderCliNode) Serilog-gal JSON-ként naploz a stdout-ra - kontener runtimes es log gyujtok (Loki, ELK, CloudWatch...) strukturált esemenyeket dolgoznak fel, nincs szabad-szoveges parsing."
---

# Naplozas es observabilitas

Mindharom szolgalat (Web, MCP, CtraderCliNode) **Serilog**-gal **compact JSON-ként a stdout-ra** naploz - kontener runtimes és log gyűjtők (Loki, ELK, CloudWatch, Azure Log Analytics, Datadog) közvetlenül dolgozzák fel a strukturált eseményeket, nincs szabad-szöveges parsing.

## Pipeline

- **Sink 1 - Console (compact JSON).** `RenderedCompactJsonFormatter`; minden esemény teljes OpenTelemetry erőforrás identitást hordoz - `service.name` (`cmind-web` / `cmind-mcp` / `cmind-node-agent` / `cmind-copy-agent`), `service.version`, `service.namespace` (`cmind`), `deployment.environment` - plusz `trace_id` / `span_id` az ambient `Activity`-ból (`ActivityEnricher`) és a `LogContext` scopeokból. A trace id-k lehetővé teszik a CloudWatch Logs Insights és az Azure Log Analytics számára a log↔trace pivot-t **még telepített collector nélkül is**.
- **Sink 2 - OTLP (opcionális).** Amikor az `OTEL_EXPORTER_OTLP_ENDPOINT` be van állítva, a log-ok OTLP-n is exportálódnak ugyanazokkal az erőforrás attribútumokkal, az OpenTelemetry **metrikák** és **traces** mellett (`AddAppTelemetry`) az ASP.NET Core, HttpClient, runtime instrumentációból.
- **Sink 3 - Azure Monitor (opcionális).** Amikor az `APPLICATIONINSIGHTS_CONNECTION_STRING` be van állítva, a traces és metrikák **natívan** az Application Insights-ba exportálódnak (`AddAzureMonitorTraceExporter` / `AddAzureMonitorMetricExporter`) - nincs collector. Lásd alább.
- **Forrás által generált üzenetek.** Az alkalmazás log-ai erősen tipizált `LogMessages` kiterjesztéseket használnak (`Core/Logging/LogMessages.cs`) stabil `EventId`-kkel - soha nem ad-hoc `ILogger.LogInformation`.
- **Kérés naplózás.** `UseSerilogRequestLogging()` egy strukturált összefoglalót bocsát ki minden HTTP kérésről (metódus, útvonal, státusz, eltelt ms).

## Konfiguráció

A szintek hangolhatók per szolgáltatás az `appsettings.json` `Serilog` szekciójában keresztül (a `ReadFrom.Configuration`-on keresztül olvassa), pl.:

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

Felülbírálás futásidőben környezeti változókkal, pl. `Serilog__MinimumLevel__Default=Debug`.

## Szállítás collector-nak

Állítsd be egy env var-t per szolgáltatás, irányítsd az OTLP végpontra:

```
OTEL_EXPORTER_OTLP_ENDPOINT=http://otel-collector:4317
```

- Helm: `--set observability.otlpEndpoint=http://otel-collector:4317`.
- Compose / felhő: add env var-t minden szolgáltatáshoz.

A collector-ból, oszd ki a backend-re (Tempo/Jaeger traces, Prometheus metrikák, Loki log-ok) a trace↔log korrelációval együtt.

## Felhő-natív backend-ek (nincs extra collector)

Mindkét managed deployment natív observability stack-re van huzalozva a dobozból - nincs OTLP collector kell.

### Azure - Application Insights + Log Analytics

`deploy/azure/main.bicep` provisioningolja a **workspace-based Application Insights** komponenst, átadja a kapcsolati karakterláncot a Web-nek és az MCP-nek `APPLICATIONINSIGHTS_CONNECTION_STRING`-ként. Eredmény:

- **Traces + metrikák** natívan az App Insights-ba áramlanak (Application Map, live metrics, end-to-end transaction search), `trace_id` alapján korrelálva.
- **Log-ok** (compact JSON stdout-on) landolnak a **Log Analytics workspace-ben** ugyanabban a munkaterületben a Container Apps `appLogsConfiguration` révén, szóval az `AppTraces` / `ContainerAppConsoleLogs_CL` összekapcsolható trace id alapján.
- Opcionális `otlpEndpoint` Bicep paraméter, hogy egy collector-ra is szétoszd.

Log-ok a Log Analytics-ben (JSON sor a `Log_s`-ben):

```kusto
ContainerAppConsoleLogs_CL
| extend log = parse_json(Log_s)
| where log["service.name"] == "cmind-web"
| project TimeGenerated, level = log["@l"], msg = log["@m"], trace_id = log.trace_id
```

### AWS - X-Ray + CloudWatch (ADOT sidecar)

`deploy/aws/main.tf` az **AWS Distro for OpenTelemetry (ADOT) collector**-t sidecar-ként futtatja minden Fargate task-ban. Az alkalmazás OTLP-t exportál a `http://localhost:4317`-re; az ADOT sidecar szétosztja:

- **traces → AWS X-Ray** (`awsxray` exporter),
- **metrics → CloudWatch** (`awsemf`, namespace `cmind`, log group `/ecs/<prefix>/metrics`).
- **Log-ok** az `awslogs` driveren maradnak compact JSON-ként; a CloudWatch Logs Insights automatikusan felfedezi a JSON mezőket, szóval `filter` / `stats` parancsokat futtathatsz `trace_id`, `service.name`, `@l`, stb. mezőkön.

A task IAM szerepköre (`aws_iam_role.task`) hordozza az `AWSXRayDaemonWriteAccess` + `CloudWatchAgentServerPolicy` jogosultságokat.

Query log-okat a CloudWatch Logs Insights-ban:

```
fields @timestamp, @l, @m, trace_id, `service.name`
| filter `service.name` = "cmind-web"
| sort @timestamp desc
```

## Health végpontok (amelyeket a probe-ok is használnak)

| Végpont | Szolgáltatás | Jelentés |
|---------|---------|---------|
| `/alive` | Web | Liveness - csak a folyamat. |
| `/health` | Web | Readiness - tartalmazza az adatbázis ellenőrzést. |
| `/version` | Web, MCP | Termék + protokoll verzió (MCP liveness/readiness). |

Minden környezetben leképezve (korábban csak Dev), szóval a Kubernetes és felhő probe-ok produkcióban működnek.
