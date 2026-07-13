---
description: "حقائق تنفيذ النسخة لكل — الكمون، الانزلاق المحقق، الملء مقابل الفشل — يتم التقاطها في كل محاولة نسخ، مطبقة كتقرير شفافية لكل ملف تعريف. معطّلة بشكل افتراضي…"
---

# شفافية تنفيذ النسخة (المرحلة 3)

حقائق تنفيذ النسخة لكل — الكمون، الانزلاق المحقق، الملء مقابل الفشل — يتم التقاطها في كل محاولة نسخ، مطبقة كتقرير شفافية لكل ملف تعريف. **معطّلة بشكل افتراضي**؛ تمكين مع `App:Copy:TransparencyEnabled=true`. عند الإيقاف، محرك النسخ بايت بايت لم يتغير: المضيف ينبعث إلى حوض no-op، لا شيء مكتوب.

## كيفية عمله

```
CopyEngineHost ──Record(fact)──▶ ICopyEventSink
                                   │
             (transparency off) NullCopyEventSink   → discards (default; zero hot-path cost)
             (transparency on)  ChannelCopyEventSink → bounded in-memory channel (DropOldest)
                                   │
                                   ▼
                          CopyExecutionDrainer (BackgroundService)
                                   │  batches every App drain interval
                                   ▼
                          CopyExecution append-only table  ◀── GET /api/copy/profiles/{id}/transparency
```

- **المسار الساخن يبقى خالياً من I/O.** المضيف يستدعي `ICopyEventSink.Record(...)` — non-blocking، أبداً throwing enqueue. أبداً awaits، أبداً touches DB، أبداً يحظر تنفيذ الأمر.
- **الخسارة مفضلة على الضغط العكسي.** القناة محدود (`CopyExecutionChannelCapacity`) مع `DropOldest`: إذا توقف drainer DB، *الأقدم* صفوف الشفافية انخفضت بدلاً من تأخير نسخة. الشفافية = أفضل جهد التلمترة، وليس تبعية التجارة.
- **الثبات خارج النطاق.** `CopyExecutionDrainer` ينضح القناة في دفعات (`CopyExecutionDrainBatchSize`) على `CopyExecutionDrainInterval`، يكتب صفوف `CopyExecution` عبر `DataContext` المحدود. الشطف النهائي عند إيقاف التشغيل.
- **الحقائق وليس الأوامر.** `CopyExecution` = سجل إضافي فقط (مثل `InstanceLog`/`AuditLog`)، وليس إجمالي. نموذج القراءة يعاينه مباشرة (CQRS-lite)، يجمع في الذاكرة.

## ما يتم تسجيله

واحد `CopyExecutionRecord` لكل محاولة نسخ على وجهة واحدة:

| النوع | متى | الحاملات |
|------|------|---------|
| `Opened` | تم وضع أمر النسخ | symbol، side، wire volume، master price، realized slippage (points)، latency (ms) |
| `Failed` | فتح النسخ رمى/مرفوض | symbol، side، master volume/price، latency، failure reason (exception type) |

(`Closed`/`Skipped`/`Reconciled` موجود في enum للتوسع المستقبلي.)

## التقرير

`GET /api/copy/profiles/{id}/transparency` (owner-scoped) يرجع، على مدى أحدث 500 حقيقة:

- **الملخص** — المجموع، مفتوح، فشل، **معدل الملء**، **متوسط الكمون (ms)**، **متوسط الانزلاق (نقاط)**.
- **Recent** — حقائق حديثة خام (الوجهة، موقع المصدر، symbol، side، volume، master price، slippage، latency، reason، timestamp).

## التكوين (`App:Copy`)

| الإعداد | الافتراضي | التأثير |
|---------|---------|--------|
| `TransparencyEnabled` | `false` | تشغيل لكل التقاط الحقيقة نسخ + drainer للعقدة. |

سعة القناة، حجم دفعة الصرف، فترة الصرف = `CopyDefaults` ثوابت (`CopyExecutionChannelCapacity` / `CopyExecutionDrainBatchSize` / `CopyExecutionDrainInterval`).

## الاختبارات

- **الوحدة** (`CopyTransparencyTests`) — open ناجح ينبعث `Opened` حقيقة مع symbol/side/volume/latency الصحيح؛ open المرفوض ينبعث `Failed` حقيقة مع السبب. مقاد عبر حوض الالتقاط.
- **التكامل** (`CopyExecutionDrainerTests`، Postgres حقيقي) — drainer يثابت الحقائق المخزنة مؤقتاً إلى `CopyExecution` سجل؛ حوض فارغ لا يكتب شيء.
- **DST** — تغيير المضيف fire-and-forget مع حوض no-op افتراضي، لذا تبقى حزمة الضغط على النسخ الحتمية خضراء (23/23).
