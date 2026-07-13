---
title: Koledar REST in cBot API
description: "Ekonomiski koledar je izpostavljen kot verzioniran, JWT-zavarovan, rate-limited REST API — zastavna integracijska površina."
---

# Koledar REST in cBot API

Ekonomiski koledar je izpostavljen kot **verzioniran, JWT-zavarovan, rate-limited REST API** — zastavna
integracijska površina. Katera koli zunanja storitev, nadzorna plošča ali cBot se integrira proti njemu kot produktu. Ima
feature parity s FXStreet Calendar API in gre čez njega: točkovno-čas `asOf`, polne verige revizij,
deterministična utemeljitev vpliva, presenečenska analitika, država→simbol resolucija in blackout
matematika ki je drugam API ne izpostavijo.

> **Status.** JWT varnost (izdaja odjemalca + zamenjava žetona), vrata in jedrne bralne končne točke — `token`, `events`, `events/{id}`, `history`, `series`, `surprises`, `next`, `blackout`,
> `affected-symbols`, `health` — so **implementirane in integracija-testirane** (avtentikacija, uveljavljanje obsega,
> feature/white-label 404), plus **`events/batch`** (omejeno multipleksiranje) in odkrito
> **`/openapi.json`** dokument, **`ETag`/`If-None-Match` 304** na branju dogodkov/zgodovine, in
> **keyset cursor pagination** (`Link: rel="next"`), SSE **`stream`** (živo `event: release` push,
> poll-backed), **HMAC-podpisane webhooke** (`X-CMind-Signature: sha256=…`, lastnik-registrirani, dostavljeni s
> strani config-gated delavca iz vztrajnega watermarka), in ladjena **tipizirani odjemalec** (`CmindCalendarClient`).
> Polna javna API površina je implementirana.

## Varnost — JWT

API znova uporablja obstoječo HS256 žetonsko mašinerijo (ista pot ki jo uporabljajo CtraderCliNode agenti),
ne nova shema:

- App admin izda **Calendar API odjemalca** (ime + obsegi + potek). Odjemalec zamenja svoj id
  in skrivnost pri `POST /api/calendar/v1/token` za **kratkoživi HS256 JWT**
  (`iss=cmind-calendar`, `aud=calendar-api`, `exp` ~15 min, `scope` claim). Samo kratki JWT je na zahtevkah
  (`Authorization: Bearer <jwt>`).
- Skrivnost odjemalca je shranjena **šifrirana** prek `ISecretProtector` — nikoli besedilno, nikoli logirano.
- **Obsegi** (najmanj privilegij): `calendar:read`, `calendar:blackout`, `calendar:surprises`,
  `calendar:stream`. cBot žeton tipično dobi `read` + `blackout` samo.
- Standardna `JwtBearer` validacija (izdajatelj, občinstvo, življenjska doba, podpisovni ključ; `alg=none` zavrnjen; tesno
  časovno popravek). Per-odjemalčevo žetonsko vedro + globalni limiter; `429` z `Retry-After`. Vsi
  avtentikacijski neuspehi so revizijni.
- Onemogočanje odjemalca takoj ustavi prihodnjo izdajo žetona; kratka življenjska doba JWT omeji razkriti žeton. Cela
  `/api/calendar/**` drevesa `404` ko je funkcija onemogočena.

## Konvencije

- **Bazna pot in verzioniranje:** `/api/calendar/v1/...` (URL-verzionirano; aditivne spremembe ne bumpajo).
- **Format:** JSON; RFC 3339 UTC trenutki plus eksplicitni `sourceTimeZone`; izbirni `tz=` upodobi
  priročno lokalni čas brez izgube UTC sidra.
- **Stranjenje:** cursor-based (`cursor`, `limit` ≤ 1000); `next` cursor v telesu in `Link` header.
- **Caching:** `ETag` + `If-None-Match`; zgodovinski obsegi dobijo dolg TTL, prihodnji krajši.
- **Napake:** RFC 7807 `problem+json`, nikoli gola `500`.
- **Degradirana branja:** napaka vira/DB vrača `200` najbolj znane podatke plus `X-Calendar-Freshness`
  / `stale=true` signal (ali `503 Retry-After` samo če resnično nič ni znano) — cBot odloča.

## Končne točke

| Metoda in pot | Namen | Ključni parametri |
|---|---|---|
| `POST /v1/token` | Zamenjaj id+skrivnost → kratki JWT | telo: `clientId`, `clientSecret` |
| `GET /v1/events` | Dogodki v oknu (prihodnji ali zgodovinski) | `from`,`to`,`countries`,`currencies`,`series`,`minImpact`,`category`,`q`,`asOf`,`cursor`,`limit`,`tz` |
| `GET /v1/events/{id}` | En dogodek: polna veriga revizij, presenečenje, utemeljitev vpliva, prizadeti simboli | `watchlist?`,`asOf?` |
| `GET /v1/events/{id}/revisions` | Urejena zgodovina revizij | — |
| `GET /v1/history` | Globok zgodovinski poteg za serijo (≥10y) | `series`,`from`,`to`,`asOf`,`cursor`,`limit` |
| `GET /v1/series` | Katalog sledenih indikatorjev + cadence + vir | `countries`,`currencies`,`q` |
| `GET /v1/surprises` | Zgodovinska actual/forecast/surprise z-seriesija | `series`,`count`/`from,to` |
| `GET /v1/next` | Naslednja relevantna objava za simbol (država→simbol preslikano) | `symbol`,`minImpact` |
| `GET /v1/blackout` | Ali je simbol znotraj visoko-vplivnega okna zdaj/ob T | `symbol`,`at?`,`minImpact`,`before`,`after` |
| `GET /v1/affected-symbols` | Razreši dogodek → simboli v spremljani | `eventId`,`watchlist` |
| `POST /v1/events:batch` | Multipleksiraj več poizvedb v enem round-tripu | telo: polje poizvedb |
| `GET /v1/stream` (SSE) | Živi push: objave/revizije/vstop-okna | `currencies`,`minImpact` (obseg `calendar:stream`) |
| `POST /v1/webhooks` | Registriraj HMAC-podpisani callback za objavo/revizijo/blackout | telo: url, filtri, skrivnost |
| `GET /v1/health` | Svežina na vir + pokritost | — |

## Blackout — cBot filter novice

`GET /v1/blackout` vrača `{ inBlackout, event, startsAt, endsAt, stale }`. Ob negotovosti privzame
**konfigurirani konzervativni odgovor** (privzeto zaprto napake: "predpostavljeno znotraj blackouta" za tveganje-izključene
bot-e), plus `stale` zastavica — vrzel podatkov nikoli ne izda zelene luči za trgovanje skozi NFP. Končna točka je čista
DB/predpomnilnik branje s trdim strežniškim časovnim minimumom; na vroči poti ni sinhronega origin fetcha.

Ladjena tipizirani odjemalec (`Infrastructure.Calendar.CmindCalendarClient`) ovije to: usmeritev njen `HttpClient`
na API koren, kličite `GetTokenAsync(clientId, clientSecret)` enkrat, nato `GetBlackoutAsync(token, symbol)`
pred vsakim naročilom — je **fail-safe po konstrukciji** (katerakoli neuspeh ali napaka razčlenjevanja vrne
`InBlackout = true, Stale = true`, torej vrzel podatkov nikoli ne izda zelene luči za trgovanje). cBot pavziraj okoli novice takole:

```csharp
// Pseudocode za cTrader cBot ki uporablja WebRequest + Calendar API odjemalčev žeton.
var jwt = CalendarApi.GetToken(clientId, clientSecret);           // POST /v1/token
var res = CalendarApi.Blackout(jwt, symbol: SymbolName,           // GET  /v1/blackout
                               minImpact: "High", before: 15, after: 15);
if (res.InBlackout || res.Stale)                                  // fail-safe: stale ⇒ obravnavaj kot blackout
    return;                                                       // preskoči nova vstopa v oknu novice
// ...drugače nadaljuj s postavitvijo naročila
```

## Točkovno-čas za backteste

Podajte `asOf` na katerem koli branju da dobite koledar natančno kot je stal ob preteklem trenutku — dejanske,
napovedi in revizije *kot so bile takrat*. Ker so `asOf` branje čista in predpomnljiva, backtest ki
tolče zgodovino dobi identične bajte vsakič, in backtestirano pravilo novice se obnaša natančno kot
živo (brez naprej pogleda iz revidiranih vrednosti).

## Odpornost za algo klicatelje

API sedi v trgovalni vroči poti, torej nikoli ne meče v živega bota: vsaka pot vrača
dobro oblikovan `problem+json` ali tipizirano degradirano telo. Znova uporablja kopiraj-trgovalčeve odpornostne primitive —
standardni HTTP odpornostni obravnavalec na vsakem virnem odjemalcu, vezje varovalka domene na vir,
lease-zavarovan singleton ingestion worker s startup uskladitvijo in health checks ožičeni v
`/health`. Ladjeni tipizirani odjemalčev izrezek prihaja s ponovitvijo + časovno omejitvijo + vezjem
varovalko pred-nastavljenimi tako da avtorji bot podedujejo odpornost.

## Sorodno: AI valutna moč (`market:read`)

[AI makro valutna moč](./currency-strength.md) read model jaha **isto** JWT mašinerijo —
ena shema, ena podpisna skrivnost, en limiter — dodaja samo `market:read` obseg. Registrirajte API
odjemalca s tem obsegom, zamenjajte ga za žeton natanko kot zgoraj, in kličite:

```
GET /api/market/v1/currency-strength/latest?horizon=3M&tier=Majors
GET /api/market/v1/currency-strength/history?days=30
GET /api/market/v1/currency-strength/pair/EUR/USD?horizon=3M
```

```csharp
// pridobi žeton prek POST /api/calendar/v1/token kot zgoraj, nato:
http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
var view = await http.GetFromJsonAsync<JsonElement>(
    baseUrl + "/api/market/v1/currency-strength/latest?horizon=3M");
// view.ranking[], view.forecasts[], view.pairs[] (bias/conviction), view.narrative
```

Žeton manjkajoč `market:read` dobi `403`; potekel/tamperiran žeton dobi `401`. Končne točke so na vratih
funkcijske zastavice AI in servirane pod `/api/market/v1` tako da ostanejo neodvisne od koledarja
funkcijskih vrat. Ob run/backtest dispatchu lahko namestitev vbrizga `CMIND_API_BASEURL` + kratkoživi
`market:read` žeton tako da cBot callbacka z ničlo odjemalčeve registracije.
