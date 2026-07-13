---
description: "مساعد ذكاء اصطناعي. التوصية بإعدادات وجهة نقل التجارة الآمنة من ملف تعريف مخاطرة المتابعة + وصف حساب المصدر (الرئيسي). معرّض عبر REST API، MCP…"
---

# موصي ملف تعريف النسخ بالذكاء الاصطناعي

مساعد ذكاء اصطناعي. التوصية بإعدادات وجهة نقل التجارة الآمنة من ملف تعريف مخاطرة المتابعة + وصف حساب المصدر (الرئيسي). معرّض عبر REST API، أداة MCP، صفحة Copy Trading. استشاري فقط — لا ينشئ/يعدّل أبداً ملف التعريف؛ البشري (أو استدعاء MCP لاحق) يطبق الإعدادات.

## النموذج

- `IAiFeatureService.RecommendCopyProfileAsync(riskProfile, sourceDescription, ct)` — بناء الطلب من موجه `AiPrompts.CopyProfileSystem`، إرجاع `AiResult` نصه = كائن JSON للإعدادات المقترحة: `riskMode` (اسم `MoneyManagementMode`)، `riskParameter`، `maxDrawdownPercent`، `dailyLossLimit`، `direction`، `copyStopLoss`، `copyTakeProfit`، `slippagePips`، `rationale` قصير.
- مثل كل ميزة ذكاء اصطناعي، مُوقّفة على `App:Ai:ApiKey`: لا مفتاح → استدعاء إرجاع `AiResult.Fail(disabled)`، التطبيق لم يتأثر.

## الأسطح

| السطح | الإدخال |
|---------|-------|
| REST | `POST /api/ai/recommend-copy-profile` `{ riskProfile, sourceDescription }` → `AiResult` (ميزة `Ai`، دور User+) |
| MCP | `CopyTools.RecommendCopyProfile(riskProfile, sourceDescription)` (ميزة `CopyTrading`، مندوبة للخدمة AI) |
| واجهة المستخدم | صفحة Copy Trading → زر **AI suggest**؛ يتم عرض التوصية في تنبيه مضمن |

التوصية لم تُطبق تلقائياً عن قصد: يراجع المتابع، ثم ينشئ ملف التعريف / الوجهة عبر حوار Copy Trading العادي (أو عميل MCP يحلل JSON + استدعاءات إنشاء نقاط النهاية).

## الاختبارات

- **وحدة** — `UnitTests/Ai/AiFeatureServiceRecommendTests.cs`: ملف تعريف المخاطرة + وصف المصدر يُحوَّل إلى عميل AI تحت موجه نظام ملف تعريف النسخ (NSubstitute).
- **تكامل** — `IntegrationTests/AiRecommendDisabledTests.cs`: لا مفتاح API → `AnthropicAiClient` الفعلي + `AiFeatureService` تتدهور إلى نتيجة فشل (التطبيق يعمل بدون مفتاح).
- **E2E** — `E2ETests/AiCopyRecommendTests.cs`: زر **AI suggest** استدعاءات endpoint + عرض النتيجة (رسالة "not configured" مهيبة في بيئة الاختبار)، مما يثبت مسار واجهة المستخدم → نقطة النهاية → المسار AI.
