# Kalendar REST & API cBot

Kalendar ekonomi didedahkan sebagai **REST API berversi, dijamin JWT, berkadar-terhad** — permukaan integrasi andalan. Mana-mana perkhidmatan luaran, papan pemuka atau cBot mengintegrasikan melaluinya sebagai produk. Ia mempunyai pariti ciri dengan FXStreet Calendar API dan melepasinya: titik-dalam-masa `asOf`, rantaian semakan penuh, justifikasi impak deterministik, analitik keputusan, resolusi negara→simbol, dan matematik blackout yang tidak didedahkan oleh API kalendar lain.

> **Status.** Keselamatan JWT (pengeluaran klien + pertukaran token), gerbang, dan titik akhir baca teras —
> `token`, `events`, `events/{id}`, `history`, `series`, `surprises`, `next`, `blackout`,
> `affected-symbols`, `health` — adalah **dilaksanakan dan diuji integrasi** (auth, penguatkuasaan skop,
> gerbang/white-label 404), tambah **`events/batch`** (multipleks terikat) dan dokumen **`/openapi.json`** yang boleh ditemui,
> **`ETag`/`If-None-Match` 304** pada baca peristiwa/sejarah, dan **menaiktaraf cursor** (`Link: rel="next"`),
> **SSE `stream`** (tekanan langsung `event: release`, disokong poll), **webhook ditandatangani HMAC**
> (`X-CMind-Signature: sha256=…`, berdaftar pemilik, dihantar oleh pekerja yang dikawal konfigurasi daripada watermark bertekun), dan **klien taip yang dihantar** (`CmindCalendarClient`). Keseluruhan permukaan API awam dilaksanankan.

## Keselamatan — JWT

API menggunakan semula mesin token HS256 sedia ada (corak yang sama yang digunakan oleh ejen CtraderCliNode),
bukan skema baharu:

- Admin apl mengeluarkan **Klien API Kalendar** (nama + skop + tamat tempoh). Klien menukar id
  dan rahsia nó pada `POST /api/calendar/v1/token` untuk JWT HS256 singkat
  (`iss=cmind-calendar`, `aud=calendar-api`, `exp` ~15 min, `scope` claim). Hanya JWT singkat itu yang이드
  pada permintaan (`Authorization: Bearer <jwt>`).
- Rahsia klien disimpan **disulit** melalui `ISecretProtector` — tidak pernah teks jelas, tidak pernah dicatat.
- **Skop** (keistimewaan minima): `calendar:read`, `calendar:blackout`, `calendar:surprises`,
  `calendar:stream`. Token cBot biasanya mendapat `read` + `blackout` sahaja.
- Pengesahan `JwtBearer` standard (penerbit, audiens, hayat, kunci tanda; `alg=none` ditolak; herotan jam ketat). Selimit kadar token-sebucket setiap klien + selimit global; `429` dengan `Retry-After`. Semua kegagalan auth diaudit.
- Melumpuhkan klien memberhentikan pengeluaran token masa hadapan serta-merta; hayat JWT singkat mengikat token yang bocor. Seluruh pepohon `/api/calendar/**` mengembalikan `404` apabila ciri dilumpuhkan.

## Konvensyen

- **Laluan asas & versi:** `/api/calendar/v1/...` (versian URL; perubahan aditif tidak naik).
- **Format:** JSON; UTC serta merta RFC 3339 tambah `sourceTimeZone` yang nyata; `tz=` pilihan memperuntukkan masa tempatan tanpa kehilangan pengangkud UTC.
- **Pagination:** cursor-based (`cursor`, `limit` ≤ 1000); `next` cursor dalam badan dan pengepala `Link`.
- **Cache:** `ETag` + `If-None-Match`; julat sejarah dapat TTL panjang, akan datang dapat TTL pendek.
- **Ralat:** RFC 7807 `problem+json`, tidak pernah `500` bare.
- **Bacaan merosot:** fault sumber/DB mengembalikan `200` data terbaik-yang-diketahui tambah isyarat `X-Calendar-Freshness`
  / `stale=true` (atau `503 Retry-After` hanya jika benar-benar tidak ada yang diketahui) — cBot memutuskan.

## Titik akhir

| Kaedah & laluan | Tujuan | Param kunci |
|---|---|---|
| `POST /v1/token` | Tukar id+rahsia klien → JWT singkat | badan: `clientId`, `clientSecret` |
| `GET /v1/events` | Peristiwa dalam jendela (akan datang atau sejarah) | `from`,`to`,`countries`,`currencies`,`series`,`minImpact`,`category`,`q`,`asOf`,`cursor`,`limit`,`tz` |
| `GET /v1/events/{id}` | Satu peristiwa: rantaian semakan penuh, keputusan, justifikasi impak, simbol terjejas | `watchlist?`,`asOf?` |
| `GET /v1/events/{id}/revisions` | Sejarah semakan teratur | — |
| `GET /v1/history` | Tarik sejarah mendalam untuk siri (≥10y) | `series`,`from`,`to`,`asOf`,`cursor`,`limit` |
| `GET /v1/series` | Katalog indikator dikesan + kadencia + sumber | `countries`,`currencies`,`q` |
| `GET /v1/surprises` | Siri actual/ramalan/skor-z keputusan sejarah | `series`,`count`/`from,to` |
| `GET /v1/next` | Pelepasan relevan seterusnya untuk simbol (negara→simbol dipetakan) | `symbol`,`minImpact` |
| `GET /v1/blackout` | Adakah simbol dalam jendela tinggi impak sekarang/pada T | `symbol`,`at?`,`minImpact`,`before`,`after` |
| `GET /v1/affected-symbols` | Selesaikan peristiwa → simbol dalam watchlist | `eventId`,`watchlist` |
| `POST /v1/events:batch` | Multipleks beberapa pertanyaan dalam satu pusingan | badan: tatasusunan pertanyaan |
| `GET /v1/stream` (SSE) | Tekanan langsung: pelepasan/semakan/masuk jendela | `currencies`,`minImpact` (skop `calendar:stream`) |
| `POST /v1/webhooks` | Daftar semula callback ditandatangani HMAC untuk pelepasan/semakan/blackout | badan: url, penapis, rahsia |
| `GET /v1/health` | Kesegaran setiap sumber + liputan | — |

## Blackout — penapis berita cBot

`GET /v1/blackout` mengembalikan `{ inBlackout, event, startsAt, endsAt, stale }`. Pada ketidakpastian nó lalai
kepada **jawapan konservatif yang dikonfigurasi** (gagal-ditutup secara lalai: "andaikan dalam-blackout" untuk bot risiko-off), tambah bendera `stale` — jurang data tidak pernah menyalakan perdagangan melalui NFP. Titik akhir ialah baca DB/cache murni dengan masa tamat pelayan yang tegas; tiada fetch asal segerak pada laluan panas.

Klien taip yang dihantar (`Infrastructure.Calendar.CmindCalendarClient`) membalut nó: arahkan `HttpClient` nó ke akar API, panggil `GetTokenAsync(clientId, clientSecret)` sekali, kemudian `GetBlackoutAsync(token, symbol)`
sebelum setiap pesanan — nó **selamat secara konstruksi** (mana-mana bukan-kejayaan atau ralat parse mengembalikan
`InBlackout = true, Stale = true`, jadi jurang data tidak pernah menyalakan perdagangan). cBot memberhentikan sekeliling berita seperti ini:

```csharp
// Pseudokod untuk cTrader cBot menggunakan WebRequest + token klien API Kalendar.
var jwt = CalendarApi.GetToken(clientId, clientSecret);           // POST /v1/token
var res = CalendarApi.Blackout(jwt, symbol: SymbolName,           // GET  /v1/blackout
                               minImpact: "High", before: 15, after: 15);
if (res.InBlackout || res.Stale)                                  // gagal-selamat: basi ⇒ layan sebagai blackout
    return;                                                       // skip new entries in the news window
// ...otherwise proceed to place the order
```

## Titik-dalam-masa untuk backtest

Bekalkan `asOf` pada mana-mana baca untuk mendapatkan kalendar tepat seperti nó berdiri pada saat lalu — actual,
ramalan dan semakan *seperti nó ketika itu*. Kerana bacaan `asOf` murni dan boleh di-cache, backtest yang menghancurkan sejarah mendapat bait yang sama setiap kali, dan peraturan berita yang di-backtest bertindak tepat seperti yang hidup (tiada lihat-masa-depan dari nilai yang disemak).

## Keteguhan untuk Algo yang memanggil

API duduk dalam laluan panas perdagangan, jadi nó tidak pernah membaling ke bot hidup: setiap laluan mengembalikan `problem+json` yang well-formed atau badan merosot yang taip. nó menggunakan primitif keteguhan salinan-perdagangan —
pengendalikan HTTP standard pada setiap klien sumber, pemutus litar domain setiap sumber, pekerja pengingesan singleton yang dijamin lesen dengan sepadan permulaan, dan semakan kesihatan disambungkan ke `/health`. Snip klien taip yang dihantar dilengkapi dengan retry + masa tamat + pemutus litar pra-konfigurasi jadi penulis bot mewarisi keteguhan.

## Sib: AI kekuatan mata wang (`market:read`)

Model baca [kekuatan mata wang AI makro](./currency-strength.md) menunggang **mesin JWT yang sama** —
satu skema, satu kunci tanda, satu selimit kadar — menambah hanya skop `market:read`. Daftar klien API dengan skop itu,
tukar nó untuk token tepat seperti di atas, dan panggilan:

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

Token yang hilang `market:read` mendapat `403`; token tamat/tamper mendapat `401`. Titik akhir gerbang pada bendera ciri AI dan dilayan di bawah `/api/market/v1` supaya nó kekal bebas dari gerbang ciri kalendar. Pada penghantaran lari/backtest penempatan boleh suntik `CMIND_API_BASEURL` + token `market:read` singkat jadi cBot menghantar balik dengan sifar pendaftaran klien.
