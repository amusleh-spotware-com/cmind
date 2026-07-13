---
description: "إنشاء وتشغيل واختبار_backtest_لـ_cBots_من_cTrader_(C#_و_Python،_كلاهما_.NET)_من_محرر_Monaco_في_المتصفح،_يعمل_على_صورة_ghcr.io/spotware/ctrader-console_الرسمية."
---

# إنشاء واختبار_backtest_لـ_cBots

إنشاء وتشغيل واختبار_backtest_لـ_cTrader_ cBots_(C#_و_Python،_كلاهما_.NET)_من_محرر_Monaco_في_المتصفح،_يعمل_على_صورة_`ghcr.io/spotware/ctrader-console`_الرسمية.

## الإنشاء

- صفحة **Builder** تستضيف محرر_Monaco؛ يقوم `CBotBuilder` بتجميع المشروع باستخدام
  `dotnet build` **في_حاوية_ذات_استخدام_واحد** (`AppOptions.BuildImage`،_عمل_المجلد_ bind-mount
  في_`/work`)،_لذا_لا_تصل_أهداف_MSBuild_غير_الموثوقة_إلى_المضيف._NuGet_مخ细_
  عبر_البناءات_عبر_مجلد_مشارك._مضيف_الويب_يحتاج_إلى_وصول_إلى_مقبس_Docker.
- قوالب_البدء_لـ_C#_و_Python_موجودة_في_`src/Nodes/Builder/Templates/`.

## التشغيل_واختبار_backtest

- **Instances** =_تسلسل_هرمي_للحالة_TPH_(`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/
  `Running`/`Stopping`/`Stopped`/`Failed`)._الانتقال_يستبدل_الكيان_(تغيير_المعرف)،
  مع_الحفاظ_على_معرف_الحاوية.
- `NodeScheduler`_يختار_أقل_العقد_ تحميلاً_المؤهلة؛_`ContainerDispatcherFactory`_توجه_إلى
  وكيل_العقدة_البعيدة_عبر_HTTP_أو_مرسل_Docker_المحلي.
- مزامنة_إكمال_الاستطلاع_تؤكد_الحاويات_الخارجة_(حاويات_backtest_تنهي_ذاتها_عبر_
  `--exit-on-stop`)؛_التقرير_موجود_→_مكتمل_(يخزن_`ReportJson`)_،_مفقود_→_فاشل.
- سجلات_الحاوية_الحية_تتدفق_إلى_المتصفح_عبر_SignalR؛_منحنيات_حقبة_backtest_تُحلل_من_
  التقرير_وتُعرض_بيانياً.

## ملاحظات_حول_واجهة_cTrader_Console_CLI

اختبارات_backtest_تحتاج_إلى_`--data-mode`(الافتراضي_`m1`)_،_التواريخ_كـ_`dd/MM/yyyy HH:mm`،
  و_`params.cbotset` _JSON_وسيط_موضعي؛_`run`_ترفض_`--data-dir`(اختبار_backtest_فقط)._انظر_
  `ContainerCommandHelpers`.

## العقد_والتوسع

السعة_التنفيذية_تتوسع_بإضافة_وكلاء_العقد_(التسجيل_الذاتي_و_نبضات_القلب)._انظر_
  [اكتشاف_العقد](../operations/node-discovery.md)_و[التوسع](../deployment/scaling.md).
