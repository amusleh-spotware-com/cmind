---
description: "Todos os três serviços (Web, MCP, CtraderCliNode) fazem log via Serilog como JSON compacto no stdout — runtimes de container e coletores de log (Loki, ELK, CloudWatch, Azure Log Analytics, Datadog) ingerem eventos estruturados diretamente, sem parsing de texto livre."
---

# Logging e observabilidade

Todos os três serviços (Web, MCP, CtraderCliNode) fazem log via **Serilog** como **JSON compacto no stdout** —
runtimes de container e coletores de log (Loki, ELK, CloudWatch, Azure Log Analytics, Datadog) ingerem
eventos estruturados diretamente, sem parsing de texto livre.

## Pipeline

- **Sink 1 — Console (JSON compacto).** `RenderedCompactJsonFormatter`; cada evento carrega
  identidade de recurso OpenTelemetry completa — `service.name` (`cmind-web` / `cmind-mcp` / `cmind-node-agent` /
  `cmind-copy-agent`), `service.version`, `service.namespace` (`cmind`), `deployment.environment` —
  mais `trace_id` / `span_id` do `Activity` ambiente (`ActivityEnricher`) e escopos `LogContext`.
  IDs de trace permitem que CloudWatch Logs Insights e Azure Log Analytics façam pivot log↔trace **mesmo sem
  collector implantado**.
- **Sink 2 — OTLP (opcional).** Quando `OTEL_EXPORTER_OTLP_ENDPOINT` definido, logs também exportam via OTLP
  com os mesmos atributos de recurso, junto com **métricas** e **traces** OpenTelemetry (ASP.NET Core,
  HttpClient, instrumentação de runtime) de `AddAppTelemetry`.
- **Sink 3 — Azure Monitor (opcional).** Quando `APPLICATIONINSIGHTS_CONNECTION_STRING` definido, traces
  e métricas exportam **nativamente** para Application Insights (`AddAzureMonitorTraceExporter` /
  `AddAzureMonitorMetricExporter`) — sem collector. Veja a seção cloud-native abaixo.
- **Mensagens geradas por fonte.** Logs do app usam `LogMessages` extensions
  (`Core/Logging/LogMessages.cs`) com `EventId` estáveis — nunca `ILogger.LogInformation` ad-hoc.
- **Request logging.** `UseSerilogRequestLogging()` emite um resumo estruturado por requisição HTTP
  (método, path, status, ms decorridos).

## Configuração

Níveis ajustáveis por serviço via seção `Serilog` em `appsettings.json` (lido através de
`ReadFrom.Configuration`), ex.:

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

Override em runtime com variáveis de ambiente, ex. `Serilog__MinimumLevel__Default=Debug`.

## Enviando para um collector

Defina uma env var por serviço, apontando para o endpoint OTLP:

```
OTEL_EXPORTER_OTLP_ENDPOINT=http://otel-collector:4317
```

- Helm: `--set observability.otlpEndpoint=http://otel-collector:4317`.
- Compose / cloud: adicione env var a cada serviço.

Do collector, distribua para backend (Tempo/Jaeger traces, Prometheus metrics, Loki logs) com
correlação trace↔log intacta.

## Backends cloud-native (sem collector extra)

Ambos os deployments gerenciados são wired para stack de observabilidade native out of box — sem collector OTLP necessário.

### Azure — Application Insights + Log Analytics

`deploy/azure/main.bicep` provê componente Application Insights **workspace-based**, passa sua
connection string para Web e MCP como `APPLICATIONINSIGHTS_CONNECTION_STRING`. Resultado:

- **Traces + métricas** fluem nativamente para App Insights (Application Map, métricas live, busca de transações end-to-end), correlacionados por `trace_id`.
- **Logs** (JSON compacto no stdout) pousam no **mesmo Log Analytics workspace** via Container Apps
  `appLogsConfiguration`, então `AppTraces` / `ContainerAppConsoleLogs_CL` join on trace id.
- Defina param Bicep `otlpEndpoint` opcional para **também** distribuir para um collector.

Query logs no Log Analytics (linha JSON em `Log_s`):

```kusto
ContainerAppConsoleLogs_CL
| extend log = parse_json(Log_s)
| where log.["service.name"] == "cmind-web"
| project TimeGenerated, level = log["@l"], msg = log["@m"], trace_id = log.trace_id
```

### AWS — X-Ray + CloudWatch (sidecar ADOT)

`deploy/aws/main.tf` executa **AWS Distro for OpenTelemetry (ADOT) collector como sidecar** em cada
task Fargate. App exporta OTLP para `http://localhost:4317`; sidecar envia:

- **traces → AWS X-Ray** (`awsxray` exporter),
- **métricas → CloudWatch** (`awsemf`, namespace `cmind`, log group `/ecs/<prefix>/metrics`).
- **Logs** ficam no driver `awslogs` como JSON compacto; CloudWatch Logs Insights auto-descobre campos JSON,
  então você pode `filter` / `stats` em `trace_id`, `service.name`, `@l`, etc.

Task role (`aws_iam_role.task`) carrega `AWSXRayDaemonWriteAccess` + `CloudWatchAgentServerPolicy`.

Query logs no CloudWatch Logs Insights:

```
fields @timestamp, @l, @m, trace_id, `service.name`
| filter `service.name` = "cmind-web"
| sort @timestamp desc
```

## Endpoints de health (também usados por probes)

| Endpoint | Serviço | Significado |
|----------|---------|-------------|
| `/alive` | Web | Liveness — apenas processo. |
| `/health` | Web | Readiness — inclui checagem de banco de dados. |
| `/version` | Web, MCP | Versão do produto + protocolo (liveness/readiness MCP). |

Mapeados em **todos** os ambientes (anteriormente apenas Dev) para que probes Kubernetes e cloud funcionem em
produção.
