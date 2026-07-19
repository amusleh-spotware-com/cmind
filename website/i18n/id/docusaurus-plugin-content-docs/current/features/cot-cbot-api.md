# API cBot COT

Data Komitmen Pedagang diekspos ke cBots dan klien eksternal melalui API REST terautentikasi, sehingga strategi dapat menarik posisi (posisi neto, % minat terbuka, indeks COT) sebagai input sinyal. Ini menggunakan kembali **mekanisme JWT yang sama dan cakupan `market:read`** seperti API pasar kekuatan mata uang — satu token, satu skema.

## Autentikasi

1. Dalam aplikasi, terbitan klien API data pasar (pemilik) dan berikan cakupan **`market:read`**.
2. Tukarkan id/rahasia klien untuk token pembawa jangka pendek:

   ```http
   POST /api/calendar/v1/token
   Content-Type: application/json

   { "clientId": "…", "clientSecret": "…" }
   ```

   Respons membawa `token`, `expiresAt`, dan `scopes` yang diberikan.
3. Kirim token di setiap panggilan COT:

   ```http
   Authorization: Bearer <token>
   ```

Token yang hilang/tidak valid mengembalikan `401`; token tanpa `market:read` mengembalikan `403`.

## Titik Akhir

Jalur basis `/api/market/v1/cot`. Semua respons adalah JSON.

| Metode & jalur | Tujuan |
|---------------|---------|
| `GET /markets` | Katalog pasar-kontrak yang dilacak. `group` opsional (Fx, Metals, Energy, Agriculture, Softs, Rates, Indices, Crypto) dan kata kunci `q`. |
| `GET /latest?code={code}&kind={kind}&combined={bool}` | Snapshot mingguan terbaru untuk pasar. |
| `GET /history/{code}?kind={kind}&combined={bool}&from={iso}&to={iso}` | Sejarah mingguan di seluruh jendela. |

Parameter:

- `code` — kode pasar kontrak CFTC (mis. `099741` untuk Euro FX; dapatkan dari `/markets`).
- `kind` — `Legacy` (default), `Disaggregated` atau `Tff`.
- `combined` — `true` untuk futures + opsi, `false` (default) untuk hanya futures.
- `asOf` (ISO-8601, opsional) — jangkar point-in-time: hanya laporan publik pada saat itu yang dikembalikan, sehingga backtest tidak melihat antisipasi.

### Contoh

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

## Alat MCP

Model baca yang sama tersedia untuk klien AI sebagai alat MCP: `CotMarkets`, `CotLatest`, `CotHistory`, dan `CotHealth` — masing-masing benar point-in-time melalui `asOf` opsional. Lihat [fitur Komitmen Pedagang](./cot-report.md) untuk gambaran lengkapnya.

## Gating

API berada di belakang gerbang dua tingkat yang sama dengan halaman: `App:Branding:EnableCot` dan `App:Features:Cot`. Dengan salah satu dimatikan setiap rute di bawah `/api/market/v1/cot` mengembalikan `404`.
