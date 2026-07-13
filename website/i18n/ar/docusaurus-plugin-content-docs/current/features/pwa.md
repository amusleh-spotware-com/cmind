---
description: "ينصب cMind على هاتف أو سطح المكتب مثل تطبيق أصلي — رمز الشاشة الرئيسية وعاء مستقل وsplash وصفحة offline ودية. إنها mobile-first و..."
---

# تطبيق قابل للتثبيت (PWA)

ينصب cMind على هاتف أو سطح المكتب مثل تطبيق أصلي — رمز الشاشة الرئيسية وعاء مستقل وsplash و
صفحة offline ودية. إنه **mobile-first** وreactive بالكامل؛ انظر
[ui-guidelines.md](../ui-guidelines.md).

## ما يعنيه "قابل للتثبيت" هنا — والحد الصادق

Blazor **Server** يعرض من خلال دائرة SignalR حية، لذلك التطبيق لا يمكن أن يعمل بالكامل في وضع offline. ما
PWA توفر:

- **قابل للتثبيت** — manifest ويب صحيح + أيقونات، لذلك المتصفحات توفر *تثبيت* / *إضافة إلى الشاشة الرئيسية*.
- **App-shell مخزن مؤقتًا** — عامل الخدمة يخزن مؤقتًا الأصول الثابتة (CSS والرموز والmanifest) ويعرض
  **صفحة offline** عند انقطاع الشبكة، بدلاً من خطأ المتصفح.
- **الشعور الأصلي** — عرض مستقل وzied branded theme-color/status bar وأيقونة التطبيق وأيقونة الشاشة الرئيسية iOS.

إنه **لا** يوفر التفاعل offline — سيتطلب ذلك Blazor WebAssembly (مسار منفصل مستقبلي). لا تعد
استخدام offline من الميزات الحية.

## القطع

| القطعة | حيث |
|-------|-------|
| Manifest (ديناميكي ومستقة) | `Web/Endpoints/PwaEndpoints.cs` → `GET /manifest.webmanifest` (مجهول) |
| الرموز (192 و512 و512-maskable وapple-touch-180) | `Web/wwwroot/icons/` |
| عامل الخدمة (app-shell) | `Web/wwwroot/service-worker.js` |
| صفحة offline fallback | `Web/wwwroot/offline.html` |
| التسجيل + علامات iOS + التقاط install-prompt | `Web/Components/App.razor` |
| ثوابت الطريق | `Core.Constants.PwaRoutes` |

### Manifest

يتم تقديمه ديناميكيًا من `BrandingOptions` لذلك اسم المنتج والألوان والرموز من الموزع تنقل إلى
التطبيق المثبت: `name`/`short_name` من `ProductName` و`description` و`theme_color` من `AppBarColor` و
`background_color` من `BackgroundColor` و`display: standalone` ومجموعة الأيقونات (incl. **maskable**
512 لأيقونة Android نظيفة). مجهول — يجب أن يعمل موجه التثبيت قبل تسجيل الدخول.

### عامل الخدمة

app-shell فقط. إنه **أبدًا** لا يعترض دائرة Blazor (`/_blazor`) والإطار (`/_framework`) أو
مراكز SignalR (`/hubs`) — تلك دائمًا شبكة. التنقلات شبكة-first مع صفحة offline
كـ fallback؛ الأصول الثابتة (`/css` و`/icons` و`/_content`) هي cache-first مع إعادة تقييم الخلفية.
مسجل مع `updateViaCache: 'none'` لذلك يتم تطبيق تحديثات العمل بموثوقية. الأجهزة النسخ
(`cmind-shell-v<n>`) — bump على تغييرات shell.

### iOS

يتجاهل iOS manifest icons/splash، لذا `App.razor` يصدر أيضًا `apple-touch-icon` و
معاملات meta `apple-mobile-web-app-*`. iOS لا يوجد `beforeinstallprompt`؛ يثبت المستخدمون عبر *Add to
Home Screen* من Safari. يتم التقاط `beforeinstallprompt` إلى `window.deferredInstallPrompt` على Chromium/Android
لـ affordance تثبيت مخصص.

## الاختبارات

- **E2E** — `E2ETests/PwaTests.cs`: manifest مقدم مع `application/manifest+json`، رموز غير فارغة incl.
  واحد قابل للنقع و`display: standalone` و`apple-touch-icon` مرتبط وعامل الخدمة يسجل +
  ينشط. تغطي `MobileLayoutTests` / `MobileDialogTests` الـ mobile shell التي تثبت PWA.
