# Logging & observability

All three services (Web, MCP, ExternalNode) log through **Serilog** as **compact JSON on stdout**,
so container runtimes and log collectors (Loki, ELK, CloudWatch, Azure Log Analytics, Datadog)
ingest structured events directly — no parsing of free-text lines.

## Pipeline

- **Sink 1 — Console (compact JSON).** `RenderedCompactJsonFormatter`; every event carries
  `service.name` (`cmind-web` / `cmind-mcp` / `cmind-node-agent`) plus `LogContext` scopes.
- **Sink 2 — OTLP (optional).** When `OTEL_EXPORTER_OTLP_ENDPOINT` is set, logs are also exported
  over OTLP with trace/span correlation from the ambient `Activity`, alongside the existing
  OpenTelemetry **metrics** and **traces** (ASP.NET Core, HttpClient, runtime instrumentation).
- **Source-generated messages.** Application logs use the strongly-typed `LogMessages` extensions
  (`Core/Logging/LogMessages.cs`) with stable `EventId`s — never ad-hoc `ILogger.LogInformation`.
- **Request logging.** `UseSerilogRequestLogging()` emits one structured summary per HTTP request
  (method, path, status, elapsed ms).

## Configuration

Levels are tunable per service via the `Serilog` section in `appsettings.json` (read through
`ReadFrom.Configuration`), e.g.:

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

Override at runtime with env vars, e.g. `Serilog__MinimumLevel__Default=Debug`.

## Shipping to a collector

Set one env var on each service and point it at an OTLP endpoint:

```
OTEL_EXPORTER_OTLP_ENDPOINT=http://otel-collector:4317
```

- Helm: `--set observability.otlpEndpoint=http://otel-collector:4317`.
- Compose / cloud: add the env var to each service.

From the collector, fan out to your backend (Tempo/Jaeger for traces, Prometheus for metrics, Loki
for logs) with trace↔log correlation intact.

## Health endpoints (also used by probes)

| Endpoint | Service | Meaning |
|----------|---------|---------|
| `/alive` | Web | Liveness — process only. |
| `/health` | Web | Readiness — includes the database check. |
| `/version` | Web, MCP | Product + protocol version (MCP liveness/readiness). |

These are mapped in **all** environments (previously Dev-only) so Kubernetes and cloud probes work
in production.
