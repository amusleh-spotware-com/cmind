---
description: "لكل مالك تغذية الأحداث ذات الصلة بالأمان — وجهة المقصد تفعيل قاطع الرفض، انتهاك حماية الحساب أو قاعدة الشركة الملكية، التسطيح بانتظار. مُشغّلة بشكل افتراضي…"
---

# إخطارات تشغيل النسخ (المرحلة 2b)

تغذية لكل مالك للأحداث ذات الصلة بالأمان — وجهة المقصد تفعيل قاطع الرفض، انتهاك حماية الحساب أو قاعدة الشركة الملكية، التسطيح بانتظار. **مُشغّلة بشكل افتراضي** (`App:Copy:NotificationsEnabled`، الافتراضي `true`); تعيين false للصمت. مفهوم خاص به في سياق النسخ، منفصل عن السوق/AI `AlertRule` إجمالي.

## كيفية عمله

نفس نمط المضيف خارج النطاق→ sink→ drainer مثل سجل execution-transparency:

```
CopyEngineHost ──Notify(record)──▶ ICopyNotificationSink
                                     │
             (notifications off) NullCopyNotificationSink   → discards (no-op; unchanged engine)
             (notifications on)  ChannelCopyNotificationSink → bounded DropOldest channel
                                     │
                                     ▼
                            CopyNotificationDrainer (BackgroundService)
                                     │  resolves each profile's owner, batches
                                     ▼
                            CopyNotification feed  ◀── GET /api/copy/notifications
```

- المضيف `Notify(...)` non-blocking، أبداً يرمي — أبداً يلمس DB، أبداً يتأخر النسخ.
- Drainer يحل مالك `UserId` من كل إخطار لملف التعريف؛ الإخطار الذي ذهب ملف التعريف الخاص به (المالك غير قابل للحل) مسقط، وليس يتيم.
- `CopyNotification` = إضافي فقط، تغذية معترف بها لكل صف (ليس إجمالي).

## ما يتم رفعه

| النوع | الخطورة | متى |
|------|----------|------|
| `DestinationTripped` | تحذير | ميزانية رفض G8 استنفدت؛ فتح جديد موقوف للفترة الهادئة. |
| `AccountProtectionTriggered` | حرج | انتهاك سقف/سقف رصيد ZuluGuard؛ يفتح الأقفال (SellOut تصفية). |
| `PropRuleBreached` | حرج | انتهاك prop daily-loss / trailing-drawdown؛ وجهة مسطحة + مقفلة للنهار. |
| `FlattenAll` | حرج | تم تنفيذ التسطيح الذعر؛ كل وجهة مغلقة + مقفلة. |
| `TokenInvalidated` | (محجوز) | تم إبطال رمز وجهة؛ في انتظار الدوران. |

## API

- `GET /api/copy/notifications` (owner-scoped) — إخطارات المستخدم الأخيرة (الأحدث 200) عبر جميع ملفات التعريف، بالإضافة إلى عدد **غير مقبول**.
- `POST /api/copy/notifications/{id}/acknowledge` — وضع علامة واحدة مقروءة.

## التكوين (`App:Copy`)

| الإعداد | الافتراضي | التأثير |
|---------|---------|--------|
| `NotificationsEnabled` | `true` | بث إخطارات الأمان + تشغيل drainer. `false` → حوض no-op. |

## الاختبارات

- **الوحدة** (`CopyNotificationTests`) — وجهة مقصد مرفوعة ترفع `DestinationTripped`؛ تسطيح الذعر رفع `FlattenAll` على مستوى الملف الشخصي. عبر حوض الالتقاط.
- **التكامل** (`CopyNotificationDrainerTests`، Postgres حقيقي) — drainer يحل المالك + يثابت؛ إخطار لملف تعريف غير معروف مسقط.
- **DST** — المضيف ينبعث fire-and-forget مع حوض no-op افتراضي، لذا تبقى حزمة الضغط على النسخ خضراء (23/23).
