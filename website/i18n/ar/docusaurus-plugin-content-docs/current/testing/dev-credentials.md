---
description: "جميع بيانات الاعتماد التي تحتاجها مجموعات الاختبار تعيش في ملف واحد gitignored: secrets/dev-credentials.local.json. انسخ النموذج المرتكب وملء ما لديك"
---

# بيانات اعتماد تطوير — ملف واحد لكل اختبار

جميع بيانات الاعتماد التي تحتاجها مجموعات الاختبار تعيش في ملف واحد gitignored:
`secrets/dev-credentials.local.json`. انسخ النموذج المرتكب وملء ما لديك
— كل قيمة اختيارية واختبارات التي تحتاج قيمة مفقودة تخطي بنظافة.

```bash
mkdir -p secrets
cp dev-credentials.example.json secrets/dev-credentials.local.json
# edit secrets/dev-credentials.local.json
```

## ما يقرأه كل طبقة اختبار

| الطبقة | الحاجة | من |
|------|-------|------|
| **الوحدة** (`tests/UnitTests`) | لا شيء | — حتمي وبدون أسرار وبدون شبكة |
| **التكامل** (`tests/IntegrationTests`) | Postgres | Testcontainers (Docker) — auto |
| **نسخ حي** (`tests/IntegrationTests/CopyLive`) | OpenAPI app + token cache | `OpenApi.App` و`OpenApi.Tokens` |
| **E2E onboarding** (`tests/E2ETests/CopyLive`) | OpenAPI app + cID logins | `OpenApi.App` و`OpenApi.Cids` |
| **E2E real run/backtest** (`CBotRealRunBacktestTests`) | login cID + حساب **demo** | `OpenApi.Cids[].{Username,Password,Accounts}` |
| **ميزات AI** | مفتاح Anthropic | `Ai.ApiKey` (unset ⇒ ميزات AI ترجع معطلة والتطبيق لا يزال يعمل) |
| **Live economic-calendar sources** (`tests/IntegrationTests/Calendar/CalendarSourceLiveTests`) | FRED / BLS API keys | `Calendar.FredApiKey`, `Calendar.BlsApiKey` (unset ⇒ that source's live test skips; the keyless central-bank schedule still works) |

## المخطط

انظر `dev-credentials.example.json` في جذر الريبو. الأقسام:

- `OpenApi.App` — `{ ClientId, ClientSecret }` من تطبيق cTrader Open API.
- `OpenApi.Cids` — تسجيلات دخول cTrader ID المستخدمة بواسطة onboarding OAuth بدون رأس. كل إدخال أيضًا
  ينقل صفيف **`Accounts`** — أرقام حساب التداول cTrader (رقم تسجيل الدخول/الحساب
  على سبيل المثال `3635817`) تحت هذا cID التي يُسمح لبنية البنية التحتية للاختبار ربط
  في التطبيق والقيادة. `CBotRealRunBacktestTests` يقرأ الإدخال الأول الذي يحتوي على صفيف `Accounts` غير فارغ
  ويضيف cID + الحساب إلى التطبيق ثم يشغل بالفعل ويتراجع cBot عليه. **ضع فقط
  أرقام حساب العرض هنا** — أبدًا حساب حي؛ اختبارات التشغيل/المحاكاة تضع أوامر حقيقية على
  مهما كان الحساب الذي تدرجه. `Accounts` الفارغة/المحذوفة ⇒ اختبار التشغيل/المحاكاة الحقيقي ينتقل بنظافة.
- `OpenApi.Tokens` — cache token متعدد cID (إدخال واحد لكل cID مُعاد مع
  رمز المصادقة/الوصول + قائمة الحسابات). كتب تلقائيًا بواسطة onboarding وبواسطة
  خطوة token-refresh؛ نادرًا ما تحرره يدويًا.
- `Owner` — تسجيل دخول مالك الصريح للتطبيق تحت E2E.
- `Database.ConnectionString` — فقط عند الإشارة إلى الاختبارات في Postgres خارجي بدلاً من
  Testcontainers.
- `Ai.ApiKey` — مفتاح Anthropic API لميزات AI.
- `Calendar.FredApiKey` — [FRED](https://fredaccount.stlouisfed.org/apikeys) (St. Louis Fed) API key. The primary economic-calendar value source (interest rates, inflation, employment).
- `Calendar.BlsApiKey` — [BLS](https://data.bls.gov/registrationEngine/) (US Bureau of Labor Statistics) v2 registration key (CPI, PPI, employment, JOLTS). Absent ⇒ the low-quota public tier.

  Both feed the exact `FredSource`/`BlsSource` the ingestion worker uses. With a key present, `CalendarSourceLiveTests` hits the real provider and asserts observations come back; absent, that source's test skips cleanly. The app also reads these at runtime via `App:Calendar:FredApiKey` / `App:Calendar:BlsApiKey` (environment variables override — e.g. `FRED_API_KEY`, `BLS_API_KEY`).

## الأسبقية

1. **متغيرات البيئة** تتجاوز كل شيء (على سبيل المثال `App__OwnerPassword` و`App:Ai:ApiKey`).
2. **`secrets/dev-credentials.local.json`** — الملف الموحد (المفضل).
3. **ملفات الانقسام القديمة** — `openapi-test-app.local.json` و`openapi-cids.local.json` و
   `openapi-tokens.local.json` لا تزال تقرأ عند غياب الملف الموحد لذلك الأجهزة الموجودة
   الحفاظ على العمل. يجب أن تستخدم الإعدادات الجديدة الملف الوحيد.

## الأمان

- `secrets/` و`*.local.json` هي gitignored — لا شيء هنا يتم التعهد به أبدًا.
- اختبارات النسخ الحي ترفض التشغيل مقابل حسابات non-demo (`IsLive` حسابات هي
  تم ترشيحها بواسطة `LiveCopyFixture`). احفظ فقط حسابات العرض في ذاكرة التخزين المؤقت للرموز.
- في حالة الكتلة (Kubernetes) يرتفع ملف تثبيت كـ سر للقراءة فقط؛ refreshes رمز هي
  الاحتفاظ بها في الذاكرة والكتابة الخلفية read-only هي صامتة no-op.
