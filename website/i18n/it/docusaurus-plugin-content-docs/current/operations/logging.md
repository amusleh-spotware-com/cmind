---
description: "Tutti e tre i servizi (Web, MCP, CtraderCliNode) registrano tramite Serilog come JSON compatto su stdout — i runtime dei container e i collezionisti di log (Loki, ELK, CloudWatch..."
---

# Logging e osservabilità

Tutti e tre i servizi (Web, MCP, CtraderCliNode) registrano tramite **Serilog** come **JSON compatto su stdout** — i runtime dei container e i collezionisti di log (Loki, ELK, CloudWatch, Azure Log Analytics, Datadog) ingeriscono direttamente gli eventi strutturati, nessun parsing di testo libero.

## Pipeline

- **Sink 1 — Console (JSON compatto).** `RenderedCompactJsonFormatter`; ogni evento porta l'identità completa della risorsa OpenTelemetry — `service.name` (`cmind-web` / `cmind-mcp` / `cmind-node-agent` / `cmind-copy-agent`), `service.version`, `service.namespace` (`cmind`), `deployment.environment` — più `trace_id` / `span_id` da `Activity` ambiente (`ActivityEnricher`) e scope `LogContext`. Gli ID di traccia consentono a CloudWatch Logs Insights e Azure Log Analytics di eseguire il pivot log↔trace **anche senza collezionista distribuito**.
- **Sink 2 — OTLP (opzionale).** Quando `OTEL_EXPORTER_OTLP_ENDPOINT` è impostato, i log vengono anche esportati su OTLP con gli stessi attributi di risorsa, insieme a **metriche** e **tracce** OpenTelemetry (ASP.NET Core, HttpClient, instrumentazione runtime) da `AddAppTelemetry`.
- **Sink 3 — Azure Monitor (opzionale).** Quando `APPLICATIONINSIGHTS_CONNECTION_STRING` è impostato, le tracce e le metriche vengono esportate **nativamente** ad Application Insights (`AddAzureMonitorTraceExporter` / `AddAzureMonitorMetricExporter`) — nessun collezionista. Vedi la sezione cloud-native qui sotto.
- **Messaggi generati da origine.** L'app registra usando estensioni `LogMessages` fortemente tipizzate (`Core/Logging/LogMessages.cs`) con `EventId`s stabili — mai ad-hoc `ILogger.LogInformation`.
- **Logging delle richieste.** `UseSerilogRequestLogging()` emette un riepilogo strutturato per richiesta HTTP (metodo, percorso, stato, ms trascorsi).

## Configurazione

Livelli sintonizzabili per servizio tramite la sezione `Serilog` in `appsettings.json` (leggi attraverso `ReadFrom.Configuration`), ad es.:

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

Sostituisci in runtime con variabili di ambiente, ad es. `Serilog__MinimumLevel__Default=Debug`.

## Spedire a un collezionista

Imposta una variabile di ambiente per servizio, punta all'endpoint OTLP:

```
OTEL_EXPORTER_OTLP_ENDPOINT=http://otel-collector:4317
```

- Helm: `--set observability.otlpEndpoint=http://otel-collector:4317`.
- Compose / cloud: aggiungi variabile di ambiente a ogni servizio.

Dal collezionista, distribuisci al backend (tracce Tempo/Jaeger, metriche Prometheus, log Loki) con correlazione traccia↔log intatta.

## Backend nativi del cloud (nessun collezionista aggiuntivo)

Entrambe le distribuzioni gestite cablate per lo stack di osservabilità nativo subito — nessun collezionista OTLP necessario.

### Azure — Application Insights + Log Analytics

`deploy/azure/main.bicep` disposizioni **componente Application Insights basato su workspace**, passa la sua stringa di connessione a Web e MCP come `APPLICATIONINSIGHTS_CONNECTION_STRING`. Risultato:

- **Tracce + metriche** fluiscono nativamente in App Insights (Application Map, metriche live, ricerca transazionale end-to-end), correlate da `trace_id`.
- **Log** (JSON compatto su stdout) sbarcano nello **stesso workspace Log Analytics** tramite Container Apps `appLogsConfiguration`, quindi `AppTraces` / `ContainerAppConsoleLogs_CL` si uniscono su trace id.
- Imposta il parametro opzionale Bicep `otlpEndpoint` per **anche** distribuire a un collezionista.

Query log in Log Analytics (linea JSON in `Log_s`):

```kusto
ContainerAppConsoleLogs_CL
| extend log = parse_json(Log_s)
| where log.["service.name"] == "cmind-web"
| project TimeGenerated, level = log["@l"], msg = log["@m"], trace_id = log.trace_id
```

### AWS — X-Ray + CloudWatch (sidecar ADOT)

`deploy/aws/main.tf` esegue **AWS Distro for OpenTelemetry (ADOT) collezionista come sidecar** in ogni compito Fargate. L'app esporta OTLP a `http://localhost:4317`; sidecar spedisce:

- **tracce → AWS X-Ray** (esportatore `awsxray`),
- **metriche → CloudWatch** (`awsemf`, namespace `cmind`, log group `/ecs/<prefix>/metrics`).
- **Log** rimangono sul driver `awslogs` come JSON compatto; CloudWatch Logs Insights scopre automaticamente i campi JSON, quindi `filter` / `stats` su `trace_id`, `service.name`, `@l`, ecc.

Il ruolo dell'attività (`aws_iam_role.task`) porta `AWSXRayDaemonWriteAccess` + `CloudWatchAgentServerPolicy`.

Query log in CloudWatch Logs Insights:

```
fields @timestamp, @l, @m, trace_id, `service.name`
| filter `service.name` = "cmind-web"
| sort @timestamp desc
```

## Endpoint di salute (utilizzati anche dai probe)

| Endpoint | Servizio | Significato |
|----------|---------|---------|
| `/alive` | Web | Liveness — solo il processo. |
| `/health` | Web | Readiness — include il controllo del database. |
| `/version` | Web, MCP | Versione del prodotto + protocollo (liveness/readiness MCP). |

Mappato in **tutti** gli ambienti (precedentemente solo Dev) quindi i probe Kubernetes e cloud funzionano in produzione.
