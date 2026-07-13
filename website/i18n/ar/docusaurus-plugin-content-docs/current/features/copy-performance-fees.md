---
description: "رسوم الأداء لمدير الأموال على علامة مائية عالية، نموذج النسخة التجارية القياسي (cTrader Copy، Darwinex، ZuluTrade profit-share): يتقاضى المزود…"
---

# رسوم أداء النسخ (المرحلة 4)

رسوم أداء مدير الأموال **على علامة مائية عالية**، نموذج النسخة التجارية القياسي (cTrader Copy، Darwinex، ZuluTrade profit-share): يتقاضى المزود نسبة من *الربح الجديد* فوق ذروة رصيد كل متابع — أبداً على الرصيد الافتتاحي، وأبداً مرتين للأرضية المستعادة بالفعل. **اختياري** عبر `App:Copy:FeesEnabled` (معطّل افتراضياً).

## النموذج (العلامة المائية العالية)

لكل وجهة (حساب المتابع)، كل تسوية:

1. **التسوية الأولى** تبذر العلامة المائية العالية (HWM) بالرصيد الحالي → لا رسم (لا يتم فرض رسوم على المتابع أبداً على الإيداع الخاص به).
2. **الذروة الجديدة** (الرصيد > HWM): `fee = performanceFeePercent × (equity − HWM)`، ثم `HWM ← equity`.
3. **في أو أسفل الذروة**: لا رسم، HWM لم يتغير — يجب على المتابع أولاً استعادة الذروة القديمة، لذا لا يتم فرض رسوم عليهم مرتين للمكاسب نفسها.

حساب الرسوم هو ثابت النطاق على `CopyDestination.SettleFee(equity)` — يملك الإجمالي؛ خدمة التسوية توفر فقط الرصيد المستقصى وتسجل المبلغ المرجعة. `PerformanceFee` هو كائن قيمة مقيد بـ 50% حتى لا يتمكن الإعداد الخاطئ من فرض رسوم على كل كسب متابع.

## كيف يتم تسويته

```
CopyFeeSettlementService (BackgroundService, only when FeesEnabled)
   │  every App:Copy:FeeSettlementInterval
   ├─ load running profiles with a fee-configured destination
   ├─ ICopyEquityReader.ReadEquityAsync(ctid)   ← OpenApiCopyEquityReader opens a session,
   │                                               computes balance + floating P&L (PropFirmEquityCalculator)
   ├─ destination.SettleFee(equity)             ← HWM logic on the aggregate
   └─ persist advanced HWM + append CopyFeeAccrual (only on a new high)
```

- `ICopyEquityReader` هو تجريد Core؛ التنفيذ الحي (`OpenApiCopyEquityReader`) هو القطعة البنية الحتمية الوحيدة — لذا يتم ممارسة منطق التسوية + HWM في الاختبارات مع قارئ مزيف، لا وسيط حي.
- `CopyFeeAccrual` هو سجل إضافي فقط (HWM-before، equity، fee %، fee amount، settled-at) — سجل حقيقي للتقرير والفواتير، وليس إجمالي.

## التكوين و API

| إعداد `App:Copy` | الافتراضي | التأثير |
|--------------------|---------|--------|
| `FeesEnabled` | `false` | تشغيل خدمة التسوية. |
| `FeeSettlementInterval` | `1h` | كم مرة يتم استقصاء الرصيد وتسوية الرسوم. |

لكل وجهة: `PerformanceFeePercent` (0–50) يتم تعيينها على الوجهة (طلب إضافة/تحرير وجهة).

- `GET /api/copy/profiles/{id}/fees` — تجميع الرسوم بملف التعريف + إجمالي المفروضة.

## الاختبارات

- **الوحدة** (`CopyPerformanceFeeTests`) — ثابت HWM: التسوية الأولى تبذر + لا تفرض رسم؛ ذروة جديدة تفرض رسم فقط الكسب فوق الذروة؛ في/أسفل الذروة لا تفرض رسم والذروة لا تتراجع أبداً؛ بعد الانسحاب فقط الاسترجاع ماضٍ الذروة القديمة مفروضة؛ 0% أبداً تفرض رسم؛ VO يرفض النسب خارج النطاق.
- **التكامل** (`CopyFeeSettlementTests`، Postgres حقيقي، قارئ رصيد مزيف) — بذر→10k (لا رسم، علامة منبوذة)، 12k (رسوم 400، علامة متقدمة)، 11k (لا رسم، علامة محتفظ)؛ تجميع مثابت مع المالك/المبلغ الصحيح.

لا يتأثر مضيف النسخ برسوم (التسوية عمل DB منفصل)، لذا حزمة الضغط DST النسخ لم تتأثر (23/23).
