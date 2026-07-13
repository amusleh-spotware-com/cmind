---
description: "API kalender ekonomi — versi, diamankan JWT, dibatasi laju — permukaan integrasi utama. Paritas dengan FXStreet Calendar API dengan tambahan point-in-time asOf, rantai revisi penuh, justifikasi dampak deterministik, analitik kejutan, resolusi negara→simbol, dan matematika blackout."
---

# Kalender REST & cBot API

Kalender ekonomi disajikan sebagai **API REST berversion, diamankan JWT, dibatasi laju** — permukaan
integrasi utama. API ini memiliki paritas fitur dengan FXStreet Calendar API dan melampauinya: point-in-time
`asOf`, rantai revisi penuh, justifikasi dampak deterministik, analitik kejutan, resolusi
negara→simbol, dan matematika blackout yang tidak diekspos oleh API kalender lain.

> **Status.** Keamanan JWT (penerbitan client + pertukaran token), penggerbang, dan endpoint baca
> inti — `token`, `events`, `events/{id}`, `history`, `series`, `surprises`, `next`, `blackout`,
> `affected-symbols`, `health` — **diimplementasikan dan diuji integrasi** (autentikasi, penegakan
> scope, 404 fitur/white-label), ditambah **`events/batch`** (multiplex terbatas) dan dokumen
> **`/openapi.json`** yang dapat ditemukan, **`ETag`/`If-None-Match` 304** pada baca event/history, dan
> **pagination cursor keyset** (`Link: rel="next"`), **SSE `stream`** (push langsung `event: release`,
> didukung polling), **webhook bertanda HMAC** (`X-CMind-Signature: sha256=…`, terdaftar pemilik,
> dikirim oleh worker yang dikontrol konfigurasi dari watermark persisten), dan **client teretik**
> yang dikirim (`CmindCalendarClient`). Seluruh permukaan API publik diimplementasikan.

## Keamanan — JWT

API ini menggunakan ulang machinery token HS256 yang sudah ada di repo (pola yang sama yang digunakan
CtraderCliNode agents), bukan skema baru:

- Admin app menerbitkan **Calendar API client** (nama + scopes + expiry). Client menukar id
  dan secret-nya di `POST /api/calendar/v1/token` untuk **JWT HS256 berumur pendek**
  (`iss=cmind-calendar`, `aud=calendar-api`, `exp` ~15 menit, claim `scope`). Hanya JWT pendek yang berjalan
  pada request (`Authorization: Bearer <jwt>`).
- Secret client disimpan **terenkripsi** melalui `ISecretProtector` — tidak pernah plaintext, tidak pernah di-log.
- **Scopes** (prinsip least-privilege): `calendar:read`, `calendar:blackout`, `calendar:surprises`,
  `calendar:stream`. Token cBot biasanya hanya mendapat `read` + `blackout`.
- Validasi `JwtBearer` standar (issuer, audience, lifetime, signing key; `alg=none` ditolak; clock
  skew ketat). Rate limit per-client token-bucket + limiter global; `429` dengan `Retry-After`. Semua
  kegagalan auth di-audit.
- Menonaktifkan client segera menghentikan penerbitan token baru; umur JWT pendek membatasi token
  yang bocor. Seluruh pohon `/api/calendar/**` mengembalikan `404` ketika fitur dinonaktifkan.

## Konvensi

- **Base path & versioning:** `/api/calendar/v1/...` (URL-versioned; perubahan aditif tidak me-revision).
- **Format:** JSON; RFC 3339 UTC instants plus `sourceTimeZone` eksplisit; `tz=` opsionalMe-render
  waktu lokal tanpa kehilangan anchor UTC.
- **Pagination:** cursor-based (`cursor`, `limit` ≤ 1000); cursor `next` di body dan header `Link`.
- **Caching:** `ETag` + `If-None-Match`; rentang historis mendapat TTL panjang, yang akan datang TTL pendek.
- **Errors:** RFC 7807 `problem+json`, tidak pernah `500` bare.
- **Baca terdegradasi:** fault sumber/DB mengembalikan `200` data terbaik yang diketahui plus sinyal
  `X-Calendar-Freshness`/`stale=true` (atau `503 Retry-After` hanya jika benar-benar tidak ada yang
  diketahui) — cBot yang memutuskan.

## Endpoint

| Method & path | Tujuan | Params kunci |
|---|---|---|
| `POST /v1/token` | Tukar client id+secret → JWT singkat | body: `clientId`, `clientSecret` |
| `GET /v1/events` | Events dalam jendela (mendatang atau historis) | `from`,`to`,`countries`,`currencies`,`series`,`minImpact`,`category`,`q`,`asOf`,`cursor`,`limit`,`tz` |
| `GET /v1/events/{id}` | Satu event: rantai revisi penuh, kejutan, justifikasi dampak, simbol yang terpengaruh | `watchlist?`,`asOf?` |
| `GET /v1/events/{id}/revisions` | Riwayat revisi berurutan | — |
| `GET /v1/history` | Tarikan historis mendalam untuk seri (≥10y) | `series`,`from`,`to`,`asOf`,`cursor`,`limit` |
| `GET /v1/series` | Katalog indikator yang dilacak + cadence + sumber | `countries`,`currencies`,`q` |
| `GET /v1/surprises` | Seri z-score aktual/forecast/kejutan historis | `series`,`count`/`from,to` |
| `GET /v1/next` | Rilis relevan berikutnya untuk simbol (negara→simbol dipetakan) | `symbol`,`minImpact` |
| `GET /v1/blackout` | Apakah simbol berada dalam jendela dampak tinggi sekarang/pada T | `symbol`,`at?`,`minImpact`,`before`,`after` |
| `GET /v1/affected-symbols` | Resolve event → simbol dalam watchlist | `eventId`,`watchlist` |
| `POST /v1/events:batch` | Multiplex beberapa query dalam satu round-trip | body: array of queries |
| `GET /v1/stream` (SSE) | Push langsung: rilis/revisi/masuk jendela | `currencies`,`minImpact` (scope `calendar:stream`) |
| `POST /v1/webhooks` | Daftarkan callback bertanda HMAC untuk rilis/revisi/blackout | body: url, filters, secret |
| `GET /v1/health` | Kesegaran per-sumber + cakupan | — |

## Blackout — filter berita cBot

`GET /v1/blackout` mengembalikan `{ inBlackout, event, startsAt, endsAt, stale }`. Saat ketidakpastian, default ke **jawaban konservatif terkonfigurasi** (fail-closed secara default: "asumsikan dalam-blackout" untuk bot risk-off), ditambah flag `stale` — celah data tidak pernah mengizinkan trading melalui NFP. Endpoint adalah baca DB/cache murni dengan timeout server yang tegas; tidak ada pengambilan origin sinkron pada hot path.

Client teretik yang dikirim (`Infrastructure.Calendar.CmindCalendarClient`) membungkus ini: arahkan `HttpClient`-nya ke root API, panggil `GetTokenAsync(clientId, clientSecret)` sekali, lalu `GetBlackoutAsync(token, symbol)` sebelum setiap order — **aman-gagal berdasarkan konstruksi** (error non-sukses atau parse apapun Mengembalikan `InBlackout = true, Stale = true`, sehingga celah data tidak pernah mengizinkan trading). cBot menjeda di sekitar berita seperti ini:

```csharp
// Pseudocode untuk cTrader cBot menggunakan WebRequest + token client Calendar API.
var jwt = CalendarApi.GetToken(clientId, clientSecret);           // POST /v1/token
var res = CalendarApi.Blackout(jwt, symbol: SymbolName,           // GET  /v1/blackout
                               minImpact: "High", before: 15, after: 15);
if (res.InBlackout || res.Stale)                                  // fail-safe: stale ⇒ treat as blackout
    return;                                                       // skip new entries in the news window
// ...otherwise proceed to place the order
```

## Point-in-time untuk backtest

Berikan `asOf` pada pembaca untuk mendapatkan kalender persis seperti pada saat tertentu di masa lalu — actuals,
forecasts dan revisi *seperti pada saat itu*. Karena pembaca `asOf` murni dan dapat di-cache, backtest
yang membentuki histori menghasilkan byte yang identik setiap saat, dan aturan berita yang di-backtest
berperilaku persis seperti yang langsung (tidak ada look-ahead dari nilai revisi).

## Ketahanan untuk panggilan algo

API duduk di hot path trading, jadi tidak pernah melempar ke bot langsung: setiap jalur mengembalikan `problem+json` yang dibentuk dengan baik atau body terdegradasi yang diketik. Ini menggunakan ulang primitif ketahanan dari copy-trading — handler ketahanan HTTP standar pada setiap client sumber, pemutus sirkuit domain per sumber, worker ingestion singleton yang dijaga lease dengan rekonsiliasi startup, dan health check yang diperkuat ke `/health`. Cuplikan client teretik yang dikirim dilengkapi dengan retry + timeout + pemutus sirkuit yang telah dikonfigurasi sebelumnya sehingga penulis bot mewarisi ketahanan.

## Sibling: kekuatan mata uang AI (`market:read`)

Model baca [kekuatan mata uang makro AI](./currency-strength.md) menggunakan JWT machinery yang sama — satu skema, satu secret penandatanganan, satu pembatas laju — hanya menambahkan scope `market:read`. Daftarkan client API dengan scope tersebut, tukar dengan token seperti di atas, dan panggil:

```
GET /api/market/v1/currency-strength/latest?horizon=3M&tier=Majors
GET /api/market/v1/currency-strength/history?days=30
GET /api/market/v1/currency-strength/pair/EUR/USD?horizon=3M
```

```csharp
// obtain a token via POST /api/calendar/v1/token as above, then:
http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
var view = await http.GetFromJsonAsync<JsonElement>(
    baseUrl + "/api/market/v1/currency-strength/latest?horizon=3M");
// view.ranking[], view.forecasts[], view.pairs[] (bias/conviction), view.narrative
```

Token yang tidak memiliki `market:read` mendapat `403`; token kadaluwarsa/dimanipulasi mendapat `401`. Endpoint di-gate pada feature flag AI dan dilayani di bawah `/api/market/v1` sehingga tetap independen dari gate fitur kalender. Pada dispatch run/backtest, deployment dapat menyuntikkan `CMIND_API_BASEURL` + token `market:read` berumur pendek sehingga cBot memanggil balik dengan nol registrasi client.
