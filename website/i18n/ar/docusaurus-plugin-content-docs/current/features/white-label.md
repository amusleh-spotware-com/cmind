---
description: "تطبيق إعادة تسمية الموزع — اسم المنتج والشعار والفافيكون والألوان وCSS المخصص — عبر تكوين النشر بدون تغيير الرمز. كل قيمة branding افتراضية ..."
---

# Markup-label branding

تطبيق إعادة تسمية الموزع — اسم المنتج والشعار والفافيكون والألوان وCSS المخصص — عبر تكوين النشر بدون تغيير الرمز. كل قيمة branding **افتراضية للهوية الأسهم**: نشر غير مشفر يبدو نفسه كما هو؛ موزع يتجاوز فقط ما يحتاج.

## نموذج

- `Core.Options.BrandingOptions` — مقيد من `App:Branding`. يستند إلى السلسلة (حافة الإعداد)؛ كل لون يتم التحقق من صحته عند بناء المظهر.
- `Core.Branding.HexColor` — كائن القيمة لون hex CSS (`#RGB` / `#RRGGBB`)، غير قابل للتغيير، التحقق من صحة النفس.
  اللون غير الصحيح يرمي `DomainException` (`domain.branding.color_invalid`) عند بناء المظهر — نشر مشفر يفشل بسرعة عند بدء التشغيل، وليس لعرض لوحة ألوان مكسورة.
- `Web.Components.Theme.Build(BrandingOptions)` — إنتاج موضوع MudBlazor من branding. فقط إدخالات اللوحة المميزة تأتي من الإعداد؛ الطباعة والتخطيط والنبرات السطحية المحايدة تبقى ثابتة حتى يحافظ المنتج على مظهر متماسك عبر الموزعين.
- `Web.Branding.IBrandingThemeProvider` — singleton وبناء المظهر مرة واحدة وإعادة البناء على تغيير الخيارات.
  يتم حقنه بواسطة `MainLayout`/`EmptyLayout` لـ `MudThemeProvider` بواسطة شريط التطبيق لاسم المنتج/الشعار. تقرأ `App.razor` `IOptionsMonitor<AppOptions>` مباشرة لصفحة `<head>` (العنوان والوصف والفافيكون والمظهر-اللون وCSS المخصص).

## التشكيل

```json
{
  "App": {
    "Branding": {
      "ProductName": "AcmeFX",
      "CompanyName": "Acme Markets Ltd",
      "SupportUrl": "https://support.acme.example",
      "Description": "AcmeFX — copy trading and strategy automation.",
      "LogoUrl": "/branding/acme-logo.svg",
      "FaviconUrl": "/branding/acme.ico",
      "PrimaryColor": "#2D7FF9",
      "SecondaryColor": "#1E63C8",
      "AppBarColor": "#0B1220",
      "BackgroundColor": "#0E1525",
      "SurfaceColor": "#161E30",
      "SuccessColor": "#3FB950",
      "ErrorColor": "#F85149",
      "WarningColor": "#D29922",
      "InfoColor": "#2D7FF9",
      "CustomCss": ".mud-appbar { letter-spacing: 1px; }"
    }
  }
}
```

شكل متغير البيئة: `App__Branding__ProductName=AcmeFX` و`App__Branding__PrimaryColor=%232D7FF9`.

| المفتاح | تأثير | الافتراضي |
|-----|--------|---------|
| `ProductName` | نص شريط التطبيق + صفحة `<title>` | `cMind` |
| `LogoUrl` | صورة شعار شريط التطبيق؛ عندما يكون فارغًا، يظهر نص اسم المنتج | *(فارغ)* |
| `FaviconUrl` | `<link rel="icon">` | `favicon.svg` |
| `Description` | `<meta name="description">` | وصف الأسهم |
| `PrimaryColor` / `SecondaryColor` | يؤكد وأيقونة الدرج والأزرار | `#26C281` / `#1FB97A` |
| `AppBarColor` / `BackgroundColor` / `SurfaceColor` | chrome + السطوح؛ `AppBarColor` يقود `<meta theme-color>` + PWA manifest `theme_color`، `BackgroundColor` manifest `background_color` | لوحة الألوان الداكنة |
| `SuccessColor` / `ErrorColor` / `WarningColor` / `InfoColor` | ألوان الحالة | الأسهم |
| `CustomCss` | حقن `<style>` في `<head>` (موثوق النشر) | *(فارغ)* |
| `ShowSiteLink` | عرض رابط الائتمان "Powered by cMind" على لوحة المعلومات | `true` |
| `RequireMfa` | يتطلب من كل مستخدم إعداد المصادقة ثنائية العاملين قبل استخدام التطبيق | `false` |
| `NodesUi` | كم من سطح العقد يشحن: `Full` (list + manual add/delete)، `Monitor` (read-only list وno add/delete)، `Hidden` (no nav وno page وno manual API) | `Full` |
| `RestrictNodesToOwner` | عندما `true` فقط يمكن للمالك رؤية/إدارة العقد؛ وإلا فإن سطح الموظفين الإداريين أو أعلاه يمكن. لا يرى المستخدمون العاديون العقد على أي حال | `false` |

الأصول المشار إليها بواسطة `LogoUrl`/`FaviconUrl` مقدمة من تطبيق الويب `wwwroot` (على سبيل المثال تثبيت مجلد `wwwroot/branding/`) أو أي عنوان URL مطلق.

تم التحقق من `App:Branding` عند بدء التشغيل (`BrandingOptionsValidator`، تشغيل عبر `ValidateOnStart`): يجب أن يكون كل لون hex صالح، `CustomCss` يجب أن لا يحتوي على `<`/`>` (لا يمكن كسر من علامة `<style>`). فشل نشر مشفر في الإقلاع برسالة واضحة وليس لعرض صفحة مكسورة.

## رابط Powered-by

تعرض لوحة المعلومات رابط ائتمان صغير **"Powered by cMind"** يشير إلى موقع توثيق المشروع.
يتم التحكم فيه بواسطة `App:Branding:ShowSiteLink` وهو **`true` افتراضيًا** — نشر غير مشفر يعرضه.
يقوم موزع يشغل مثيل موسوم بالملصق الأبيض بالكامل بتعيين
`App__Branding__ShowSiteLink=false` لإزالته بالكامل.

يتم إصدار الرابط بواسطة مكون لوحة المعلومات ويقرأ العلم من خلال `IBrandingThemeProvider` /
`BrandingOptions`، لذلك يكون التبديل عبارة عن تغيير إعداد فقط (بدون إعادة بناء). انظر
[Markup-label for business](../white-label-for-business.md#the-powered-by-cmind-link) لـ
ملخص واجهة الأعمال.

## قائمة الوسطاء المسموحة

يمكن لنشر الملصق الأبيض تقييد المزودين الذين قد يضيفهم مستخدموهم حسابات التداول — لذلك وسيط
تشغيل cMind لعملائها الخاصة فقط يخدم كتابها الخاصة. تم التكوين تحت `App:Accounts`:

```json
{
  "App": {
    "Accounts": {
      "AllowedBrokers": ["Pepperstone", "IC Markets"]
    }
  }
}
```

شكل متغير البيئة: `App__Accounts__AllowedBrokers__0=Pepperstone`.

**السلوك:**

- **قائمة فارغة (الافتراضي) ⇒ غير مقيد.** كل وسيط مسموح و **لا يعمل التحقق** — ا
  نشر الأسهم لم يتغير تماما.
- **غير فارغ ⇒ مقيد.** cMind يتحقق من كل حساب يحاول المستخدم إضافته مقابل القائمة
  (غير حساس لحالة الأحرف):
  - **Open API (OAuth) link** — يتم الإبلاغ عن اسم الوسيط بشكل حتمي بواسطة cTrader Open API، لذلك
    حساب غير مسموح ببساطة **يتم تخطيه** (الحسابات المسموحة في نفس المنحة لا تزال ترتبط)؛ ال
    تخبر صفحة التفويض المستخدم بالمزودين الذين تم تخطيهم.
  - **Manual cID (username / password)** — الوسيط الذي يكتبه المستخدم هو **ليس** موثوق. cMind **يتحقق**
    وسيط الحساب الحقيقي بتشغيل الحسنة المرسلة broker-probe عبر cTrader CLI (قراءة
    `Account.BrokerName`) ويستمر بهذا الاسم المحقق. يتم رفض وسيط غير مسموح برسالة
    إخطار؛ فشل التحقق (بيانات اعتماد سيئة وبدون عقدة وtimeout) يظهر أيضًا والـ
    الحساب لم يتم إضافته.

**النموذج:**

- `Core.Options.AccountsOptions` — مقيد من `App:Accounts` (`AllowedBrokers` و`BrokerProbeTimeout` و
  `BrokerProbeAlgoPath`).
- `Core.Accounts.BrokerName` — كائن القيمة (محشور وتساوي غير حساس لحالة الأحرف).
- `Core.Accounts.BrokerAllowlist` — `IsRestricted` / `Allows(broker)`؛ فارغ = السماح بالكل. يتم فرضها كـ
  ثابت داخل `CTraderIdAccount.AddTradingAccount` / `LinkOpenApiAccount`
  (`domain.account.broker_not_allowed`).
- `Core.Accounts.IBrokerVerifier` → `Web.Accounts.BrokerVerifier` — يشغل حاوية الاختبار على ويب
  host (التي لديها مقبس Docker)، الذيول السجلات، وتحليل الوسيط عبر
  `Core.Accounts.BrokerProbeOutput`. يتم استدعاؤه فقط عندما تكون القائمة المسموحة مقيدة.

**Broker-probe cBot:** ما قبل البناء `broker-probe.algo` ينضم إلى تطبيق الويب (`src/Web/BrokerProbe/`،
نسخ إلى الإخراج كـ `broker-probe/broker-probe.algo`)، لذا الافتراضي
`App:Accounts:BrokerProbeAlgoPath` يحل out of the box — يتم حل المسار النسبي ضد التطبيق
دليل قاعدة وتم استخدام المسار المطلق كما هو. يعيش المصدر في `tools/broker-probe/`. عندما يكون ال
algo غائب، التحقق من cID اليدوي يفشل المغلق — يمكن لا تزال ترتبط الحسابات تحت قائمة مسموحة مقيدة
عبر مسار Open API، الذي لا يحتاج إلى اختبار.

## قائمة الوسطاء المسموحة — الاختبارات

- **الوحدة** — `UnitTests/Accounts/`: `BrokerName`/`BrokerAllowlist` كائنات القيمة و`BrokerProbeOutput`
  محلل والثابت `CTraderIdAccount` قائمة مسموحة.
- **التكامل** — `IntegrationTests/BrokerAllowlistTests.cs`: نقطة نهاية cID اليدوية مع التحقق الوهمي
  (غير مقيد / تحقق / غير مسموح / فشل التحقق) + رابط Open API يتخطى غير مسموح
  الحسابات. `BrokerVerifierLiveTests.cs` يشغل **الاختبار الحقيقي** عند توفر cID creds + algo
  (تخطي بنظافة وإلا).
- **E2E** — `E2ETests/BrokerAllowlistTests.cs`: نشر مقيد يرفض إضافة يدوية من خلال ال
  واجهة المستخدم الحقيقية وتعرض إخطار "لم تستطع التحقق" (لا صف حساب مضاف).

## رؤية واجهة مستخدم العقد

العقد هي البنية الأساسية التي لا يدير معظم المستأجرين يدويًا — عملاء cTrader CLI
[تسجيل ذاتي و heartbeat](../operations/node-discovery.md)، لذلك يمكن لنشر الملصق الأبيض إخفاء ال
عناصر التحكم اليدوية أو سطح العقد بالكامل والتزال تشغيل كتلة صحية من خلال الاكتشاف التلقائي.
مفتاحان branding config فقط يحكمان هذا:

```json
{
  "App": {
    "Branding": {
      "NodesUi": "Monitor",
      "RestrictNodesToOwner": true
    }
  }
}
```

شكل متغير البيئة: `App__Branding__NodesUi=Hidden` و`App__Branding__RestrictNodesToOwner=true`.

**`NodesUi` — ثلاثة أنماط:**

- **`Full` (الافتراضي)** — المنتج الأسهم: قائمة العقد بالإضافة إلى **عقدة جديدة** اليدوية و **حذف**
  الضوابط. `POST`/`DELETE /api/nodes` عمل.
- **`Monitor`** — سطح للقراءة فقط: قائمة و live stats البقاء لكن إضافة يدوية وحذف هي
  مزالة. تظهر العقد فقط من خلال الاكتشاف التلقائي. `POST`/`DELETE /api/nodes` يعود **404**.
- **`Hidden`** — رابط ملاح العقد والصفحة ذهبت بالكامل والطريق الصفحة يعيد التوجيه إلى ال
  لوحة معلومات؛ إضافة/حذف API اليدوية قبالة. الكتلة الاكتشاف التلقائي فقط.

**`RestrictNodesToOwner`** أرضيات من قد يرى وإدارة العقد. الافتراضي `false` يحتفظ قياسي
**admin-or-above** سطح الموظفين (`AdminOrAbove`)؛ اضبط `true` لجعل **مالك فقط** (`Owner`). على أي حال
**المستخدمون العاديون لا يرون العقد** — هذا يختار فقط بين owner-only والسطح موظفين أوسع.

العقدة **الاكتشاف التلقائي لا يتأثر بكل المفاتيح**: عنوان URL مسجل ذاتي مجهول `POST /api/nodes/register`
+ heartbeat نقطة النهاية تعمل دائمًا، لذلك نشر `Hidden`/`Monitor` لا يزال يعمل
تلقائيا نموهم الكتلة.

**النموذج:**

- `Core.Nodes.NodesUiMode` — `Full` / `Monitor` / `Hidden`.
- `Core.Nodes.NodesUiAccess` — مصدر الحقيقة الوحيد يؤلف وضع + owner-restriction:
  `IsPageVisible` و`AllowsManualManagement` و`RequiredPolicy(restrictToOwner)`. حوار
  (`NavMenu.razor`)والصفحة (`Pages/Nodes.razor`) والنقاط (`NodeEndpoints`) جميعها قراءة حتى
  واجهة المستخدم وAPI لا يمكن أن تختلف.
- `Core.Options.BrandingOptions.NodesUi` / `.RestrictNodesToOwner` — مقيد من `App:Branding`.

## رؤية واجهة مستخدم العقد — الاختبارات

- **الوحدة** — `UnitTests/Nodes/NodesUiAccessTests.cs`: رؤية الصفحة وإدارة يدوية و
  حل السياسة المطلوبة عبر كل وضع + branding افتراضي.
- **التكامل** — `IntegrationTests/NodeUiGatingTests.cs`: أكثر HTTP الحقيقية + Postgres — `Full` يسمح أ
  إضافة يدوية و`Monitor`/`Hidden` 404 add و delete و`RestrictNodesToOwner` يحظر إدارة بينما ال
  المالك لا يزال يقرأ القائمة.
- **E2E** — `E2ETests/NodesUiTests.cs` (الافتراضي `Full`: رابط ملاح + صفحة + زر عقدة جديدة تعرض) و
  `E2ETests/NodesHiddenTests.cs` (`Hidden`: رابط ملاح ذهب و`/nodes` redirects).

## رموز التصميم (متغيرات CSS)

يصل Branding أيضًا إلى **الخاص به** stylesheet + مكونات مخصصة للتطبيق وليس فقط MudBlazor. `Web.Branding.BrandingCss.BuildRootVariables(BrandingOptions)` تنبت لوحة الألوان المميزة كخصائص CSS المخصصة على `:root` (`--app-primary` و`--app-primary-hover` و`--app-surface` و`--app-appbar` و`--app-success`/`--app-error`/`--app-warning`/`--app-info` و…)، حقن في `App.razor` مباشرة بعد `site.css`. `site.css` وكل مكون قراءة `var(--app-*)` — **بدون ألوان hardcoded** — لذلك لوحة موزع تتدفق في كل مكان (بطل تسجيل الدخول وشريط التنقل السفلي وتلميحات المساعدة وصفحة غير متصلة) مجاني. نبرات السطح المحايد الافتراضي في `site.css :root`؛ `CustomCss` (حقن آخر) يمكن أن يتجاوز أي رمز. انظر [ui-guidelines.md](../ui-guidelines.md) §2.

## PWA مستقة

التطبيق القابل للتثبيت مستقة أيضًا — نقطة نهاية Manifest (`/manifest.webmanifest`) مبنية من `BrandingOptions` (`ProductName` → `name`/`short_name` و`Description` و`AppBarColor`/`BackgroundColor` → theme/background). انظر [pwa.md](pwa.md).

## الاختبارات

- **الوحدة** — `UnitTests/Branding/HexColorTests.cs`: التحقق من صحة hex الصحيح/غير الصحيح.
- **التكامل** — `IntegrationTests/ThemeBuildTests.cs`: خرائط الألوان إلى لوحة الألوان، اللون غير الصحيح يرمي؛
  `IntegrationTests/BrandingHttpTests.cs`: `ProductName`/description/theme-colour المخصص يعرض في صفحة مقدمة `<head>` (WebApplicationFactory + Postgres)، والقيم الافتراضية الحفاظ على اسم الأسهم.
- **E2E** — `E2ETests/BrandingTests.cs`: اسم المنتج المميز يعرض في شريط التطبيق في المتصفح الحقيقي.
