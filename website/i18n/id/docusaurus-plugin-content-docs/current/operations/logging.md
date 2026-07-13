---
description: "Ketiga service (Web, MCP, CtraderCliNode) log via Serilog sebagai compact JSON pada stdout — container runtime dan log collector (Loki, ELK, CloudWatch…"
---

# Logging & observability

Ketiga service (Web, MCP, CtraderCliNode) log via **Serilog** sebagai **compact JSON pada stdout** — container runtime dan log collector (Loki, ELK, CloudWatch, Azure Log Analytics, Datadog) ingest structured event langsung, no free-text parsing.

## Pipeline

- **Sink 1 — Console (compact JSON).** `RenderedCompactJsonFormatter`; setiap event membawa full OpenTelemetry resource identity — `service.name` (`cmind-web` / `cmind-mcp` / `cmind-node-agent` / `cmind-copy-agent`), `service.version`, `service.namespace` (`cmind`), `deployment.environment` — plus `trace_id` / `span_id` dari ambient `Activity` (`ActivityEnricher`) dan `LogContext` scope. Trace id biarkan CloudWatch Logs Insights dan Azure Log Analytics pivot log↔trace **bahkan dengan tidak ada collector deployed**.
- **Sink 2 — OTLP (optional).** Saat `OTEL_EXPORTER_OTLP_ENDPOINT` set, log juga export over OTLP dengan resource attribute sama, bersama OpenTelemetry **metric** dan **trace** (ASP.NET Core, HttpClient, runtime instrumentation) dari `AddAppTelemetry`.
- **Sink 3 — Azure Monitor (optional).** Saat `APPLICATIONINSIGHTS_CONNECTION_STRING` set, trace dan metric export **natively** ke Application Insights (`AddAzureMonitorTraceExporter` / `AddAzureMonitorMetricExporter`) — tidak ada collector. Lihat section cloud-native di bawah.
- **Source-generated message.** App log gunakan strongly-typed `LogMessages` extension (`Core/Logging/LogMessages.cs`) dengan stable `EventId` — tidak pernah ad-hoc `ILogger.LogInformation`.
- **Request logging.** `UseSerilogRequestLogging()` emit satu structured summary per HTTP request (method, path, status, elapsed ms).

## Konfigurasi

Level tunable per service via `Serilog` section di `appsettings.json` (read through `ReadFrom.Configuration`), mis.:

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

Override pada runtime dengan env var, mis. `Serilog__MinimumLevel__Default=Debug`.

## Shipping ke collector

Set satu env var per service, arahkan ke OTLP endpoint:
