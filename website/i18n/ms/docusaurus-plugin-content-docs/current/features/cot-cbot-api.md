# COT cBot API

Data Commitment of Traders didedahkan kepada cBot dan pelanggan luar melalui API REST yang disahkan,
jadi strategi boleh menarik kedudukan (kedudukan bersih, % minat terbuka, indeks COT) sebagai input isyarat.
Ia menggunakan semula **mekanisme JWT yang sama dan skop `market:read`** sebagai API pasaran kekuatan mata wang — satu
token, satu skim.

## Authentication

1. Dalam apl, keluarkan pelanggan API data pasaran (pemilik) dan berikan skop **`market:read`**.
2. Tukar id/rahsia pelanggan untuk token pembawa jangka pendek:

   ```http
   POST /api/calendar/v1/token
   Content-Type: application/json

   { "clientId": "…", "clientSecret": "…" }
   ```

   Tindak balas membawa `token`, `expiresAt` dan `scopes` yang diberikan.
3. Hantar token pada setiap panggilan COT:

   ```http
   Authorization: Bearer <token>
   ```

Token hilang/tidak sah mengembalikan `401`; token tanpa `market:read` mengembalikan `403`.

## Endpoints

Laluan asas `/api/market/v1/cot`. Semua tindak balas adalah JSON.

| Method & path | Purpose |
|---------------|---------|
| `GET /markets` | Katalog pasaran kontrak terjejak. Pilihan `group` (Fx, Metals, Energy, Agriculture, Softs, Rates, Indices, Crypto) dan kata kunci `q`. |
| `GET /latest?code={code}&kind={kind}&combined={bool}` | Potret mingguan terkini untuk pasaran. |
| `GET /history/{code}?kind={kind}&combined={bool}&from={iso}&to={iso}` | Sejarah mingguan atas tetingkap. |

Parameters:

- `code` — kod pasaran kontrak CFTC (cth. `099741` untuk Euro FX; dapatkan dari `/markets`).
- `kind` — `Legacy` (default), `Disaggregated` atau `Tff`.
- `combined` — `true` untuk hadapan + opsyen, `false` (default) untuk hadapan sahaja.
- `asOf` (ISO-8601, pilihan) — sauh titik masa: hanya laporan awam pada ketika itu dikembalikan,
  jadi ujian balik tidak melihat lihat ke hadapan.

### Example

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

## MCP tools

Model baca yang sama tersedia untuk pelanggan AI sebagai alat MCP: `CotMarkets`, `CotLatest`, `CotHistory`
dan `CotHealth` — setiap satu betul pada titik masa melalui `asOf` pilihan. Lihat
[ciri Commitment of Traders](./cot-report.md) untuk gambaran penuh.

## Gating

API berada di belakang pintu dua peringkat yang sama dengan halaman: `App:Branding:EnableCot` dan `App:Features:Cot`.
Dengan salah satu mati setiap laluan di bawah `/api/market/v1/cot` mengembalikan `404`.
