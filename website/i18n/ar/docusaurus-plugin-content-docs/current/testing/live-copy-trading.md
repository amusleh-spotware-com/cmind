---
description: "مجموعة_اختبار_نسخ_التداول_الكاملة._مستويان:"
---

# مجموعة_اختبار_نسخ_التداول_(حتمية_+_مباشرة)

مجموعة_اختبار_نسخ_التداول_الكاملة._مستويان:

1. **اختبارات_حتمية** (xUnit،_بدون_شبكة)_—_رياضيات_النسخ_+_منطق_
   المحرك._سريع،_CI،_لا_أسرار._تغطي_كل_وضع_إدارة_الأموال_وكل_
   فلتر/خيار_ومرونة_المحرك.
2. **اختبارات_E2E_مباشرة** (حسابات_demo_حقيقية_لـ_cTrader)_—_
   `CopyEngineHost` _الإنتاج_يضع_ويُنسخ_أوامر_حقيقية_بين_حسابات_
   حقيقية._مؤتمتة_كلياً_و_قابلة_لإعادة_التشغيل_كاختبار_وحدة:_
   تقرأ_الأسرار_المُخبأة_من_ملفات_gitignored_المحلية،
   تُجدد_رمز_الوصول_ذاتها،_تتخطى_بشكل_رشيق_عندما_الأسرار_غائبة_
   (CI_تبقى_خضراء).

لا_تشغل_أبداً_ضد_حساب_تمويلي_مباشر_—_كل_حساب **demo**،
كل_اختبار_مباشر_يُغلق_المواقع_التي_فتحها.

## التخطيط

```
tests/UnitTests/CopyTrading/
  CopySizingCalculatorTests.cs   —_كل_وضع_حجم_+_التقريب_+_min/max_lot
  CopyDecisionEngineTests.cs     —_اتجاه/عكس/انزلاق/تأخير/فلتر_رمز/حجم_صفر
  CopyEngineHostTests.cs         —_منطق_النسخ_المضيف_مقابل_جلسة_أصلي_في_الذاكرة
  FakeTradingSession.cs          —_IOpenApiTradingSession_حتمي_(تسجل_أوامر/إقفالات/تعديلات)
  OpenApiConnectionTests.cs      —_اتصال_/_إعادة_اتصال_/_تراجع_/_عطل_نهائي_(مرونة)

tests/IntegrationTests/CopyLive/
  LiveCopySecrets.cs             —_يحمل_الأسرار_المُ gitignored،_يحفظ_الرموز_المُجددة
  LiveTokenBootstrapTests.cs     —_لمرة_واحدة:_فك_تشفير_الرموز_من_قاعدة_بيانات_التطبيق_
                                  إلى_ذاكرة_التخزين_الرمزية
  LiveCopyFixture.cs             —_تدوّر_رمز_الوصول،_تعرض_قائمة_حسابات_demo
  LiveCopyScenario.cs            —_يشغل_سيناريو_نسخ_حقيقي_من_بداية_إلى_نهاية_
                                  (فتح_→_نسخ_→_تأكيد_→_تنظيف)
  CopyTradingLiveTests.cs        —_السيناريوهات_المباشرة_(1:1،_1:many،_عكس،_…)
```

## الأسرار_(محلية،_gitignored_—_لا_ارتكاب_أبداً)

كل_الأسرار_تحت_`<repo>/secrets/`_(في_`.gitignore`_+_أصلا)._المطور_
يكتب **أول_ملفين_فقط**؛_الثالث_(الرموز)_ينتج_تلقائياً_بواسطة_onboarding.

`secrets/openapi-test-app.local.json` —_تطبيق_Open_API:

```json
{ "ClientId": "2175_…", "ClientSecret": "…" }
```

`secrets/openapi-cids.local.json` —_اعتماد_تسجيل_الدخول_cID_للتفويض_
(واحد_أو_كثير):

```json
{ "Cids": [
  { "Cid": "amusleh",  "Username": "amusleh",  "Password": "…" },
  { "Cid": "afhacker", "Username": "afhacker", "Password": "…" }
] }
```

`secrets/openapi-tokens.local.json` — **كتبها_onboarding**،_متعدد_cID،
مُجددة_كل_تشغيل:

```json
{ "Cids": [
  { "Cid": "amusleh", "RefreshToken": "…", "AccessToken": "…", "IsLive": false,
    "Accounts": [ { "CtidTraderAccountId": 25172589, "TraderLogin": 3635817, "IsLive": false }, … ] }
] }
```

رمز_التحديث **لا_ينتهي_أبداً**،_لذا_بعد_onboarding_لمرة_واحدة_اختبارات_
النسخ_المباشرة_تعمل_لأجل_غير_مسمى:_كل_تشغيل_يتبادل_رمز_التحديث_
لكل_cID_برمز_وصول_حديث_(تدوير)_—_لا_متصفح،_لا_موجهات.

## onboarding_لمرة_واحدة_(مؤتمت_كلياً_—_لا_تفاعل_مطور_بعد_حفظ_الأسرار)

Onboarding_يقود_تسجيل_الدخول_الحقيقي_لـ_cTrader_ID_في_متصفح_بدون_رأس_
من_اعتماد_cID_المُخفاة،_يحبس_OAuth_callback_على_مستمع_HTTPS_محلي_
عند_إعادة_التوجيه_المُسجّلة_للتطبيق_(`https://localhost:7080/openapi/callback`)_،
يتبادل_الرمز_برمز_ويحمّل_قائمة_الحسابات،_يكتب_ذاكرة_التخزين_
الرمزية_متعددة_cID._شغّل_لمرة_واحدة_لكل_جهاز_(أو_عند_إضافة_cID):

```bash
CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests
```

يُفوض_كل_cID_في_`openapi-cids.local.json`،_يكتب_
`openapi-tokens.local.json`._بعد_ذلك_اختبارات_النسخ_المباشرة_لا_تحتاج_
شيئاً_آخر._(يجب_أن_لا_يكون_لحساب_cID_2FA/captcha_على_تسجيل_الدخول_
لإكمال_الأتمتة.)

**Bootstrap_بديل** _(إذا_الحسابات_معتمدة_بالفعل_في_التطبيق_القائم):_
فك_تشفير_الرموز_المُخزنة_مباشرة_من_قاعدة_بيانات_التطبيق_'s_Postgres_
volume_بدلاً_من_إعادة_التفويض:

```bash
docker run -d --name cmind-pg-extract -e POSTGRES_PASSWORD=appdev \
  -v app-pg-data:/var/lib/postgresql/data -p 5544:5432 postgres:17-alpine
CMIND_VOLUME_CONN="Host=127.0.0.1;Port=5544;Database=appdb;Username=postgres;Password=appdev" \
  dotnet test tests/IntegrationTests --filter FullyQualifiedName~LiveTokenBootstrapTests
docker rm -f cmind-pg-extract
```

## الأمان_—_demo_فقط

اختبارات_مباشرة_تتاول_ **حسابات_demo_فقط**:_الfixture_تُصفّي_ذاكرة_
التخزين_الرمزية_إلى_حسابات_`IsLive_==_false`_وتتصل_ببوابة_demo،
لذا_الأمر_لا_يصل_أبداً_إلى_حساب_تمويلي_مباشر_حتى_لو_حساب_مباشر_
معتمد._كل_موضع_يفتحه_الاختبار_يُغلق_في_التنظيف.

## التشغيل

```bash
# اختبارات_النسخ_الحتمية_فقط_(سريع،_لا_أسرار،_CI-آمن)
dotnet test tests/UnitTests --filter FullyQualifiedName~CopyTrading

# اختبارات_النسخ_المباشرة_مقابل_حسابات_demo_الحقيقية_(تحتاج_الملفين_السريين)
dotnet test tests/IntegrationTests --filter FullyQualifiedName~CopyTradingLiveTests

# الكل
dotnet test
```

بدون_ملفات_أسرار_اختبارات_مباشرة_تطبع_سبب_التخطي_+_تنجح_كـ_no-ops،
لذا_المجموعة_آمنة_للتشغيل_في_أي_مكان.

## التغطية

### إدارة_الأموال_/_تحديد_الحجم_(حتمي_—_`CopySizingCalculatorTests`)
FixedLot_·_LotMultiplier_·_NotionalMultiplier_(contract-size_/_currency)_·_
ProportionalBalance_·_ProportionalEquity_·_ProportionalFreeMargin_·_AutoProportional_
·_FixedRiskPercent_·_FixedLeverage_·
توسع **لأعلى** _و **لأسفل** _لعد_التوازن/leverage/الطاقة_المتناسبين_
("القاعدة_الذهبية")_·_تقريب_lot-step_·_تخطي_min-lot_مقابل_force-to-min_·_
حد_max-lot_·_حد_الأكثر_صرامة_من_الحد_الإيجابي_وmin_&_max_المواصفات_·_
تخطي_ميزان_الماستر_صفر.

### فلاتر_القرار_(حتمي_—_`CopyDecisionEngineTests`)
قائمة_الرموز_السماح/المنع_/allow_·_LongOnly/_ShortOnly_·_عكس_يعكس_
الجانب_و **يبدّل_SL/TP** ·_انزلاق_فوق_الحد_تخطي_+_بالضبط_عند_الحد_
مسموح_·_تخطي_إشارة_قديم_(max_delay)_·_تخطي_حجم_صفر_·
مصادقة_إعادة_الاتصال_(open-missing dedup،_close-orphaned).

### محرك_النسخ_المضيف_(حتمي_—_`CopyEngineHostTests`،_جلسة_في_الذاكرة)
مفتوح_يُنسخ_أمر_السوق_(جانب_/_حجم_/_تسمية)_·_ **عكس** _يعكس_الجانب_
و **يبدّل_SL/TP** ·_ **تخطيط_الرموز** _يحلل_الرمز_الوجهة_·_
 **فشل_الأمر_على_عبد_واحد_لا_يزال_يُنسخ_إلى_الآخرين** ·_مصدر_إغلاق_
يُغلق_النسخة_المُنسوخة_·_إعادة_الاتصال_تُغلق_النسخ_اليتيمة.

### مرونة_الاتصال_(حتمي_—_`OpenApiConnectionTests`)
يصل_إلى_Connected_بعد_اعتماد_التطبيق_·_اتصال_مُسقط_يعيد_الاتصال_ويُعيد_
Auth_·_خطأ_ auth_فادح_يُعطب_·_تراجع_أسي.

### مباشر،_حسابات_demo_حقيقية_لـ_cTrader_(`CopyTradingLiveTests`)
تجديد_الرمز_وقائمة_الحسابات_·_ **1:1** _نسخ_يُنفذ_·_ **1:many** _يُنسخ_
إلى_كل_عبد_·_ **عكس** _يحول_شراء_الماستر_إلى_بيع_العبد_·_
 **cross-cID** _(الماستر_تحت_cID_واحد_يُنسخ_إلى_عبد_تحت_cID_آخر،
_لكل_يُصادق_بمعرفه_الخاص)._كل_واحد_يفتح_موضع_min-lot_حقيقي_على_
الماستر،_ينتظر_المحرك_ليُنسخه_(مطابق_بواسطة_معرف_موقع_المصدر_
على_العبد)_،_يؤكد_،_يُغلق_كل_شيء._السوق_المغلق_يُبلغ_
**Inconclusive**،_وليس_فشل.

## التسجيل_والتدقيق

كل_عملية_نسخ_تداول_مسجلة_عبر_أحداث_مُنشأة_من_المصدر_(
`Core/Logging/LogMessages.cs`،_معرفات_الأحداث_1043–1055)_،_
مسار_كامل_قابل_للتدقيق:

| الحدث | Id | المعنى |
|---|---|---|
| CopyHostStarted | 1046 | محرك_ملف_انطلق_(مصدر_+_عدد_الوجهات) |
| CopySourceOpen | 1047 | الماستر_فتح_موضع_(رمز_/_جانب_/_عقود) |
| CopyOrderPlaced | 1048 | أمر_النسخ_أُرسل_إلى_عبد_(رمز_/_جانب_/_حجم_/_معرف_المصدر) |
| CopySkipped | 1049 | تم_تخطي_نسخ_والسبب_(انزلاق_/_اتجاه_/_فلتر_رمز_/_حجم_صفر_/_…) |
| CopyProtectionApplied | 1050 | SL/TP_طُبّق_على_نسخة_عبد |
| CopyOpenFailed | 1051 | فتح_نسخة_عبد_فشل_(معزول_—_العبد_الأخرى_تستمر) |
| CopySourceClose / CopyPositionClosed | 1052 / 1053 | ماستر_أغلق_→_نسخة_عبد_أُغلقت |
| CopyCloseFailed | 1054 | إغلاق_نسخة_عبد_فشل |
| CopyResync | 1055 | مصادقة_إعادة_الاتصال_(عدد_مفتوح_المصدر،_الأيتام_أُغلقوا) |
| CopyPartialClose | 1056 | إغلاق_جزئي_للاستنساخ_انعكس_—_شريحة_متناسبة_أُغلقت_على_عبد |
| CopyScaleIn | 1057 | scale-in_للاستنساخ_انعكس_(opt-in)_—_حجم_مضاف_أُنسخ_إلى_عبد |
| CopyPendingOrderPlaced | 1058 | أمر_معلق_limit/stop_انعكس_إلى_عبد_(opt-in) |
| CopyPendingOrderCancelled | 1059 | الأمر_المعلق_المصدر_أُلغي_→_الأمر_المعلق_العبد_أُلغي |
| CopyTrailingApplied | 1060 | stop_متتابع_طُبّق_على_نسخة_عبد_(opt-in) |
| CopyStopLossAmended | 1061 | نقل_SL_المصدر_أعاده_تُعدّل_نسخة_العبد |
| CopyHostTokenRotated | 1062 | المنشئ_أعاده_يد_أعاد_تشغيل_المضيف_بعد_تدوير_رمز_الوصول |

تُصدر_السجلات_كتنسيق_JSON_مضغوط_Serilog_(خصائص_مُهيكلة:_
`ProfileId`،_`DestinationCtid`،_`SourcePositionId`،_`Symbol`،_`Side`،
`Volume`،_…)،_تُشحن_إلى_OTLP_عند_ضبط_`OTEL_EXPORTER_OTLP_ENDPOINT`._
**قابل_للتكوين_كلياً** _لكل_فئة_عبر_التكوين_القياسي_—_مثلاً_رفع/خفض_
تفضيل_محرك_النسخ_بدون_لمس_الكود:

```jsonc
// appsettings.json —_تجاوزات_مستوى_Serilog
"Serilog": { "MinimumLevel": { "Override": {
  "CopyEngine": "Information",              // مسار_التدقيق_في_CopyEngineHost
  "Nodes.CopyTrading": "Information"        // المنشئ_/_تجديد_الرمز
} } }
```

`Audit_log_records_every_trading_operation` _اختبار_المضيف_يؤكد_المسار_
يُصدر_لفتح_وأمر_وحماية_وإغلاق.

## حالات_الحد_(تم_التحقق_مقابل_كيف_تفشل_منصات_النسخ/MAM_الحقيقية)

انزلاق_&_latency،_لاحقة_الرمز/عدم_التطابق،_التداول_المكرر_عند_
إعادة_الاتصال،_عدم_تطابق_الرافعة_و_تحديد_حجم_هامش_آمن،_اختلاف_
عملة_الإيداع/حجم_العقد،_min/max_lot_&_التقريب،_الأوامر_المرفوضة،
فلاتر_الاتجاه،_تنظيف_الأيتام_بعد_قطع_الاتصال_—_كل_ذلك_مغطى_
أعلاه._المصادر:
[عدم_تطابق_الرافعة](https://copygram.app/blog/education/the-truth-about-leverage-mismatches-copying-high-leverage-low-leverage-accounts)_
·_
[النسخ_cross-broker](https://www.mt4copier.com/cross-broker-trade-copying-efficient-forex-replication/)_
·_
[فخاخ_النسخ](https://www.mt4copier.com/copy-trading-pitfalls-every-account-manager-must-avoid/)_
·_
[انزلاق_&_latency](https://copygram.app/blog/education/understanding-slippage-latency-copy-trading)_
·_
[لماذا_يفشل_نسخ_التداول](https://xtsupport.zendesk.com/hc/en-us/articles/51566808595993-Why-Copy-Trading-Fails-Causes-Prevention-Guide)_
·_
[معاملات_المخاطر](https://www.mt4copier.com/risk-parameters/).

## تغطية_التنميط_المتقدم_(إغلاق_جزئي_·_أوامر_معلة_·_SL-trailing)

المضيف_يُنسخ_أكثر_من_فتح_السوق/إغلاق._كل_سلوك_=_خيار_ opt-in_لكل_وجهة_
على_`CopyDestination`_(`MirrorPartialClose` _افتراضي_تشغيل_،_`MirrorScaleIn`/
`CopyPendingOrders`/`CopyTrailingStop` _افتراضي_إيقاف)_،_محروس_بأساليب_
القصد_،_مُ persistمُ كـ_jsonb_(هجرة_
`CopyAdvancedMirroringAndNodeAffinity`).

| السلوك | الاختبار_الحتمي_(`CopyEngineHostTests`) | الاختبار_المباشر |
|---|---|---|
| إغلاق_جزئي_→_شريحة_متناسبة | `Partial_close_mirrors_a_proportional_slice_on_the_slave`_(1.0→0.4_أغلق_60%)_+_المسار_المعطل | `Partial_close_shrinks_the_slave_copy_proportionally` ✅ |
| scale-in | `Scale_in_is_ignored_by_default_and_mirrored_when_enabled` | — |
| أمر_معلق_limit/stop_مُوضع | `Pending_order_is_placed_on_the_slave_when_enabled`_(نظرية:Limit+Stop)_+_المسار_المعطل | `Pending_limit_order_is_mirrored_and_cancel_propagates` ✅ |
| إلغاء_معلق | `Source_pending_cancel_cancels_the_slave_pending` | (نفس_الاختبار_المباشر_—_يُلغي_على_الماستر،_يؤكد_العبد_يُلغي) ✅ |
| ملء_معلق_لا_يفتح_مرتين | `Filled_pending_does_not_double_open`_(dedupe_بـ_معرف_الأمر_→_معرف_الموضع) | — |
| stop_متتابع | `Trailing_stop_is_applied_to_the_copy_when_enabled` | `Trailing_stop_is_mirrored_onto_the_slave_copy` ✅ |
| نقل_SL_المصدر_يُعدّل | `Source_stop_loss_move_re_amends_the_copy` | — |
| أحداث_التدقيق_تُصدر | `Advanced_mirroring_audit_events_fire`_(1056/1058/1059) | — |

كل_الاختبارات_المباشرة_أعلاه **تم_التحقق_من_اخضرارها** _مقابل_
حسابات_demo_الحقيقية_لـ_cTrader_(1:1،_1:many،_عكس،_cross-cID،_إغلاق_جزئي،
معلق+إلغاء،_trailing).

إضافات_السلك_في_`OpenApiTradingSession`:
`SendPendingOrderAsync`،_`CancelOrderAsync`،_`ReconcilePendingOrdersAsync`،
علامة_trailing_على_`AmendPositionSltpAsync`،_حقول_الأمر/المعلق_على_
`ExecutionEvent`،_`LoadSpotPriceAsync`_(اشتراك_الفورية_→_bid/ask،
_تُستخدم_باختبارات_المعلق/المتتابع_الحية_لوضع_الأوامر_المعلنة_بعيداً_
عن_السوق)_،
`StopLoss`/`TrailingStopLoss` _على_`OpenPositionSnapshot`_(حالة_trailing_
للاستنساخ_قابلة_للماحظة_عبر_المصادقة)._النسخ_تبقى_مُسماة_بـ **معرف_
موقع_المصدر**_(النسخ_المعلنة_بـ **معرف_الأمر_المصدر**)_لذا_المصادقة_
عند_إعادة_الاتصال_تظل_مبنية_على_المعرف_،_لا_تُكرر_تداولاً.

**gotcha_الحدث_cTrader_(تم_التحقق_مباشرة):** حدث_تنفيذ_
`ORDER_ACCEPTED`/`ORDER_CANCELLED`_للأمر_المعلق_المُقام_يحمل_
**غير_مفتوح_`Position`Placeholder** _بالإضافة_إلى_`Order`._التدفق_يجب_
أن_يُصنّفه_كـ *حدث_أمر* **قبل** _فرع_الموضع_(بوابة_على_الموضع_ليس_
`OPEN`)_،_وإلا_تموضع_المعلق_المُنسوخ_يُقرأ_خطأً_كـ_إغلاق_موضع. _
`SourceExecutionsAsync` _يفعل_هذا؛_عدمه_يُسقط_صامتاً_كل_تنميط_
الأوامر_المعلنة.

## تدوير_الرمز_+_انجذاب_العقدة

- **التدوير_إلى_المضيفين_القيد_التشغيل.**_`CopyEngineSupervisor` _يسجل_
  توقيع_الرمز_على_كل_مضيف_قيد_التشغيل_و،_كل_مصادقة_،_يعيد_بناء_
  الخطة_من_قاعدة_البيانات_(مدوّرة_حديثاً_بواسطة_`OpenApiTokenRefreshService`)._
  التوقيع_المتغير_يعيد_تشغيل_المضيف_(`CopyHostTokenRotated`،_1062)؛_
  المضيف_الجديد_`ResyncAsync` _يعيد_بناء_الحالة_بدون_تكرار_التداول._
  تدوير_القوة_في_منتصف_التشغيل_عبر_`IOpenApiTokenClient.RefreshAsync`_
  للتحقق_من_المضيف_الحية_يستمر_في_النسخ.
- **انجذاب_العقدة_(لا_نسخ_مزدوج).**_منشئ_الويب_المحلي_و_عامل_`CopyAgent`_
  كلاهما_يشغل_منشئاً._كل_ملف_قيد_التشغيل_مطال_بواسطة_عقدة_واحدة_فقط_
  (`CopyProfile.AssignedNode`،_ادعاء_`ExecuteUpdate` _ذري_مفتاح_من_
  `CopyOptions.NodeName`،_اسم_الجهاز_الافتراضي)._المنشئ_يستضيف_فقط_
  الملفات_التي_يمتلكها؛_إيقاف/إيقاف_يُصدر_ادعاء._التغطية:_
  - المجال_(وحدة):_`AssignToNode_makes_profile_hosted_by_only_that_node`،
    `Stopping_a_profile_releases_its_node_assignment`،
    `NodeIdentity_rejects_blank`.
  - **تكامل_(Postgres_حقيقي،_Testcontainers)**:_`CopyNodeAffinityTests`_
    _يدفع_`ClaimUnassignedProfilesAsync` _الفعلي_للمنشئ_—_يؤكد_أول_عقدة_
    تدّعي_3_ملفات_قيد_التشغيل،_الثانية_تدّعي **0** (لا_مضيف_مزدوج)_،
    إيقاف→إعادة_تشغيل_يُصدر_الادعاء_لعقدة_أخرى.
  - كشف_التدوير_(`TokenRotationSignatureTests`):_توقيع_الرمز_الخاص_
    بالمنشئ_يتغير_عندما_تدور_رمز_المصدر_أو_الوجهة_،_ثابت_بخلاف_
    ذلك_(مضيف_قيد_التشغيل_يعيد_التشغيل_فقط_عند_التدوير_الحقيقي).

### رموز_التحديث_ذات_الاستخدام_الواحد_(مهم)

رموز_تحديث_cTrader **ذات_استخدام_واحد** —_كل_تحديث_يُرجع_رمز_تحديث_
 *جديد*_،_يُلغي_القديم._الfixture_تحديث_في_البداية،_تُPersistence_الرمز_
  المدوّر_إلى_`secrets/openapi-tokens.local.json`._النتائج:
- إذا_التحديث_يعمل_لكن **لا_يستطيع_الإلحاق** _الرمز_الجديد_(مثلاً_
  mount_قراءة_فقط)_،_الرمز_المُخبأ_ميت،_التشغيل_التالي_يفشل_`ACCESS_DENIED`._
  أعد_التوليد_بـ_onboarding_بدون_رأس:
  `CMIND_ONBOARD=1_dotnet_test_tests/E2ETests_--filter_FullyQualifiedName~OnboardingTests`.
- `LiveCopySecrets.SaveTokens` _يبتلع_أخطاء_الكتابة_لذا_ذاكرة_التخزين_
  المقروءة_فقط_لا_تُعطّل_التشغيل،_لكن **المباشر** _في_المجموعة_
  في_العنقود_لا_يزال_يحتاج **ذاكرة_التخزين_القابلة_للكتابة**_(K8s_Job_
  تنسخ_Secret_إلى_emptyDir_—_انظر_توثيق_النشر).

## تشغيل_المجموعة_في_عنقود_Kubernetes

المجموعة_الكاملة_تشغل_في_العنقود_مقابل_التطبيق_المنتشر_عبر_Helm_،
لذا_الانحدار_يُلتقط_في_العنقود_مثل_المحلية._انظر_
[`docs/deployment/kubernetes.md`](../deployment/kubernetes.md#in-cluster-test-suite).

```bash
scripts/k8s-e2e.sh                                   # مجموعة_حتمية_(لا_أسرار)
TEST_FILTER='FullyQualifiedName~CopyTradingLiveTests' COPY_SECRET=cmind-copy-secrets scripts/k8s-e2e.sh  # مباشر
```

`Dockerfile.tests` _يبني_صورة_المُشغِّل؛_Helm_`tests-job.yaml`_(بوابة_
`tests.enabled=false`)_تشغله_مقابل_Postgres_+_Web_+_MCP_في_العنقود._
**الافتراضي_=_المجموعة_الحتمية**_(لا_أسرار،_لا_رموز_مدوّرة)._
للمجموعة_المباشرة،_اضبط_`tests.copySecret` _إلى_Secret_التي_تحمل_
`openapi-*.local.json` _المُgitignored_؛_init-container_تنسخه_إلى_
**emptyDir_قابل_للكتابة** _في_`/app/secrets`_(مطلوب_—_رموز_التحديث_
ذات_الاستخدام_الواحد_يجب_أن_تكون_قابلة_للإلحاق)._اختبارات_النسخ_
تحتاج_فقط_Web_+_Postgres_+_ذاكرة_التخزين_الرمزية_—_لا_وكلاء_عقد_
مميزين._البرنامج_يؤكد_الوظيفة_تخرج_0_والسجلات_تحتوي_`Passed!`.

**تم_التحقق_هنا_(Docker،_لا_عنقود):** صورة_الاختبار_تشغل_المجموعة_
الحتمية_(`101_نجح`)_و،_مع_ mount_`secrets/`_قابل_للكتابة_،_المجموعة_
 **المباشرة** الكاملة_(_8_نجح_)_—_المسار_الفعلي_للوظيفة_ناقص_
Kubernetes._`kind`/`kubectl`/`helm` _غير_متوفرة_في_بيئة_التأليف،
لذا_تشغيل_العنقود_الكامل_`k8s-e2e.sh` _هو_الخطوة_الواحدة_غير_
المنفذة_هنا.

## مصفوفة_الخيار_المباشر_+_الفوضى_(LiveCopyMatrix_/_LiveCopyChaos)

مجموعتان_مباشرتان_مدارتان_بـ_`LiveCopyScenario`/_`LiveCopyFixture`_,
النظير_المباشر_لمجموعة_DST_الإجهاد:

- **`LiveCopyMatrix`** —_`[Theory]`/`[MemberData]`_مصفوفة_خيار:_مفتوح_
  ماستر_حقيقي_واحد_لكل_صف_مقابل_حسابات_demo_،
  كل_واحد_بتجهيز_وجهة_مُكوَّن_مختلف_،
  يؤكد_النتيجة_الذهبية._الصفوف:_`one_to_one`،_`half_multiplier`،
  `reverse`_(الجانب_المعاكس)_،_`manage_only`_(لا_يفتح_شيئاً)_،
  `trading_hours_closed`_(النافذة_استبعدت_الآن_→_لا_نسخ)_،
  `source_label_block`_(فلتر_التسمية_→_لا_نسخ)_،
  `lot_sanity_block`_(السقف_→_لا_نسخ).
- **`LiveCopyChaos`** —_محرك_النسخ_مقابل_بداية_عدائية:_الماستر_يمتلك_
  موضع_قبل_بدء_المضيف_،_لذا_التقارب_لا_يستطيع_القدوم_إلا_من_
  مصادقة_البدء_(`Sync-Open-on-Start`)_،_بالإضافة_إلى_الحالة_
  السلبية_مع_التبديل_إيقاف._التغطية_الحتمية_لـ_socket-flap_/_
  تدوير_الرمز_/_رفض_الفوضى_مُغطاة_بشكل_شامل_بواسطة_مجموعة_DST؛_
  هذا_يؤكد_مسار_ resync_المباشر.

كلاهما **يتخطى_بشكل_رشيق** _بدون_أسرار،_يُبلغ_ **Inconclusive** _
عند_السوق_المغلق،_كل_صف_يُنظف_المواقع_التي_فتحها_(حسابات_demo_فقط).

**متانة_الfixture:**_`LiveCopyFixture` _الآن_تتodegradate_إلى_تخطي_نظيف_
عندما_لا_تستطيع_ذاكرة_التخزين_المُخبأة_التحديث_(رموز_التحديث_
ذات_الاستخدام_الواحد_—_ذاكرة_تخزين_قديمية_أو_تشغيل_غير_متصل_لا_تُعطّل_
المجموعة_المباشرة_بعدين؛_أعد_تشغيل_onboarding_OAuth_لتحديث_
ذاكرة_التخزين).

## تشغيل_المجموعات_المباشرة_+_العنقودية_بنفسك_(لا_عذر_—_كل_شيء_
ذاتي_الخدمة)

كل_ما_أدناه_متوفر_في_هذه_البيئة._لا_تدّعي_"الأسرار_غير_
متوفرة" أو_"الرمز_قديم"_أو_"kind/helm_غير_مثبتة"_—_اضبطها_
وشغّلها.

### 1._احصل_على_رمز_Open_API_حديث_(OAuth_بدون_رأس،_لا_تفاعل_متصفح)

رموز_تحديث_cTrader_ذات_الاستخدام_الواحد،_لذا_الرمز_المُخبأ_سيصبح_
قديم._أعد_صنعه_ذاته_من_اسم_المستخدم_وكلمة_ال مرور_المُخفاة_
(`secrets/openapi-cids.local.json`_+
`secrets/openapi-test-app.local.json`،_أو_موحّد_
`secrets/dev-credentials.local.json`)._اختبار_onboarding_يقود_
OAuth_بدون_رأس_عبر_Playwright_،_يكتب_
`secrets/openapi-tokens.local.json`:

```bash
CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests
```

~13_ثانية؛_يُفوض_كل_cID_،_يُخبئ_رموز_حديثة._أعد_التشغيل_كلما_
مجموعة_مباشرة_تُبلغ_عن_عدم_توفر_fixture_بسبب_فشل_التحديث.

### 2._شغّل_مجموعات_النسخ_المباشرة_(حسابات_demo_حقيقية_لـ_cTrader)

```bash
dotnet test tests/IntegrationTests --filter FullyQualifiedName~CopyTradingLiveTests   # التنميط_الأساسي_(8)
dotnet test tests/IntegrationTests --filter FullyQualifiedName~LiveCopyMatrix          # مصفوفة_الخيارات_(7)
dotnet test tests/IntegrationTests --filter FullyQualifiedName~LiveCopyChaos           # فوضى_resync_(2)
```

ضع_ونظّف_أوامر_ DEMO_الحقيقية_(لا_حسابات_مباشرة)_،_يُبلغ_
**Inconclusive** _عند_السوق_المغلق._تم_التحقق_من_اخضرارها_من_طرف_
إلى_طرف.

### 3._Bootstrap_الرموز_من_حجم_تطبيق_قائم_(بديل)

إذا_التطبيق_شغال_+_cID_مرتبط_في_التطبيق_،_استخرج_أحدث_رمز_تحديث_
للمصدر_مباشرة_من_`app-pg-data` _Postgres_volume_بدلاً_من_إعادة_
التفويض_—_انظر_`LiveTokenBootstrapTests`،_اضبط_`CMIND_VOLUME_CONN`.

### 4._E2E_عنقود_Kubernetes

`kind`،_`helm`،_Docker_متوفرة_(ثبّت_ kind/helm_عبر_`go_install`/إصدارات_
 ثنائية_أو_`choco_install_kind_kubernetes-helm`_إذا_لم_على_PATH)._
برنامج_لمرة_واحدة_يبني_ويحمّل_الصور_،_ينشر_chart_،_يشغل_وظيفة_
الاختبار_في_العنقود_،_يؤكد_خروج_0:

```bash
scripts/k8s-e2e.sh                                 # مجموعة_النسخ_الحتمية_(لا_أسرار)
TEST_FILTER='FullyQualifiedName~CopyTradingLiveTests' COPY_SECRET=cmind-copy-secrets scripts/k8s-e2e.sh   # مباشر_في_العنقود
```

انظر_[../deployment/kubernetes.md](../deployment/kubernetes.md).
