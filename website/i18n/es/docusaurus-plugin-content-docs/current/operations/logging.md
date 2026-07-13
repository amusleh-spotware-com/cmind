---
description: "Los tres servicios (Web, MCP, CtraderCliNode) registran vía Serilog como JSON compacto en stdout — los tiempos de ejecución de contenedor y recopiladores de registros (Loki, ELK, CloudWatch…"
---

# Logging & observability

Los tres servicios (Web, MCP, CtraderCliNode) registran vía **Serilog** como **JSON compacto en stdout** — los tiempos de ejecución de contenedor y recopiladores de registros (Loki, ELK, CloudWatch, Azure Log Analytics, Datadog) ingieren eventos estructurados directamente, sin análisis de texto libre.

## Pipeline

- **Sink 1 — Consola (JSON compacto).** `RenderedCompactJsonFormatter`; cada evento lleva identidad de recurso OpenTelemetry completa — `service.name` (`cmind-web` / `cmind-mcp` / `cmind-node-agent` / `cmind-copy-agent`), `service.version`, `service.namespace` (`cmind`), `deployment.environment` — más `trace_id` / `span_id` de `Activity` ambiente (`ActivityEnricher`) y alcances de `LogContext`. Las ids de traza permiten que CloudWatch Logs Insights y Azure Log Analytics canalicen log↔trace **incluso sin recopilador desplegado**.
- **Sink 2 — OTLP (opcional).** Cuando `OTEL_EXPORTER_OTLP_ENDPOINT` se establece, los registros también se exportan sobre OTLP con los mismos atributos de recursos, junto con OpenTelemetry **métricas** y **trazas** (ASP.NET Core, HttpClient, instrumentación en tiempo de ejecución) desde `AddAppTelemetry`.
- **Sink 3 — Azure Monitor (opcional).** Cuando se establece `APPLICATIONINSIGHTS_CONNECTION_STRING`, las trazas y métricas se exportan **nativamente** a Application Insights (`AddAzureMonitorTraceExporter` / `AddAzureMonitorMetricExporter`) — sin recopilador. Ver sección cloud-native abajo.
- **Mensajes generados por fuente.** La app registra extensiones `LogMessages` fuertemente tipadas (`Core/Logging/LogMessages.cs`) con `EventId`s estables — nunca `ILogger.LogInformation` ad-hoc.
- **Registro de solicitudes.** `UseSerilogRequestLogging()` emite un resumen estructurado por solicitud HTTP (método, ruta, estado, ms transcurridos).

## Configuración

Niveles sintonizables por servicio vía sección `Serilog` en `appsettings.json` (lectura a través de `ReadFrom.Configuration`), ej.:

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

Anular en tiempo de ejecución con variables de env, ej. `Serilog__MinimumLevel__Default=Debug`.

## Envío a un recopilador

Establece una variable de env por servicio, apunta al endpoint OTLP:

```
OTEL_EXPORTER_OTLP_ENDPOINT=http://otel-collector:4317
```

- Helm: `--set observability.otlpEndpoint=http://otel-collector:4317`.
- Compose / nube: agregar variable env a cada servicio.

Desde el recopilador, distribución a backend (Tempo/Jaeger trazas, Prometheus métricas, Loki registros) con correlación trace↔log intacta.

## Backends cloud-native (sin recopilador extra)

Ambas despliegues gestionadas cableadas para stack de observabilidad nativa fuera de la caja — ningún recopilador OTLP necesario.

### Azure — Application Insights + Log Analytics

`deploy/azure/main.bicep` aprovisiona el componente **Application Insights basado en área de trabajo**, pasa su cadena de conexión a Web y MCP como `APPLICATIONINSIGHTS_CONNECTION_STRING`. Resultado:

- **Trazas + métricas** fluyen nativamente en App Insights (Mapa de aplicación, métricas en vivo, búsqueda de transacciones de extremo a extremo), correlacionadas por `trace_id`.
- **Registros** (JSON compacto en stdout) aterrizan en la **misma área de trabajo de Log Analytics** vía `appLogsConfiguration` de Container Apps, por lo que `AppTraces` / `ContainerAppConsoleLogs_CL` se unen en id de traza.
- Establece parámetro Bicep `otlpEndpoint` opcional para *también* distribuir a un recopilador.

Consulta registros en Log Analytics (línea JSON en `Log_s`):

```kusto
ContainerAppConsoleLogs_CL
| extend log = parse_json(Log_s)
| where log.["service.name"] == "cmind-web"
| project TimeGenerated, level = log["@l"], msg = log["@m"], trace_id = log.trace_id
```

### AWS — X-Ray + CloudWatch (sidecar ADOT)

`deploy/aws/main.tf` ejecuta **AWS Distro para OpenTelemetry (ADOT) recopilador como sidecar** en cada tarea de Fargate. La app exporta OTLP a `http://localhost:4317`; sidecar envía:

- **trazas → AWS X-Ray** (exportador `awsxray`),
- **métricas → CloudWatch** (`awsemf`, espacio de nombres `cmind`, grupo de registro `/ecs/<prefix>/metrics`).
- **Registros** permanecen en el controlador `awslogs` como JSON compacto; CloudWatch Logs Insights auto-descubre campos JSON, para que puedas `filter` / `stats` en `trace_id`, `service.name`, `@l`, etc.

El rol de tarea (`aws_iam_role.task`) lleva `AWSXRayDaemonWriteAccess` + `CloudWatchAgentServerPolicy`.

Consulta registros en CloudWatch Logs Insights:

```
fields @timestamp, @l, @m, trace_id, `service.name`
| filter `service.name` = "cmind-web"
| sort @timestamp desc
```

## Endpoints de salud (también utilizados por sondas)

| Endpoint | Servicio | Significado |
|----------|---------|---------|
| `/alive` | Web | Vivacidad — solo proceso. |
| `/health` | Web | Preparación — incluye la verificación de base de datos. |
| `/version` | Web, MCP | Versión de producto + protocolo (vivacidad/preparación de MCP). |

Mapeado en **todos** los ambientes (previamente solo Dev) para que Kubernetes y sondas de nube funcionen en producción.
