---
title: Naptar REST es cBot API
description: A gazdasagi naptar egy verziozott, JWT-biztosított, rate-limited REST API-kent van kitéve - a flagship integration felulet. Bármely kulso szolgalat, dashboard vagy cBot integrálható.
---

# Gazdasagi naptar REST es cBot API

A gazdasagi naptar egy **verziozott, JWT-biztosított, rate-limited REST API**-ként van kitéve - a flagship integration felulet. Bármely kulso szolgalat, dashboard vagy cBot integrálható vele mint termékkel. Van feature parity az FXStreet Calendar API-val és túlmegy rajta: pont-idő `asOf`, teljes revizios lancok, determinisztikus hatas indoklás, meglepetes analytics, orszag-szimbulum feloldás, és blackout matek, amit más naptar API-k nem tesznek ki.

> **Allapot.** A JWT biztonság (kliens kibocsatas + token csere), a gating, és a core olvasasi vegpontok - `token`, `events`, `events/{id}`, `history`, `series`, `surprises`, `next`, `blackout`, `affected-symbols`, `health` - **implementalva es integracio-tesztelve** (auth, scope enforcement, feature/white-label 404), plusz **`events/batch`** (korlatos multiplex) es egy felfedezheto **`/openapi.json`** dokumentum, **`ETag`/`If-None-Match` 304** az event/history olvasasokon, és **keyset cursor pagination** (`Link: rel="next"`), az **SSE `stream`** (elo `event: release` push, poll-backed), **HMAC-alairt webhooks** (`X-CMind-Signature: sha256=...`, tulajdonos altal regisztralva, egy config-gated worker altal kiszolgalva egy perzisztalt watermark-rol), és a szallitott **tipizalt kliens** (`CmindCalendarClient`). A teljes publikus API felulet implementalva.

## Biztonsag - JWT

Az API újrahasználja a repo meglévő HS256 token machinery-jét (ugyanaz a minta, amit a CtraderCliNode ügynökök használnak), nem egy új séma:

- Egy app admin kibocsát egy **Calendar API klienst** (név + scope-ok + lejarat). A kliens becseréli az id és titkát a `POST /api/calendar/v1/token`-nél egy **rövid életű HS256 JWT**-re (`iss=cmind-calendar`, `aud=calendar-api`, `exp` ~15 perc, `scope` claim). Csak a rövid JWT utazik a kéréseken (`Authorization: Bearer <jwt>`).
- A kliens titka **titkositva** tárolva a `ISecretProtector` révén - sosem plaintext, sosem naplózva.
- **Scope-ok** (legkisebb jogosultság): `calendar:read`, `calendar:blackout`, `calendar:surprises`, `calendar:stream`. Egy cBot token tipikusan csak `read` + `blackout`-ot kap.
- Standard `JwtBearer` validáció (issuer, audience, lifetime, signing key; `alg=none` elutasítva; szoros óra eltérés). Per-kliens token-bucket rate limit + global limiter; `429` `Retry-After`-nal. Minden auth hiba naplózva.
- A kliens letiltása azonnal leállítja az új token kibocsátást; a rövid JWT élettartam korlátozza a kiszivárgott token hatását. A teljes `/api/calendar/**` fa `404`-el, amikor a funkció le van tiltva.

## Konvenciók

- **Base path & verziózás:** `/api/calendar/v1/...` (URL-verziózott; additív változtatások nem növelnek).
- **Formátum:** JSON; RFC 3339 UTC pillanatok plusz egy explicit `sourceTimeZone`; opcionalis `tz=` renderel egy kényelmes helyi időt az UTC horgany elvesztése nélkül.
- **Pagination:** cursor-based (`cursor`, `limit` <= 1000); `next` cursor a bodyban és `Link` headerben.
- **Cache-lés:** `ETag` + `If-None-Match`; historikus tartományok hosszú TTL-t kapnak, közelgők rövidet.
- **Hibák:** RFC 7807 `problem+json`, sosem csupasz `500`.
- **Degraded olvasások:** egy forrás/DB hiba `200`-at ad a legjobban ismert adatokkal plusz egy `X-Calendar-Freshness` / `stale=true` jelzéssel (vagy `503 Retry-After` csak ha tényleg semmi sem ismert) - a cBot dönt.

## Végpontok

| Metodus es utvonal | Cel | Fő paraméterek |
|---|---|---|
| `POST /v1/token` | Exchange kliens id+titok → rövid JWT | body: `clientId`, `clientSecret` |
| `GET /v1/events` | Események egy ablakban (közelgő vagy historikus) | `from`,`to`,`countries`,`currencies`,`series`,`minImpact`,`category`,`q`,`asOf`,`cursor`,`limit`,`tz` |
| `GET /v1/events/{id}` | Egy esemény: teljes revizio lanc, meglepetes, hatas indoklás, erintett szimbulumok | `watchlist?`,`asOf?` |
| `GET /v1/events/{id}/revisions` | Rendezett revizios historia | — |
| `GET /v1/history` | Mély historikus pull egy sorozathoz (>=10y) | `series`,`from`,`to`,`asOf`,`cursor`,`limit` |
| `GET /v1/series` | Követett indikátorok katalogusa + cadence + forras | `countries`,`currencies`,`q` |
| `GET /v1/surprises` | Historikus tenyleges/eloregeetes/megrlepetes z-score sorozatok | `series`,`count`/`from,to` |
| `GET /v1/next` | Következő releváns release egy szimbulumhoz (orszag-szimbulum leképezve) | `symbol`,`minImpact` |
| `GET /v1/blackout` | Egy szimbulum egy nagy-hatas ablakon belül van-e most/T-kor | `symbol`,`at?`,`minImpact`,`before`,`after` |
| `GET /v1/affected-symbols` | Egy esemény feloldása → szimbulumok egy watchlistben | `eventId`,`watchlist` |
| `POST /v1/events:batch` | Multiplex néhány lekérdezést egy körben | body: tömb query-k |
| `GET /v1/stream` (SSE) | Elo push: releases/revisions/window-enter | `currencies`,`minImpact` (scope `calendar:stream`) |
| `POST /v1/webhooks` | HMAC-alairt callback regisztrálása release/revision/blackout-ra | body: url, filterek, titok |
| `GET /v1/health` | Per-forrás frisseség + lefedettség | — |

## Blackout - a cBot hir szűrője

`GET /v1/blackout` visszaadja `{ inBlackout, event, startsAt, endsAt, stale }`. Bizonytalanság esetén az **konfigurált konzervatív válaszra** default-ol (fail-closed by default: "feltételezd hogy blackout-on belül" a kockázat-ellenes botoknak), plusz egy `stale` flag - egy adat rés soha nem engedi át a kereskedést az NFP-n. A végpont tiszta DB/cache olvasás egy kemény szerver timeout-pal; nincs szinkron origin fetch a hot path-en.

A szállított tipizált kliens (`Infrastructure.Calendar.CmindCalendarClient`) becsomagolja: irányítsd a `HttpClient`-et az API gyökérre, hívd `GetTokenAsync(clientId, clientSecret)` egyszer, aztán `GetBlackoutAsync(token, symbol)` minden rendelés előtt - **fail-safe by construction** (bármely nem-sikeres vagy parse hiba `InBlackout = true, Stale = true`-t ad vissza, igy egy adat rés soha nem engedi át a kereskedést). Egy cBot szünetelteti a hir around-ot így:

```csharp
// Pseudocode for a cTrader cBot using WebRequest + a Calendar API client token.
var jwt = CalendarApi.GetToken(clientId, clientSecret);           // POST /v1/token
var res = CalendarApi.Blackout(jwt, symbol: SymbolName,           // GET  /v1/blackout
                               minImpact: "High", before: 15, after: 15);
if (res.InBlackout || res.Stale)                                  // fail-safe: stale ⇒ treat as blackout
    return;                                                       // skip new entries in the news window
// ...otherwise proceed to place the order
```

## Pont-idő a backtesztekhez

Passz `asOf` bármely olvasáshoz, hogy a naptár pontosan úgy álljon, ahogy egy mult pillanatban állt - a tenyleges ertekek, elorejelzesek es reviziok *ahogy akkor voltak*. Mivel az `asOf` olvasások tisztek és cache-elhetők, egy backtest, ami a historiat bombázza, minden alkalommal azonos bájtokat kap, és egy backtestelt hir szabaly pontosan úgy viselkedik, mint az élő (nincs look-ahead a revizios ertekekbol).

## Rugalmasság az algo hívóknak

Az API egy kereskedelmi hot path-on ül, igy sosem dob a élő botba: minden útvonal jól-formázott `problem+json`-t vagy egy tipizált degraded body-t ad vissza. Újrahasználja a másolási kereskedés rugalmassági primitívjeit - a standard HTTP rugalmassági kezelőt minden forrás kliensen, egy domain circuit breakert per forrás, egy lease-őrzött singleton ingestion workert indítási egyeztetéssel, és health check-ek bekötve `/health`-be. A szállított tipizált kliens snippett retry + timeout + circuit breaker előre konfigurálva szállítva, igy a bot szerzők öröklik a rugalmasságot.

## Testvér: AI penznem-erosseg (`market:read`)

Az [AI makro penznem-erosseg](./currency-strength.md) read model ugyanazon JWT machinery-n utazik - egy séma, egy signing titok, egy rate-limiter - csak egy `market:read` scope-ot adva hozzá. Regisztrálj egy API klienst ezzel a scope-pal, cseréld be token-re, pontosan úgy, mint fentebb, és hívd:

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

Egy token, ami hiányzik a `market:read`-ből, `403`-at kap; egy lejárt/módosított token `401`-et. A végpontok gated on the AI feature flag és `/api/market/v1` alatt vannak szolgálva, igy függetlenek a naptár feature gate-től. Run/backtest dispatch-nél egy telepites injektálhat `CMIND_API_BASEURL` + egy rövid életű `market:read` tokent, igy egy cBot callback-elhet nulla kliens regisztrációval.
