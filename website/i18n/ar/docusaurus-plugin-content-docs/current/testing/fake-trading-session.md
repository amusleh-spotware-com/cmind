---
description: "tests/UnitTests/CopyTrading/FakeTradingSession.cs_=_IOpenApiTradingSession_في_الذاكرة_التي_تُشغَّل_عليها_جميع_اختبارات_وحدة_نسخ_التداول._المهمة:_تقليد_خادم_cTrader_Open_API_الحقيقي_بدرجة_كافية_لأن_اختبارات_الوحدة_تغطي_السلوك_الذي_كان_فقط_المستوى_المباشر_يستطيع_التقاطه._هذه_التوثيقة_=_عقد_الولاء:_ماذا_ينمذئ_الأصلي،_بأي_ولاء،_والقاعدة_التي_تحافظ_على_الأصلي."
---

# FakeTradingSession_—_عقد_الولاء_لـ_cTrader_Open_API

`tests/UnitTests/CopyTrading/FakeTradingSession.cs`=_`IOpenApiTradingSession`_
في_الذاكرة_التي_تُشغَّل_عليها_جميع_اختبارات_وحدة_نسخ_التداول._المهمة:_
تقليد **خادم_cTrader_Open_API_الحقيقي** _بدرجة_كافية_لأن_اختبارات_
الوحدة_تغطي_السلوك_الذي_كان_فقط_المستوى_المباشر_يستطيع_التقاطه._هذا_
التوثيق_=_عقد_الولاء:_ماذا_ينمذئ_الأصلي،_بأي_ولاء،_والقاعدة_
التي_تحافظ_على_الأصلي.

> **قاعدة_ملزمة_(CLAUDE.md):** الأصلي_يبقى_مؤمناً_بـ_cTrader. **مدّده،_ولاتُضعفه**
> _لتمرير_اختبار._كل_سلوك_حقيقي_جديد_ تعتمد_عليه_يتم_نمذجته_هنا،
> _مُثبّت_باختبار_الولاء.

## مصفوفة_الولاء_(F1–F13)

تتتبع_خطة_`plans/copy-trading-overhaul.md` §7.6._الأسطورة:_✅_نمذأ_·_◑_جزئي_
(opt-in_/_تمديد)_·_⬜_لم_يُنمذأ_بعد.

| # | سلوك_Open_API_الحقيقي | حالة_الأصلي | كيف_يتم_نمذجته |
|---|---|---|---|
| F1 | أمر_السوق_يمكنه **ملء_جزئي** | ◑ | `PartialFillFractionForCtid[ctid]_=_f` _ملء_فقط_`f×volume`؛_المصادقة_
  تُظهر_الفجوة_ Phase-1 true-up (G5) _يُغلق._قبول→زوج_الملء_لا_يزال_قادماً. |
| F2 | الحجم_يُطبع_إلى **step**،_مرفوض_أقل_من **min** /_أكثر_من **max** | ✅ | `VolumeBoundsForCtid[ctid]_=_(Step,_Min,_Max)` _يُطبع_إلى_الأقل_إلى_
  step،_يُلقي_`CtraderRejectException(VolumeTooLow/High)`. |
| F3 | **SL/TP_غير_صالح** مرفوض_(الجانب_+_الأرقام) | ⬜ | مخطط_في_ Phase 0a/1 (أزواج_مع_M6 _تطبيع_دقة_SL/TP). |
| F4 | الأسعار **مُوسَّعة_صحيحياً_بالأرقام**؛_`pipPosition` | ◑ | `SymbolDetails` _الآن_تحمل_`Digits`(و_`MaxVolume`)_،_مُعبأة_من_الرمز_
  الحقيقي؛_`PipPosition` _تدفع_تحمل_نطاق_السوق،_`Digits` _تدفع_
  تطبيع_دقة_SL/TP_(M6)._توسع_السعر_الصحيح_العدد_لا_يزال_معلقاً. |
| F5 | **نطاق_السوق** _الملء_فقط_إذا_كانت_الأسعار_الفورية_ضمن_`base_±_slippage`،
  وإلا_مرفوض | ✅ | `IsMarketRangeRejected` _يقارن_السعر_الفوري_(`SetSpot`)_إلى_
  `baseSlippagePrice_±_slippageInPoints`._علامة_`RejectMarketRangeForCtid`_
  القديمة_تstill_تُجبر_الرفض. |
| F6 | **مُحرّك_معلق_→_ملء** _حدث_ثنائي_(الأمر_يحمل_`positionId`_+_موضع_ OPEN) | ◑ | `PushOpen(...,_orderId:)` _يُعيد_إنتاج_حدث_الملء_المعلق؛_
  FX-Blue/cMAM_إلغاء_النسخ_المزدوج_مغطى_في_
  `CopyEngineHostTests.Filled_pending_does_not_double_open`. |
| F7 | **إقفال_مُحرّك_بالخادم** (SL/TP_أُصيب،_إيقاف_الخسارة) | ⬜ | اليوم_اختبار_مدفوع_(`PushClose`)؛_SL/TP_المُحرّك_بالسعر_+_
  إقفالات_ stop-out_مخططة. |
| F8 | **لكل_حساب** _جداول/تفاصيل_الرموز | ◑ | أسماء/معرفات_الرموز_لكل_أصلي؛_الجداول_المتباينة_لكل_حساب_
  (cross-broker)_معلقة. |
| F9 | **حالة_الحساب_الكاملة** (الرصيد،_الحقبة،_الهامش،_الرصيد_الحر) | ◑ | `Balance`_=_`LoadPositionValuationsAsync`_(entry/swap/عمولة_عبر_
  `SetPositionValuation`)_+_`SetSpot` _تغذي_الحقبة_الفعلية_إلى_
  تحديد_الحجم_النسبي_للحسابات_(G2،_اختبار_بالوحدة_في_
  `CopyEquitySizingTests`)._الهامش_المستخدم_لا_يعرضه_واجهة_البرمجة_
  للمصادقة_،_لذا_الرصيد_الحر_يُبلغ_كـ_الحقبة. |
| F10 | الأحداث_تحمل **طوابع_الوقت_للخادم** | ✅ | `ExecutionEvent.ServerTimestamp`_(unix_ms)_—_الجلسة_الحقيقية_تقرأ_
  من_`deal's_ExecutionTimestamp`؛_`PushOpen`/`PushPending` _ت accept_`serverTimestamp:`_
  ل drive_`FakeTimeProvider`-_الاختبار_يقود_زمن_ال|latency_النسخ_الحقيقي_(G1). |
| F11 | **وضع_التداول_/_الجدول** (معطل_/_close-only_/_مغلق) | ⬜ | Phase 2b_مخطط. |
| F12 | **تصنيف_الأخطاء_المكتوبة** (`ProtoOAErrorRes` أكواد) | ✅ | `RejectReasonForCtid[ctid]_=_CtraderRejectReason.X` _تُلقي_
  `CtraderRejectException(reason)`_(NotEnoughMoney،_MarketClosed،
  PositionNotFound،_…). |
| F13 | **إلغاء_الرمز** —_رمز_قديم_→_خطأ_ auth | ✅ | `InvalidateToken(ctid)` _يُعلم_الرمز_المُرفق_قديم؛_
  استدعاءات_التداول_تُلقي **حقيقي** `OpenApiException` _مع_
  `OpenApiErrorKind.TokenInvalid`_(رمز_`CH_ACCESS_TOKEN_INVALID`)_،
  تماماً_مثل_الخادم_الحقيقي_،_حتى_`SwapAccessTokenAsync` _يثبت_
  رمزاً_حديثاً._يُغذي_اختبار_متانة_الرمز_M1. |

اختبارات_الولاء_تعيش_في_
`tests/UnitTests/CopyTrading/FakeTradingSessionFidelityTests.cs`.

## opt-in،_الافتراضيات_تحافظ_على_السلوك_القديم

كل_مقبض_ولاء **معطل_افتراضياً** _لذا_الأصلي_يحافظ_على_سلوك_ملء_بسيط_
للاختبارات_التي_لا_تهتم._الاختبار_يختار_لكل_حساب:

```csharp
session.VolumeBoundsForCtid[slave]        = (Step: 10, Min: 10, Max: 1000); // F2
session.PartialFillFractionForCtid[slave] = 0.6;                            // F1 / G5
session.RejectReasonForCtid[slave]        = CtraderRejectReason.NotEnoughMoney; // F12 (one-shot)
session.InvalidateToken(slave);                                             // F13
```

## التوصيف_والمطابقة_(مخطط،_يحافظ_على_الأصلي_≡_الحقيقي)

آليتان_تحافظان_على_الأصلي_مقابل_الخادم_الحقيقي_المتحرك_(مُتتبع،_تهبط_
عبر_ Phase 0a):

1. **توصيف_مباشر** (`LiveApiCharacterization`،_حسابات_demo،_محصور_بالأسرار،
   `Inconclusive` _عند_السوق_المغلق):_تقود_Open_API_الحقيقي_،
   تسجل_الحقيقة_السلكية_الفعلي_(تسلسلات_الأحداث،_التوسع،_أكواد_
   الرفض)_في_ثوابت_ذهبية_مُفحصة_في_مشروع_الاختبار._لا_أسرار_في_
   الثوابت_—_فقط_الأشكال_المُراقَبة.
2. **harness_المطابقة**:_تشغل_مجموعة_السيناريو_نفسه_مرتين_—_مرة_
   ضد_`FakeTradingSession`،_ومرة_ضد_الجلسة_الحقيقية_(عندما_الأسرار_
   موجودة)_—_تؤكد_نتائج_ملاحظة_متطابقة._الخادم_الحقيقي_يتغير_→
   الساق_الحية_تفشل_→_تحديث_الأصلي._هذا_يجعل_"اختبارات_
   الوحدة_تغطي_كل_شيء"_موثوقاً.

الأسرار_الحية:_`secrets/dev-credentials.local.json`_(أو_الملفات_المنقسمة_
القديمة)_—_انظر_`docs/testing/dev-credentials.md`.
