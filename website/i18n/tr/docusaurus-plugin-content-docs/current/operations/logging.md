---
description: "Üç servisin tümü (Web, MCP, CtraderCliNode) Serilog aracılığıyla stdout üzerinde kompakt JSON olarak günlük kaydı yapar — konteyner çalışma zamanları ve günlük toplayıcılar (Loki, ELK, CloudWatch…"
---

# Günlük kaydı ve gözlemlenebilirlik

Üç servisin tümü (Web, MCP, CtraderCliNode) **Serilog** aracılığıyla stdout üzerinde **kompakt JSON**
olarak günlük kaydı yapar — konteyner çalışma zamanları ve günlük toplayıcılar (Loki, ELK, CloudWatch,
Azure Log Analytics, Datadog) yapılandırılmış olayları doğrudan alır, serbest-metin ayrıştırması
gerekmez.

## Boru hattı

- **Alıcı 1 — Konsol (kompakt JSON).** `RenderedCompactJsonFormatter`; her olay tam OpenTelemetry
  kaynak kimliğini taşır — `service.name` (`cmind-web` / `cmind-mcp` / `cmind-node-agent` /
  `cmind-copy-agent`), `service.version`, `service.namespace` (`cmind`), `deployment.environment` —
  ayrıca ortam `Activity`'sinden (`ActivityEnricher`) ve `LogContext` kapsamlarından `trace_id` /
  `span_id`. İzleme kimlikleri, CloudWatch Logs Insights ve Azure Log Analytics'in **hiçbir toplayıcı
  dağıtılmamış olsa bile** günlük↔iz eksen değiştirmesine olanak tanır.
- **Alıcı 2 — OTLP (isteğe bağlı).** `OTEL_EXPORTER_OTLP_ENDPOINT` ayarlandığında, günlükler de
  `AddAppTelemetry`'den gelen OpenTelemetry **metrikleri** ve **izleri** (ASP.NET Core, HttpClient,
  çalışma zamanı enstrümantasyonu) ile birlikte aynı kaynak öznitelikleriyle OTLP üzerinden dışa
  aktarılır.
- **Alıcı 3 — Azure Monitor (isteğe bağlı).** `APPLICATIONINSIGHTS_CONNECTION_STRING` ayarlandığında,
  izler ve metrikler Application Insights'a **yerel olarak** dışa aktarılır
  (`AddAzureMonitorTraceExporter` / `AddAzureMonitorMetricExporter`) — toplayıcı yok. Aşağıdaki
  bulut-yerel bölümüne bakın.
- **Kaynak-üretilen mesajlar.** Uygulama günlükleri kararlı `EventId`'lerle güçlü-tipli `LogMessages`
  uzantılarını (`Core/Logging/LogMessages.cs`) kullanır — asla anlık `ILogger.LogInformation` değil.
- **İstek günlüğü.** `UseSerilogRequestLogging()`, HTTP isteği başına yapılandırılmış bir özet yayar
  (yöntem, yol, durum, geçen ms).

## Yapılandırma

Seviyeler, `appsettings.json`'daki `Serilog` bölümü aracılığıyla servis başına ayarlanabilir
(`ReadFrom.Configuration` aracılığıyla okunur), örn.:

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

Çalışma zamanında ortam değişkenleriyle geçersiz kılın, örn. `Serilog__MinimumLevel__Default=Debug`.

## Bir toplayıcıya gönderme

Servis başına bir ortam değişkeni ayarlayın, OTLP uç noktasına yönlendirin:

```
OTEL_EXPORTER_OTLP_ENDPOINT=http://otel-collector:4317
```

- Helm: `--set observability.otlpEndpoint=http://otel-collector:4317`.
- Compose / bulut: her servise ortam değişkeni ekleyin.

Toplayıcıdan, iz↔günlük ilişkisi bozulmadan arka uca (Tempo/Jaeger izleri, Prometheus metrikleri,
Loki günlükleri) dağıtın.

## Bulut-yerel arka uçlar (ek toplayıcı yok)

Her iki yönetilen dağıtım da kutudan çıktığı gibi yerel gözlemlenebilirlik yığını için bağlanmıştır —
OTLP toplayıcısı gerekmez.

### Azure — Application Insights + Log Analytics

`deploy/azure/main.bicep`, **çalışma-alanı-tabanlı Application Insights** bileşeni sağlar, bağlantı
dizesini Web ve MCP'ye `APPLICATIONINSIGHTS_CONNECTION_STRING` olarak geçirir. Sonuç:

- **İzler + metrikler**, `trace_id` ile ilişkilendirilerek App Insights'a yerel olarak akar
  (Application Map, canlı metrikler, uçtan-uca işlem araması).
- **Günlükler** (stdout üzerinde kompakt JSON), Container Apps `appLogsConfiguration` aracılığıyla
  **aynı Log Analytics çalışma alanına** iner, böylece `AppTraces` / `ContainerAppConsoleLogs_CL` iz
  kimliğinde birleşir.
- Bir toplayıcıya **da** dağıtmak için isteğe bağlı `otlpEndpoint` Bicep parametresini ayarlayın.

Log Analytics'te günlükleri sorgulayın (`Log_s` içindeki JSON satırı):

```kusto
ContainerAppConsoleLogs_CL
| extend log = parse_json(Log_s)
| where log.["service.name"] == "cmind-web"
| project TimeGenerated, level = log["@l"], msg = log["@m"], trace_id = log.trace_id
```

### AWS — X-Ray + CloudWatch (ADOT yan-arabası)

`deploy/aws/main.tf`, her Fargate görevinde **AWS Distro for OpenTelemetry (ADOT) toplayıcısını
yan-araba olarak** çalıştırır. Uygulama OTLP'yi `http://localhost:4317`'e dışa aktarır; yan-araba
gönderir:

- **izler → AWS X-Ray** (`awsxray` dışa aktarıcı),
- **metrikler → CloudWatch** (`awsemf`, ad alanı `cmind`, günlük grubu `/ecs/<prefix>/metrics`).
- **Günlükler**, kompakt JSON olarak `awslogs` sürücüsünde kalır; CloudWatch Logs Insights JSON
  alanlarını otomatik keşfeder, böylece `trace_id`, `service.name`, `@l` vb. üzerinde `filter` /
  `stats` yaparsınız.

Görev rolü (`aws_iam_role.task`), `AWSXRayDaemonWriteAccess` + `CloudWatchAgentServerPolicy` taşır.

CloudWatch Logs Insights'ta günlükleri sorgulayın:

```
fields @timestamp, @l, @m, trace_id, `service.name`
| filter `service.name` = "cmind-web"
| sort @timestamp desc
```

## Sağlık uç noktaları (yoklamalar tarafından da kullanılır)

| Uç nokta | Servis | Anlamı |
|----------|---------|---------|
| `/alive` | Web | Canlılık — yalnızca süreç. |
| `/health` | Web | Hazırlık — veritabanı kontrolünü içerir. |
| `/version` | Web, MCP | Ürün + protokol sürümü (MCP canlılık/hazırlık). |

Kubernetes ve bulut yoklamalarının üretimde çalışması için **tüm** ortamlarda eşlenir (önceden
yalnızca Dev).
