---
description: "بناء وتشغيل واختبار cBots cTrader (C و Python، كلاهما .NET) من محرر Monaco المدمج في المتصفح، التشغيل على صورة ghcr.io/spotware/ctrader-console الرسمية."
---

# بناء واختبار الكود

بناء وتشغيل واختبار cBots cTrader (C# **و** Python، كلاهما .NET) من محرر Monaco المدمج في المتصفح، التشغيل على صورة `ghcr.io/spotware/ctrader-console` الرسمية.

## بناء الكود

- صفحة **Builder** تستضيف محرر Monaco؛ `CBotBuilder` ينسق المشروع مع `dotnet build` **في حاوية قابلة للتجاهل** (`AppOptions.BuildImage`، دليل العمل ربط-monter في `/work`)، لذا لا تصل أهداف MSBuild للمستخدم غير الموثوق بها للمضيف. يتم تخزين استعادة NuGet المؤقت عبر الإصدارات عبر حجم مشترك. يحتاج المضيف الويب إلى الوصول إلى مقبس Docker.
- تعيش قوالب بدء C# + Python في `src/Nodes/Builder/Templates/`.

## تشغيل واختبار الكود

- **الحالات** = TPH التسلسل الهرمي للحالة (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/`Running`/`Stopping`/`Stopped`/`Failed`). الانتقال يستبدل الكيان (تغيير معرف)، يتم نقل معرف الحاوية.
- `NodeScheduler` اختر العقدة الأقل محملة المؤهلة؛ `ContainerDispatcherFactory` الطريق إلى وكيل عقدة HTTP البعيد أو موزع Docker المحلي.
- منخفضات الإكمال مصالحة الحاويات المغادرة (حاويات الاختبار الخلفي تغادر بنفسها عبر `--exit-on-stop`); التقرير الحالي → مكتمل (متجر `ReportJson`)، مفقود → فشل.
- تيارات سجل الحاوية الحية إلى المتصفح على SignalR; يتم تحليل منحنيات رصيد الاختبار الخلفي من التقرير + مخطط.

## ملاحظات CLI وحدة تحكم cTrader

تحتاج الاختبارات الخلفية `--data-mode` (الافتراضي `m1`)، التواريخ كـ `dd/MM/yyyy HH:mm`، و وسيط JSON موضعي `params.cbotset`؛ رفض `run` `--data-dir` (خلفي فقط). انظر `ContainerCommandHelpers`.

## العقد والقياس

سعة التنفيذ المقياس بإضافة وكلاء العقدة (تسجيل ذاتي + نبض القلب). انظر [اكتشاف العقدة](../operations/node-discovery.md) و [القياس](../deployment/scaling.md).
