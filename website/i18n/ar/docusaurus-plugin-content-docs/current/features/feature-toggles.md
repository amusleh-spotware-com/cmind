---
description: "نشر Markup-label نادرًا ما تشحن كل قدرة. تبديلات الميزات دع المشغل يشغل ميزات المنتج الرئيسية على/إيقاف — في وقت النشر عبر الإعداد أو لاحقًا في..."
---

# تبديلات الميزات

نشر Markup-label نادرًا ما تشحن كل قدرة. تبديلات الميزات دع المشغل يشغل ميزات المنتج الرئيسية
على/إيقاف — في وقت النشر عبر الإعداد أو لاحقًا في وقت التشغيل بدون إعادة
نشر. **جميع الميزات تمكين الافتراضي**؛ النشر فقط قوائمها التي تتغير.

## النموذج

- `Core.Features.FeatureFlag` — تعداد من الميزات القابلة للبوابة: `Authoring` و`Backtesting` و`Execution` و
  `CopyTrading` و`Ai` و`PortfolioAgent` و`Alerts` و`PropGuard` و`PropFirm` و`Accounts` و`OpenApi` و`Mcp` و
  `Compliance`. Core admin
  الأسطح (لوحة المعلومات والمستخدمون والعقد والمصادقة) لا تكون أبدًا قابلة للبوابة وليست هنا.
- `Core.Options.FeaturesOptions` — خط الأساس للإعداد والمقيد من `App:Features`. كل ممتلكات
  الافتراضي `true`.
- `Core.Features.IFeatureGate` — يحل الحالة **الفعالة**: خط الأساس للإعداد غير محسوب
  مع override اختياري set-owner في وقت التشغيل. يتم التنفيذ بواسطة `Infrastructure.Features.FeatureGate`،
  ذاكرات التخزين المؤقت يتجاوز بإيجاز (`FeatureSettings.OverrideCacheTtl`)، ويلغي عند التغيير.

يتم تخزين overrides في وقت التشغيل كـ صفوف `AppSetting` مفتاحها `feature.<FeatureFlag>` (قيمة `true`/`false`).
لا صف = "استخدم خط الأساس للإعداد".

## طريقتان لتعطيل ميزة

### 1. تكوين النشر (خط الأساس)

عيّن علم `false` تحت `App:Features`. مثال `appsettings.json`:

```json
{
  "App": {
    "Features": {
      "CopyTrading": false,
      "PropGuard": false
    }
  }
}
```

أو عبر متغيرات env (شرطة تحتية مزدوجة):

```
App__Features__CopyTrading=false
```

بوابات الخط الأساسي **تسجيل البدء** للعمال في الخلفية (`Nodes.AddNodes`) وأدوات MCP
(`Mcp` server)، لذلك ميزة معطلة في الإعداد أبدًا لا تبدأ خدماتها المستضافة ولا تعرض
أدوات MCP الخاصة بها.

### 2. Override في وقت التشغيل (المالك)

يمكن للمالك التعديل على أي ميزة حية من **الإعدادات → الميزات** (`/settings/features`) أو API:

```
GET  /api/features            -> [{ "flag": "CopyTrading", "enabled": true }, ...]   (Owner)
PUT  /api/features/{flag}      body { "enabled": false }  -> set override             (Owner)
PUT  /api/features/{flag}      body { "enabled": null  }  -> clear override (revert)  (Owner)
```

التغييرات في وقت التشغيل تأخذ تأثير فوري لبوابات وقت الطلب (التنقل و API). العمال في الخلفية
وأدوات MCP مسورة عند بدء التشغيل والالتقاط حتى تغيير في وقت التشغيل عند إعادة بدء العملية التالية.

## ما تفرضه كل بوابة

| الطبقة | الآلية | التوقيت |
|-------|-----------|--------|
| HTTP API | `RouteGroupBuilder.RequireFeature(flag)` endpoint filter → `404` عند التعطيل | وقت التشغيل |
| التنقل | `NavMenu` يخفي الروابط عبر `IFeatureGate.IsEnabled` | وقت التشغيل |
| العمال في الخلفية | conditional `AddHostedService` في `Nodes.AddNodes` | بدء التشغيل (تكوين) |
| أدوات MCP | conditional `WithTools<>` في خادم MCP | بدء التشغيل (تكوين) |

الميزة التي وصل إليها رابط عميق أثناء التعطيل تعرض صفحة فارغة — API الخاص بها يعود `404`؛
nav لا يعود يسطح به.

## علم → خريطة السطح

| علم | مجموعات API | Nav | العمال / MCP |
|------|-----------|-----|----------------|
| Authoring | `/api/cbots` و`/api/paramsets` و`/api/builder` | مجموعة cBots → cBots (مجموعات params لكل cBot dialog) | MCP `CBotTools` |
| Backtesting | (يشارك `/api/instances`) | مجموعة cBots → Backtest | — |
| Execution | `/api/instances` | مجموعة cBots → Run | MCP `InstanceTools` |
| CopyTrading | `/api/copy` | Copy Trading | `CopyEngineSupervisor` و`OpenApiTokenRefreshService` و MCP `CopyTools` |
| Ai | `/api/ai` | مجموعة AI → AI؛ الإعدادات → AI (مفتاح) | `AiRiskGuard` و MCP `AiTools` |
| PortfolioAgent | `/api/agent` | مجموعة AI → Portfolio Agent | `PortfolioAgentService` |
| Alerts | `/api/alerts` | مجموعة AI → Alerts | `AlertEvaluator` |
| PropGuard | `/api/prop` | مجموعة Prop → Prop Guard | `PropGuardService` |
| PropFirm | `/api/prop-firm` | مجموعة Prop → Challenges | — |
| Accounts | `/api/ctids` | حسابات التداول | — |
| OpenApi | `/api/openapi` | الإعدادات → Open API | — |
| Mcp | `/api/mcp-keys` | مجموعة AI → MCP Keys | — |
| Compliance | `/api/compliance` | الإعدادات → الشرعية والخصوصية | — |

## الاختبارات

- **الوحدة** — `UnitTests/Features/FeaturesOptionsTests.cs`: الافتراضيات الأساسية والخريطة لكل علم.
- **التكامل** — `IntegrationTests/FeatureGateTests.cs`: خط الأساس للإعداد و override في وقت التشغيل يتفوق على
  إعداد ويثابر كـ `AppSetting` وتصفية يعود إلى الخط الأساسي (Postgres الحقيقي).
- **E2E** — `E2ETests/FeatureToggleTests.cs`: تعطيل `CopyTrading` في وقت التشغيل يخفي رابط nav الخاص بها و
  `404`s `/api/copy`، إعادة تمكين استعيد كلاهما.
