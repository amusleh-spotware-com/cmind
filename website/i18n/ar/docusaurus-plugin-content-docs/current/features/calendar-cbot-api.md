# REST API و cBot API للتقويم

يتم فضح التقويم الاقتصادي كـ **REST API مصدّر ومحمي بـ JWT ومُحدّد معدل** — سطح التكامل الرئيسي. يقوم أي خدمة خارجية أو لوحة معلومات أو cBot بالتكامل ضده كمنتج. لديه تكافؤ ميزات مع FXStreet Calendar API ويتجاوزه: `asOf` نقطة في الوقت، سلاسل المراجعة الكاملة، منطق تأثير حتمي، تحليلات المفاجأة، دقة الدول → الرموز، وحساب التعتيم الذي لا تفضح واجهات برمجة تطبيقات التقويم الأخرى.

> **الوضع.** أمان JWT (إصدار العميل + تبادل الرموز)، والبوابات، والنقاط النهائية الأساسية للقراءة — `token`و`events` و`events/{id}` و`history` و`series` و`surprises` و`next` و`blackout` و`affected-symbols` و`health` — **تم تنفيذها واختبارها متكاملة** (auth، فرض النطاق، feature/white-label 404)، بالإضافة إلى **`events/batch`** (multiplex محدود) ووثيقة **`/openapi.json`** قابلة للاكتشاف، **`ETag`/`If-None-Match` 304** على قراءات الحدث/التاريخ، و **الفهرسة المؤشر ذو الحدود الدنيا** (`Link: rel="next"`), **SSE `stream`** (دفع مباشر `event: release`، backup poll)، **webhooks موقعة HMAC** (`X-CMind-Signature: sha256=…`, تسجيل المالك، المسلمة بواسطة عامل مُوقّف بواسطة علامة مائية مستمرة)، و **العميل الذي تم شحنه بنوع** (`CmindCalendarClient`). يتم تنفيذ سطح API العام الكامل.

## الأمان — JWT

يعيد استخدام API آلية الرموز HS256 الموجودة في المستودع (نفس النمط الذي تستخدمه وكلاء CtraderCliNode)، وليس مخطط جديد:

- يصدر مسؤول التطبيق **عميل Calendar API** (الاسم + النطاقات + الانتهاء الصلاحية). يستبدل العميل معرفه وسره في `POST /api/calendar/v1/token` مقابل **JWT HS256 قصير الأجل** (`iss=cmind-calendar`، `aud=calendar-api`، `exp` ~15 دقيقة، مطالبة `scope`). فقط JWT القصير يركب الطلبات (`Authorization: Bearer <jwt>`).
- يتم تخزين سر العميل **مشفر** عبر `ISecretProtector` — أبداً plaintext، أبداً logged.
- **النطاقات** (أقل امتياز): `calendar:read`، `calendar:blackout`، `calendar:surprises`، `calendar:stream`. عادة ما يحصل رمز cBot على `read` + `blackout` فقط.
- التحقق من صحة `JwtBearer` القياسي (المُصدّر والجمهور والعمر والمفتاح التوقيع؛ يُرفض `alg=none`؛ انحراف الساعة الضيقة). حد أقصى لرمز قائمة الانتظار لكل عميل + محدود عالمي؛ `429` مع `Retry-After`. يتم تدقيق جميع إخفاقات المصادقة.
- يوقف العميل إصدار الرموز المستقبلية على الفور؛ يحد عمر JWT القصير من رمز مسرب. تُرجع الشجرة الكاملة `/api/calendar/**` `404` عند تعطيل الميزة.

## الاتفاقيات

- **مسار أساسي وإصدار:** `/api/calendar/v1/...` (URL-versioned؛ التغييرات الإضافية لا تحطم).
- **تنسيق:** JSON؛ RFC 3339 UTC instants بالإضافة إلى `sourceTimeZone` صريح؛ `tz=` اختياري يعرض وقت محلي مريح بدون فقدان ربط UTC.
- **الفهرسة:** استنادة إلى المؤشر (`cursor`، `limit` ≤ 1000)؛ مؤشر `next` في الجسم ورأس `Link`.
- **التخزين المؤقت:** `ETag` + `If-None-Match`؛ النطاقات التاريخية تحصل على TTL طويل، قادمة قصيرة.
- **الأخطاء:** RFC 7807 `problem+json`، لا تُرجع `500` عارية.
- **القراءات المتدهورة:** خطأ مصدر/قاعدة بيانات يرجع `200` أفضل بيانات معروفة بالإضافة إلى إشارة `X-Calendar-Freshness` / `stale=true` (أو `503 Retry-After` فقط إذا كان لا شيء معروف حقاً) — يقرر cBot.

## نقاط النهاية

| الطريقة والمسار | الغرض | المعاملات الرئيسية |
|---|---|---|
| `POST /v1/token` | استبدال معرف العميل + السر → JWT قصير | الجسم: `clientId`، `clientSecret` |
| `GET /v1/events` | الأحداث في نافذة (قادمة أو تاريخية) | `from`,`to`,`countries`,`currencies`,`series`,`minImpact`,`category`,`q`,`asOf`,`cursor`,`limit`,`tz` |
| `GET /v1/events/{id}` | حدث واحد: سلسلة مراجعة كاملة، المفاجأة، نسبة التأثير، الرموز المتأثرة | `watchlist?`,`asOf?` |
| `GET /v1/events/{id}/revisions` | التاريخ المراجعة المرتب | — |
| `GET /v1/history` | سحب تاريخي عميق لسلسلة (≥10 سنوات) | `series`,`from`,`to`,`asOf`,`cursor`,`limit` |
| `GET /v1/series` | الفهرس من المؤشرات المتتبعة + إيقاع + المصدر | `countries`,`currencies`,`q` |
| `GET /v1/surprises` | السلسلة الفعلية/التنبؤية/المفاجأة z-score التاريخية | `series`,`count`/`from,to` |
| `GET /v1/next` | الإفراج الذي قادم عن الرمز (مدولة الدولة → الرمز) | `symbol`,`minImpact` |
| `GET /v1/blackout` | هل الرمز داخل نافذة عالية التأثير الآن/عند T | `symbol`,`at?`,`minImpact`,`before`,`after` |
| `GET /v1/affected-symbols` | حل حدث → الرموز في قائمة المراقبة | `eventId`,`watchlist` |
| `POST /v1/events:batch` | معدّدة عدة استعلامات في رحلة واحدة | الجسم: مصفوفة الاستعلامات |
| `GET /v1/stream` (SSE) | دفع مباشر: الإفراجات/المراجعات/نافذة-إدخال | `currencies`,`minImpact` (نطاق `calendar:stream`) |
| `POST /v1/webhooks` | تسجيل رد اتصال موقع HMAC للإفراج/المراجعة/التعتيم | الجسم: url، المرشحات، السر |
| `GET /v1/health` | طزاجة لكل مصدر + التغطية | — |

## التعتيم — مرشح الأخبار cBot

يرجع `GET /v1/blackout` `{ inBlackout, event, startsAt, endsAt, stale }`. على عدم اليقين، الافتراضي إلى **الإجابة المحافظة المجهزة** (fail-closed افتراضياً: "افترض في التعتيم" للبوتات التي تقلل المخاطر)، بالإضافة إلى علم `stale` — لا تخضع فجوة البيانات أبداً للتداول عبر NFP. نقطة النهاية هي قراءة خالصة من قاعدة البيانات/الذاكرة المؤقتة مع انتظار الخادم الثابت؛ لا يوجد جلب أصلي متزامن على المسار الساخن.

عميل مُصدّر (Infrastructure.Calendar.CmindCalendarClient`) يلف هذا: وجّه `HttpClient` الخاص به إلى جذر API، استدعي `GetTokenAsync(clientId, clientSecret)` مرة واحدة، ثم `GetBlackoutAsync(token, symbol)` قبل كل أمر — إنه **آمن-فشل بنية** (أي غير نجاح أو خطأ تحليل يرجع `InBlackout = true, Stale = true`، لذلك فجوة البيانات لا تخضر للتداول). يوقف cBot حول الأخبار مثل هذا:

```csharp
// pseudocode لـ cBot cTrader يستخدم WebRequest + عميل Calendar API.
var jwt = CalendarApi.GetToken(clientId, clientSecret);           // POST /v1/token
var res = CalendarApi.Blackout(jwt, symbol: SymbolName,           // GET  /v1/blackout
                               minImpact: "High", before: 15, after: 15);
if (res.InBlackout || res.Stale)                                  // fail-safe: stale ⇒ treat as blackout
    return;                                                       // skip new entries in the news window
// ...otherwise proceed to place the order
```

## نقطة في الوقت للاختبارات الخلفية

مرر `asOf` على أي قراءة للحصول على التقويم بالضبط كما كان في لحظة الماضي — الحقائق والتنبؤات والمراجعات *كما كانت آنذاك*. لأن قراءات `asOf` نقيّة وقابلة للتخزين المؤقت، فإن backtest المحاصر للتاريخ يحصل على نفس البايتات في كل مرة، وسيعمل القاعدة الإخبارية المختبرة تماماً مثل الحي (لا نظر للأمام من القيم المراجعة).

## المرونة لمستدعي خوارزمية

API الجلوس في مسار تجاري ساخن، لذا لا يرمي أبداً في bot حي: كل مسار يرجع `problem+json` بصيغة جيدة أو جسم متدهور مُنتَج. يعيد استخدام البدائيات المرونة من نسخ التجارة — معالج المرونة HTTP القياسي على كل عميل مصدر، قاطع دائرة نطاق لكل مصدر، عامل بدء استقبال مفرد محمي بـ lease مع المصالحة عند بدء التشغيل، وفحوصات صحة مربوطة في `/health`. مقتطف العميل المصدر يأتي مع إعادة محاولة + انتظار + قاطع دائرة مُجهّز مسبقاً لذا يرث مؤلفو bot المرونة.

## التوأم: قوة العملات الذكية (`market:read`)

يركب [قوة العملة الذكية AI macro](./currency-strength.md) نفس **نفس** آلية JWT — مخطط واحد، سر توقيع واحد، محدود معدل واحد — إضافة فقط نطاق `market:read`. سجل عميل API بهذا النطاق، استبدله برمز بالضبط كما هو أعلاه، و استدعي:

```
GET /api/market/v1/currency-strength/latest?horizon=3M&tier=Majors
GET /api/market/v1/currency-strength/history?days=30
GET /api/market/v1/currency-strength/pair/EUR/USD?horizon=3M
```

```csharp
// الحصول على رمز عبر POST /api/calendar/v1/token أعلاه، ثم:
http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
var view = await http.GetFromJsonAsync<JsonElement>(
    baseUrl + "/api/market/v1/currency-strength/latest?horizon=3M");
// view.ranking[], view.forecasts[], view.pairs[] (bias/conviction), view.narrative
```

يحصل الرمز المفقود `market:read` على `403`؛ يحصل الرمز المنتهي/المُشوّه على `401`. يتم بوابة نقاط النهاية على علم ميزة AI وتُقدّم تحت `/api/market/v1` لذا تبقى مستقلة عن بوابة ميزة التقويم. في تخطيط التشغيل/الاختبار الخلفي قد يحقن النشر `CMIND_API_BASEURL` + رمز قصير الأجل `market:read` حتى cBot يستدعي بدون تسجيل عميل صفر.
