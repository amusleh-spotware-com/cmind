# COT cBot API

يتم الكشف عن بيانات Commitment of Traders لـ cBots والعملاء الخارجيين عبر API REST مصرح به،
بحيث يمكن لإستراتيجية سحب التموضع (موضع صافي، % من الفائدة المفتوحة، مؤشر COT) كمدخل إشارة.
تعاد استخدام **نفس آلية JWT ونطاق `market:read`** كما هو الحال مع API سوق قوة العملة — رمز واحد، مخطط واحد.

## المصادقة

1. في التطبيق، أصدر عميل بيانات السوق (المالك) وامنحه نطاق **`market:read`**.
2. استبدل معرف العميل/السر برمز حامل قصير الأجل:

   ```http
   POST /api/calendar/v1/token
   Content-Type: application/json

   { "clientId": "…", "clientSecret": "…" }
   ```

   يحمل الرد `token` و `expiresAt` والنطاقات الممنوحة `scopes`.
3. أرسل الرمز عند كل استدعاء COT:

   ```http
   Authorization: Bearer <token>
   ```

يعيد الرمز المفقود/غير الصحيح `401`؛ رمز بدون `market:read` يعيد `403`.

## نقاط النهاية

المسار الأساسي `/api/market/v1/cot`. جميع الردود JSON.

| الطريقة والمسار | الغرض |
|---------------|---------|
| `GET /markets` | كتالوج السوق ذي العقود المتتبعة. اختياري `group` (Fx, Metals, Energy, Agriculture, Softs, Rates, Indices, Crypto) و `q` كلمة مفتاحية. |
| `GET /latest?code={code}&kind={kind}&combined={bool}` | أحدث لقطة أسبوعية للسوق. |
| `GET /history/{code}?kind={kind}&combined={bool}&from={iso}&to={iso}` | التاريخ الأسبوعي على مدى نافذة. |

المعاملات:

- `code` — رمز سوق عقد CFTC (مثل `099741` لـ Euro FX؛ احصل عليها من `/markets`).
- `kind` — `Legacy` (الافتراضي)، `Disaggregated` أو `Tff`.
- `combined` — `true` للعقود الآجلة + الخيارات، `false` (الافتراضي) للعقود الآجلة وحدها.
- `asOf` (ISO-8601، اختياري) — نقطة زمنية: يتم إرجاع التقارير العامة فقط في تلك اللحظة،
  لذا فإن التحليل الخلفي لا يرى نظراً للأمام.

### مثال

```http
GET /api/market/v1/cot/latest?code=088691&kind=Legacy HTTP/1.1
Authorization: Bearer <token>
```

```json
{
  "contractCode": "088691",
  "marketName": "Gold",
  "kind": "Legacy",
  "combined": false,
  "reportDate": "2024-01-02T00:00:00+00:00",
  "knownAt": "2024-01-05T20:30:00+00:00",
  "openInterest": 450000,
  "cotIndex": 82.4,
  "extreme": "LongExtreme",
  "categories": [
    { "category": "NonCommercial", "long": 250000, "short": 90000, "net": 160000, "longPercentOfOi": 55.5 }
  ]
}
```

## أدوات MCP

نموذج القراءة نفسه متاح لعملاء الذكاء الاصطناعي كأدوات MCP: `CotMarkets`, `CotLatest`, `CotHistory`
و `CotHealth` — كل واحد صحيح نقطة في الزمن عبر `asOf` اختياري. انظر
[ميزة Commitment of Traders](./cot-report.md) للصورة الكاملة.

## الحد

API هي خلف نفس بوابة ذات طبقتين من الصفحة: `App:Branding:EnableCot` و `App:Features:Cot`.
مع أي من معطل كل مسار تحت `/api/market/v1/cot` يعيد `404`.
