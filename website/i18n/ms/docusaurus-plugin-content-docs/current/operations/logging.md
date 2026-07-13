---
description: "Kesemua tiga perkhidmatan (Web, MCP, CtraderCliNode) log melalui Serilog sebagai JSON ringkas pada stdout — masa jalan kontena dan pengumpul log (Loki, ELK, CloudWatch…)"
---

# Pembalakan & observabiliti

Kesemua tiga perkhidmatan (Web, MCP, CtraderCliNode) log melalui **Serilog** sebagai **JSON ringkas pada stdout** —
masa jalan kontena dan pengumpul log (Loki, ELK, CloudWatch, Azure Log Analytics, Datadog) ingest
peristiwa berstruktur secara langsung, tiada penghuraian teks bebas.

## Salur

- **Sink 1 — Konsol (JSON ringkas).** `RenderedCompactJsonFormatter`; setiap peristiwa membawa identiti OpenTelemetry sumber penuh — `service.name` (`cmind-web` / `cmind-mcp` / `cmind-node-agent` /
  `cmind-copy-agent`), `service.version`, `service.namespace` (`cmind`), `deployment.environment` —
  tambah `trace_id` / `span_id` daripada `Activity` ambient (`ActivityEnricher`) dan skop `LogContext`.
  ID trace membolehkan CloudWatch Logs Insights dan Azure Log Analytics berpusing log↔trace **walaupun tiada pengumpul digunakan**.
- **Sink 2 — OTLP (pilihan).** Apabila `OTEL_EXPORTER_OTLP_ENDPOINT` ditetapkan, log juga export melalui OTLP
  dengan atribust yang sama, sebelah OpenTelemetry **metrik** dan **trace** (ASP.NET Core,
  HttpClient, pemprosesan masa jalan) daripada `AddAppTelemetry`.
- **Sink 3 — Azure Monitor (pilihan).** Apabila `APPLICATIONINSIGHTS_CONNECTION_STRING` ditetapkan, trace dan metrik export **secara asli** ke Application Insights (`AddAzureMonitorTraceExporter` /
  `AddAzureMonitorMetricExporter`) — tiada pengumpul. Lihat bahagian asli awan di bawah.
- **Mesej dijana sumber.** Log apl menggunakan `LogMessages` sambungan taip strongly
  (`Core/Logging/LogMessages.cs`) dengan `EventId` yang stabil — tidak pernah `ILogger.LogInformation` ad-hoc.
- **Pembalakan permintaan.** `UseSerilogRequestLogging()` memancarkan satu ringkasan berstruktur setiap permintaan HTTP
  (kaedah, laluan, status, ms elapsed).

## Konfigurasi

Tahap boleh ditala setiap perkhidmatan melalui bahagian `Serilog` dalam `appsettings.json` (dibaca melalui
`ReadFrom.Configuration`), cth:

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

Override pada masa jalan dengan pembolehubah env, cth `Serilog__MinimumLevel__Default=Debug`.

## Menghantar ke pengumpul

Tetapkan satu pembolehubah env setiap perkhidmatan, tunjuk ke titik akhir OTLP:

```
OTEL_EXPORTER_OTLP_ENDPOINT=http://otel-collector:4317
```

- Helm: `--set observability.otlpEndpoint=http://otel-collector:4317`.
- Compose / awan: tambah pembolehubah env kepada setiap perkhidmatan.

Dari pengumpul, edar kepada belakang (Tempo/Jaeger trace, Prometheus metrik, Loki log) dengan
trace↔log correlation utuh.

## Backend asli awan (tiada pengumpul tambahan)

Kedua-dua penempatan yang diintegrasi untuk tumpukan observabiliti asli keluar dari kotak — tiada pengumpul OTLP needed.

### Azure — Application Insights + Log Analytics

`deploy/azure/main.bicep` memperuntukkan **Application Insights komponen workspace-based**, melewati
rentetan sambungannya kepada Web dan MCP sebagai `APPLICATIONINSIGHTS_CONNECTION_STRING`. Keputusan:

- **Trace + metrik** mengalir secara natively ke App Insights (Application Map, metrik langsung, gelintaran transaksi hujung-ke-hujung), dikolerasikan oleh `trace_id`.
- **Log** (JSON ringkas pada stdout) mendarat di **ruang kerja Log Analytics yang sama** melalui Container Apps
  `appLogsConfiguration`, jadi `AppTraces` / `ContainerAppConsoleLogs_CL` bercantum pada trace id.

Tanya log dalam Log Analytics (baris JSON dalam `Log_s`):

```kusto
ContainerAppConsoleLogs_CL
| extend log = parse_json(Log_s)
| where log.["service.name"] == "cmind-web"
| project TimeGenerated, level = log["@l"], msg = log["@m"], trace_id = log.trace_id
```

### AWS — X-Ray + CloudWatch (ADOT sidecar)

`deploy/aws/main.tf` menjalankan **AWS Distro for OpenTelemetry (ADOT) collector sebagai sidecar** dalam setiap
tugas Fargate. Apl export OTLP ke `http://localhost:4317`; sidecar kapal:

- **trace → AWS X-Ray** (`awsxray` exporter),
- **metrik → CloudWatch** (`awsemf`, namespace `cmind`, kumpulan log `/ecs/<prefix>/metrics`).
- **Log** kekal pada pemandu `awslogs` sebagai JSON ringkas; CloudWatch Logs Insights auto-mengesan JSON
  medan, jadi anda `filter` / `stats` pada `trace_id`, `service.name`, `@l`, dll.

Peranan tugas (`aws_iam_role.task`) membawa `AWSXRayDaemonWriteAccess` + `CloudWatchAgentServerPolicy`.

Tanya log dalam CloudWatch Logs Insights:

```
fields @timestamp, @l, @m, trace_id, `service.name`
| filter `service.name` = "cmind-web"
| sort @timestamp desc
```

## Titik akhir kesihatan (juga digunakan oleh probe)

| Titik akhir | Perkhidmatan | Makna |
|----------|---------|---------|
| `/alive` | Web | Ketersediaan — proses sahaja. |
| `/health` | Web | Kesediaan — termasuk semakan pangkalan data. |
| `/version` | Web, MCP | Versi produk + protokol (Ketersediaan/m Bacaan MCP). |

Dipetakan dalam **kesemua** persekitaran (sebelum ini Dev sahaja) jadi probe Kubernetes dan awan berfungsi dalam
pengeluaran.
