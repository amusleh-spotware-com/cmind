---
description: "جميع الخدمات الثلاث (Web و MCP و CtraderCliNode) تسجيل عبر Serilog بـ JSON مضغوط على stdout — container runtimes وslog collectors (Loki و ELK و CloudWatch…"
---

# التسجيل والمراقبة

جميع الخدمات الثلاث (Web و MCP و CtraderCliNode) تسجيل عبر **Serilog** كـ **JSON مضغوط على stdout** —
container runtimes وجامعات السجلات (Loki و ELK و CloudWatch و Azure Log Analytics و Datadog) تتناول
الأحداث المنظمة مباشرة وبدون تحليل النصوص الحرة.

## الخط الأنابيب

- **Sink 1 — Console (JSON مضغوط).** `RenderedCompactJsonFormatter`؛ كل حدث يحمل
  OpenTelemetry هوية المورد الكامل — `service.name` (`cmind-web` / `cmind-mcp` / `cmind-node-agent` /
  `cmind-copy-agent`)، `service.version`، `service.namespace` (`cmind`)، `deployment.environment` —
  بالإضافة إلى `trace_id` / `span_id` من ambient `Activity` (`ActivityEnricher`) و `LogContext` scopes.
  معرفات التتبع دع CloudWatch Logs Insights و Azure Log Analytics محور log↔trace **حتى بدون
  المجمع المنتشر**.
- **Sink 2 — OTLP (اختياري).** عند تعيين `OTEL_EXPORTER_OTLP_ENDPOINT`، تصدير السجلات أيضًا عبر OTLP
  مع نفس سمات المورد بجانب OpenTelemetry **المقاييس** و **الآثار** (ASP.NET Core و
  HttpClient و instrumentation في وقت التشغيل) من `AddAppTelemetry`.
- **Sink 3 — Azure Monitor (اختياري).** عند تعيين `APPLICATIONINSIGHTS_CONNECTION_STRING`، آثار
  والمقاييس تصدير **بشكل أصلي** إلى Application Insights (`AddAzureMonitorTraceExporter` /
  `AddAzureMonitorMetricExporter`) — بدون المجمع. انظر قسم cloud-native أدناه.
- **الرسائل المولدة المصدر.** يستخدم التطبيق extensions `LogMessages` من حيث الكتابة بقوة
  (`Core/Logging/LogMessages.cs`) مع `EventId`s مستقر — أبدًا ad-hoc `ILogger.LogInformation`.
- **تسجيل الطلب.** `UseSerilogRequestLogging()` ينبت ملخص هيكلي واحد لكل طلب HTTP
  (الطريقة والمسار والحالة والـ ms المنقضي).

## التشكيل

مستويات قابلة للتعديل لكل خدمة عبر قسم `Serilog` في `appsettings.json` (قراءة عبر
`ReadFrom.Configuration`)، على سبيل المثال:

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

قم بالكتابة فوق في وقت التشغيل مع متغيرات env، على سبيل المثال `Serilog__MinimumLevel__Default=Debug`.

## الشحن إلى جامع

اضبط متغير بيئة واحد لكل خدمة والإشارة إلى نقطة نهاية OTLP:

```
OTEL_EXPORTER_OTLP_ENDPOINT=http://otel-collector:4317
```

- Helm: `--set observability.otlpEndpoint=http://otel-collector:4317`.
- Compose / cloud: إضافة متغير env لكل خدمة.

من جامع والمراوح للخارج إلى backend (Tempo/Jaeger traces و Prometheus metrics و Loki logs) مع
trace↔log correlation سليمة.

## Backends cloud-native (بدون المجمع الإضافي)

كلا النشرات المدارة مسلكة لمكدس القابلية للمراقبة الأصلي خارج الصندوق — لا يلزم جامع OTLP.

### Azure — Application Insights + Log Analytics

`deploy/azure/main.bicep` يوفر **مكون Application Insights المستند إلى Workspace**، يمرر
سلسلة الاتصال الخاصة بها إلى Web و MCP كـ `APPLICATIONINSIGHTS_CONNECTION_STRING`. نتيجة:

- **الآثار والمقاييس** تتدفق بشكل أصلي إلى App Insights (Application Map و live metrics و end-to-end
  بحث المعاملات) ومرتبطة بـ `trace_id`.
- **السجلات** (JSON مضغوط على stdout) الهبوط في **نفس workspace Log Analytics** عبر Container Apps
  `appLogsConfiguration`، لذا `AppTraces` / `ContainerAppConsoleLogs_CL` الانضمام على معرف التتبع.
- تعيين معامل Bicep `otlpEndpoint` اختياري **أيضًا** الفاني إلى جامع.

السجلات الاستعلام في Log Analytics (JSON line في `Log_s`):

```kusto
ContainerAppConsoleLogs_CL
| extend log = parse_json(Log_s)
| where log.["service.name"] == "cmind-web"
| project TimeGenerated, level = log["@l"], msg = log["@m"], trace_id = log.trace_id
```

### AWS — X-Ray + CloudWatch (ADOT sidecar)

`deploy/aws/main.tf` يشغل **AWS Distro for OpenTelemetry (ADOT) المجمع كـ sidecar** في كل
مهمة Fargate. تطبيق تصدير OTLP إلى `http://localhost:4317`؛ sidecar الحفار:

- **traces → AWS X-Ray** (`awsxray` exporter)،
- **metrics → CloudWatch** (`awsemf` وnamespace `cmind` وlog group `/ecs/<prefix>/metrics`).
- **السجلات** البقاء على `awslogs` driver كـ JSON مضغوط؛ CloudWatch Logs Insights auto-discovers JSON
  الحقول، لذا يمكنك `filter` / `stats` على `trace_id` و`service.name` و`@l` إلخ.

دور المهمة (`aws_iam_role.task`) يحمل `AWSXRayDaemonWriteAccess` + `CloudWatchAgentServerPolicy`.

السجلات الاستعلام في CloudWatch Logs Insights:

```
fields @timestamp, @l, @m, trace_id, `service.name`
| filter `service.name` = "cmind-web"
| sort @timestamp desc
```

## نقاط نهاية الصحة (تستخدم أيضًا من قبل المجسات)

| نقطة نهاية | الخدمة | المعنى |
|----------|---------|---------|
| `/alive` | الويب | الحيوية — العملية فقط. |
| `/health` | الويب | الاستعداد — يتضمن فحص قاعدة البيانات. |
| `/version` | Web و MCP | إصدار المنتج والبروتوكول (MCP الحيوية/الاستعداد). |

تم التعيين في **جميع** البيئات (سابقًا Dev-only فقط) لذلك Kubernetes ومجسات السحابة تعمل في
الإنتاج.
