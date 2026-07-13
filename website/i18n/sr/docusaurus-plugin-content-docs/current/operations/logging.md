---
description: "Све три услуге (Web, MCP, CtraderCliNode) дневник преко Serilog као компактна JSON на stdout — контејнер времена извршавања и дневник колектори (Loki, ELK, CloudWatch…"
---

# Логирање & посматрање

Све три услуге (Web, MCP, CtraderCliNode) дневник преко **Serilog** као **компактна JSON на stdout** —
контејнер времена извршавања и дневник колектори (Loki, ELK, CloudWatch, Azure Log Analytics, Datadog) унесе
структурирано догађаје директно, нема слободно-текст обраду.

## Цевовод

- **Потопљивање 1 — Конзола (компактна JSON).** `RenderedCompactJsonFormatter`; сваки ferdinand носи потпуна
  OpenTelemetry ресурса идентичност — `service.name` (`cmind-web` / `cmind-mcp` / `cmind-node-agent` /
  `cmind-copy-agent`), `service.version`, `service.namespace` (`cmind`), `deployment.environment` —
  плус `trace_id` / `span_id` од опхода `Activity` (`ActivityEnricher`) и `LogContext` опсега.
  Трагови ID-е дозволи CloudWatch Logs Insights и Azure Log Analytics непокретност дневник↔трагови **чак без
  колектор развоја**.
- **Потопљивање 2 — OTLP (опционално).** Када `OTEL_EXPORTER_OTLP_ENDPOINT` поставити, дневници такође извоз преко OTLP
  са исти ресурса атрибути, поред OpenTelemetry **метрике** и **трагови** (ASP.NET Core,
  HttpClient, рантајм инструментирање) од `AddAppTelemetry`.
- **Потопљивање 3 — Azure Monitor (опционално).** Када `APPLICATIONINSIGHTS_CONNECTION_STRING` поставити, трагови
  и метрике извоз **домаће**於Application Insights (`AddAzureMonitorTraceExporter` /
  `AddAzureMonitorMetricExporter`) — нема колектор. Видити облак-домаће сечење доле.
- **Извор-генерисано поруке.** Апп дневници користе јако-типизирано `LogMessages` екстензије
  (`Core/Logging/LogMessages.cs`) са стабилна `EventId`-e — никад ад-хок `ILogger.LogInformation`.
- **Захтева логирање.** `UseSerilogRequestLogging()` емитирања једна структурирано резиме по HTTP захтева
  (метода, пута, статус, протекао ms).

## Конфигурација

Нивоа подешавања по услугу преко `Serilog` сечење у `appsettings.json` (читати кроз
`ReadFrom.Configuration`), нпр.:

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

Преписи у време извршавања са окружење променљивих, нпр. `Serilog__MinimumLevel__Default=Debug`.

## Отправљање колектору

Поставити једна окружење променљиво по услугу, упутите на OTLP крајњу тачку:

```
OTEL_EXPORTER_OTLP_ENDPOINT=http://otel-collector:4317
```

- Helm: `--set observability.otlpEndpoint=http://otel-collector:4317`.
- Compose / облак: додај окружење променљиво у сваку услугу.

Од колектор, вентилатор резултат позадина (Tempo/Jaeger трагови, Prometheus метрике, Loki дневници) са
трагови↔дневник корелација интактна.

## Облак-домаће позадина (не додатно колектор)

Обе управљана развоја жица за домаће посматрање стек од кутије — не OTLP колектор потребна.

### Azure — Application Insights + Log Analytics

`deploy/azure/main.bicep` провизионирају **радни простор-базирано Application Insights** компонента, прослеђивање њена
везни низ у Web и MCP као `APPLICATIONINSIGHTS_CONNECTION_STRING`. Резултат:

- **Трагови + метрике** проток домаће у App Insights (Application Map, живо метрике, крај-до-крај
  трансакција поиск), корелиран од `trace_id`.
- **Дневници** (компактна JSON на stdout) земља у **исти Log Analytics радни простор** преко Container Apps
  `appLogsConfiguration`, тако `AppTraces` / `ContainerAppConsoleLogs_CL` спајај на трагови ID.
- Поставити опционално `otlpEndpoint` Bicep параметар до *такође* вентилатор резултат колектору.

Упит дневници у Log Analytics (JSON линију у `Log_s`):

```kusto
ContainerAppConsoleLogs_CL
| extend log = parse_json(Log_s)
| where log.["service.name"] == "cmind-web"
| project TimeGenerated, level = log["@l"], msg = log["@m"], trace_id = log.trace_id
```

### AWS — X-Ray + CloudWatch (ADOT боковни)

`deploy/aws/main.tf` трче **AWS Distro за OpenTelemetry (ADOT) колектор као боковни** у сваки
Fargate задатак. Апп извоз OTLP до `http://localhost:4317`; боковни кораби:

- **трагови → AWS X-Ray** (`awsxray` извоз),
- **метрике → CloudWatch** (`awsemf`, простор имена `cmind`, дневна група `/ecs/<prefix>/metrics`).
- **Дневници** остану на `awslogs` возач као компактна JSON; CloudWatch Logs Insights аутоматско-открива JSON
  поља, тако могу `filter` / `stats` на `trace_id`, `service.name`, `@l`, итд.

Задатак улога (`aws_iam_role.task`) носи `AWSXRayDaemonWriteAccess` + `CloudWatchAgentServerPolicy`.

Упит дневници у CloudWatch Logs Insights:

```
fields @timestamp, @message, @l, service.name, trace_id
| filter service.name = "cmind-web"
| sort @timestamp desc
| limit 100
```
