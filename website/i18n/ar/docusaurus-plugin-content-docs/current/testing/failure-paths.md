---
title: خريطة_تغطية_مسارات_الفشل
description: كل_سيناريو_فشل_الذي_يتطلبه_التفويض_،_معقود_إلى_الاختبار(الاختبارات)_التي_تمارسه_فعلياً_—_لذا_الفجوة_مرئية_وليس_مُقدَّرة.
---

# خريطة_تغطية_مسارات_الفشل

التفويض_الاختبار_صريح:_ **مسارات_الفشل_تحسب**_—_تغيير_يمكن_
أن_ينكسر_عند_اتصال_مقطوع_،_أمر_مرفوض_،_عدم_التزامن/المصادقة_،
تدوير_رمز_،_أو_موت_عقدة_ يشحن_مع_اختبار_لذلك_،
في_نفس_الالتزام._هذه_الصفحة_تربط_كل_سيناريو_مطلوب_إلى_
الاختبار(الاختبارات)_التي_تمارسه_،_لذا_فجوة_حقيقية_هي *
مرئية*_بدلاً_من_مُقدَّرة._عندما_تضيف_مسار_فشل_،_أضف_صفاً_هنا.

## السيناريوهات_المطلوبة_→_الاختبارات

| السيناريو | المستويات | الاختبارات |
|---|---|---|
| **انقطاع_الاتصال_→_إعادة_اتصال** | وحدة_·_إجهاد_·_E2E | `OpenApiConnectionTests.Dropped_connection_reconnects_and_raises_reconnected`؛ `FakeTradingSession.Disconnect/ReconnectAsync` _و_`SyncTradingSession` (DST)؛ `MiscUiTests` _حالات_حوار_إعادة_الاتصال |
| **رفض_الأمر** | وحدة_·_إجهاد | `CopyTransparencyTests.A_rejected_open_emits_a_Failed_execution_fact_with_the_reason`؛ `CopyCircuitBreakerTests`؛ DST `CopyDstWorld.FailOrders` / `RejectMarketRange` |
| **عدم_التزامن_/_المصادقة** | وحدة_·_إجهاد | `CopyPartialFillTests.Resync_tops_up_a_broker_partial_fill…`؛ `CopyEngineHostTests.Reconnect_resync_closes_orphaned_destination_positions` (+_`…tolerates_a_position_not_found…`)؛ `CopyAdvancedScenariosTests.Reconnect_resync_opens_missing_copies_and_closes_orphans_after_a_desync`؛ `CopyChaosDstTests` |
| **تدوير_الرمز_/_إلغاء_الصلاحية** | وحدة_·_تكامل_·_إجهاد | `OpenApiAuthorizationTests.MarkRefreshFailed_*` (نافذة_التصعيد)؛ `FakeTradingSession.InvalidateToken`؛ `TokenRotationSignatureTests`، `LiveTokenBootstrapTests`، `OpenApiTokenRefreshPersistenceTests` (تكامل)؛ DST `RotateTokens` |
| **موت_العقدة_→_استرداد_الاستئجار** | وحدة_·_تكامل_·_إجهاد | `NodeInstanceReclaimerTests` (وحدة_+_تكامل)؛ `CopyRulesDomainTests.Lease_is_held_only_by_the_claiming_node_until_it_expires`؛ `CopyHostWatchdogTests`، `CopyNodeAffinityTests`، `PropFirmTrackingLeaseTests` (تكامل)؛ `CopyLeaseReclaimStressTests` |
| **خطأ_مزود_AI_(4xx/5xx/timeout/malformed)** | وحدة_·_تكامل | `AnthropicAiClientTests.Fails_gracefully_on_error_status` / `…on_malformed_json` / `…on_empty_content`؛ `AiHttpResilienceTests`، `AiRecommendDisabledTests` (تكامل) |
| **AI_معطل_كلياً_(لا_مفتاح)** | وحدة_·_تكامل_·_E2E | `AiFeatureServiceTests`؛ `AiRecommendDisabledTests`؛ `AiPagesTests` |
| **فشل_قاعدة_البيانات_العابر_/_قفل_الترحيل** | تكامل | `DatabaseResilienceTests`؛ `MigrationLockTests` |
| **فشل_وكيل_العقدة_HTTP_/_إعادة_المحاولة** | تكامل | `NodeAgentHttpResilienceTests` |
| **إنهاء_الحاوية_الذاتي_المصادقة** | وحدة | `BacktestCompletionPollerTests`؛ `RunCompletionPoller` _تغطية_في_`ContainerCommandHelpersTests` |
| **خرق_Prop-firm** | وحدة_·_تكامل | `PropFirmChallengeRulesTests`؛ `PropFirmAlertNotifierTests`؛ `PropFirmChallengePersistenceTests` |
| **إدخال_غير_صالح_/_رفض_ auth_(UI_+_العلامات)** | وحدة_·_تكامل_·_E2E | `LoginTests.Invalid_credentials_show_an_error`؛ `HexColorTests.Rejects_invalid_hex`؛ `BrandingOptionsValidatorTests` |

## مناطق_رقيقة_—_تحقق_قبل_الافتراض_المغطى

هذه_تستحق_فحصاً_صريحاً_(أضف_صفاً_أعلاه_بمجرد_التأكيد_أو_الملء):

- **رفض_ auth_أداة_MCP** —_`McpKeyAuthHandler` _يرفض_مفتاح_سيئ/غائب._لم_يُوجد_اختبار_مخصص؛
  أضف_اختبار_تكامل_يستدعي_نقطة_أداة_MCP_مع_مفتاح_غائب/غير_صالح_ويؤكد_401.
- **سطح_فشل_بناء_CBot** —_خطأ_التجميع_يجب_أن_يهبط_على_الحالة/الواجهة_كـ_`Failed`_
  مع_مخرجات_البناء._`CBotLifecycleTests` _يغطي_المسار_السعيد؛_أكد_فرع_
  الفشل_مُ_ASSERTed.
- **التنفيذ_المباشر** —_التنفيذ_النهائي_للأوامر_المباشرة_مقابل_اعتمادات_cTrader_الحقيقية_
  لا_يزال_محجوباً_(يحتاج_اعتمادات__+_عنقود_عقد)_؛_انظر_[التداول_المباشر_بالنسخ](./live-copy-trading.md).

## كيف_يُنفَّذ_هذا

مجموعة_الإجهاد_الحتمية_(DST،_`tests/StressTests`)_تُعيد_تشغيل_هذه_
الفشل على_ساعة_مضغوطة_ويجب_أن_تبقى_خضراء_—_ **لا_تُضعف_سيناريو_
إجهاد_لتمريره؛_أصلح_الكود**._[FakeTradingSession](./fake-trading-session.md)_
هو_المحاكي_المؤمن_بـ_cTrader_الذي_تشغل_عليه_اختبارات_الوحدة_هؤلاء؛_
مدّده_للسلوك_الوسيط_الجديد_بدلاً_من_إرخاء_تأكيد.
