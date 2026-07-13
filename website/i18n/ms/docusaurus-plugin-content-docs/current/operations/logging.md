---
description: "Ketiga perkhidmatan (Web, MCP, CtraderCliNode) log melalui Serilog sebagai JSON padat pada stdout — waktu jalan bekas dan pengumpul log (Loki, ELK, CloudWatch…"
---

# Log & kebolehmataan

Ketiga perkhidmatan (Web, MCP, CtraderCliNode) log melalui **Serilog** sebagai **JSON padat pada stdout** — waktu jalan bekas dan pengumpul log (Loki, ELK, CloudWatch, Azure Log Analytics, Datadog) mengambil acara berstruktur secara terus, tiada analisa teks bebas.

## Saluran

- **Sink 1 — Konsol (JSON padat).** `RenderedCompactJsonFormatter`; setiap peristiwa membawa identiti sumber OpenTelemetry penuh — `service.name` (`cmind-web` / `cmind-mcp` / `cmind-node-agent` / `cmind-copy-agent`), `service.version`, `service.namespace` (`cmind`), `deployment.environment` — tambah `trace_id` / `span_id` daripada `Activity` ambien (`ActivityEnricher`) dan skop `LogContext`. ID jejak membiarkan CloudWatch Logs Insights dan Azure Log Analytics pivot log↔jejak **bahkan tanpa pengumpul dikebarkan**.
- **Sink 2 — OTLP (pilihan).** Apabila `OTEL_EXPORTER_OTLP_ENDPOINT` ditetapkan, log juga eksport melalui OTLP dengan atribut sumber yang sama, bersama OpenTelemetry **metrik** dan **jejak** (ASP.NET Core, HttpClient, instrumentasi waktu jalan) daripada `AddAppTelemetry`.
- **Sink 3 — Monitor Azure (pilihan).** Apabila `APPLICATIONINSIGHTS_CONNECTION_STRING` ditetapkan, jejak dan metrik eksport **secara asli** ke Application Insights (`AddAzureMonitorTraceExporter` / `AddAzureMonitorMetricExporter`) — tiada pengumpul. Lihat bahagian cloud-native di bawah.
- **Mesej yang dihasilkan sumber.** Log apl menggunakan sambungan `LogMessages` yang kuat-ditaip (`Core/Logging/LogMessages.cs`) dengan `EventId`s stabil — tidak pernah ad-hoc `ILogger.LogInformation`.
- **Log permintaan.** `UseSerilogRequestLogging()` memancarkan ringkasan berstruktur satu setiap permintaan HTTP (kaedah, laluan, status, ms yang berlalu).

## Konfigurasi

Peringkat boleh dialih setiap perkhidmatan melalui bahagian `Serilog` dalam `appsettings.json` (baca melalui `ReadFrom.Configuration`), cth:

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

Overide pada masa jalan dengan pembolehubah env, cth `Serilog__MinimumLevel__Default=Debug`.

## Penghantaran ke pengumpul

Tetapkan satu pembolehubah env setiap perkhidmatan, arahkan ke titik akhir OTLP:

```
OTEL_EXPORTER_OTLP_ENDPOINT=http://otel-collector:4317
```

- Helm: `--set observability.otlpEndpoint=http://otel-collector:4317`.
- Kompos / cloud: tambah pembolehubah env ke setiap perkhidmatan.

Daripada pengumpul, kipas ke luar ke backend (jejak Tempo/Jaeger, metrik Prometheus, log Loki) dengan korelasi jejak↔log tetap.
