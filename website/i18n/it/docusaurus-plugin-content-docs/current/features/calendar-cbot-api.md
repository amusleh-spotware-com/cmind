---
description: "Calendar REST & cBot API — il calendario economico è esposto come API REST versionata, JWT-secured, rate-limited — l'integration surface flagship."
---

# Calendar REST & cBot API

Il calendario economico è esposto come **API REST versionata, JWT-secured, rate-limited** — la flagship
integration surface. Qualsiasi servizio esterno, dashboard o cBot si integra contro di essa come un
prodotto. Ha feature parity con la FXStreet Calendar API e la supera: `asOf` point-in-time, catene
complete di revisioni, rationale impatto deterministica, analytics sorprese, risoluzione
country→symbol, e blackout math che altre calendar API non espongono.

> **Status.** La sicurezza JWT (client issuance + token exchange), il gating, e gli endpoint read
> core — `token`, `events`, `events/{id}`, `history`, `series`, `surprises`, `next`, `blackout`,
> `affected-symbols`, `health` — sono **implementati e integration-tested** (auth, scope enforcement,
> feature/white-label 404), più **`events/batch`** (bounded multiplex) e un **`/openapi.json`**
> document scopribile, **`ETag`/`If-None-Match` 304** sulle letture event/history, e
> **keyset cursor pagination** (`Link: rel="next"`), lo **SSE `stream`** (live `event: release` push,
> poll-backed), **webhook con firma HMAC** (`X-CMind-Signature: sha256=…`, owner-registered, deliverati da un
> worker gated da config su un watermark persistito), e il **typed client** spedito
> (`CmindCalendarClient`). L'intera superficie API pubblica è implementata.

## Security — JWT

L'API riutilizza la machinery token HS256 esistente del repo (lo stesso pattern che usano gli agenti
CtraderCliNode), non un nuovo scheme:

- Un admin app emette un **Calendar API client** (nome + scopes + scadenza). Il client scambia il suo id
  e secret a `POST /api/calendar/v1/token` per un **short-lived HS256 JWT**
  (`iss=cmind-calendar`, `aud=calendar-api`, `exp` ~15 min, `scope` claim). Solo il JWT short viaggia
  sulle richieste (`Authorization: Bearer <jwt>`).
- Il client secret è memorizzato **crittografato** via `ISecretProtector` — mai plaintext, mai loggato.
- **Scopes** (least-privilege): `calendar:read`, `calendar:blackout`, `calendar:surprises`,
  `calendar:stream`. Un token cBot tipicamente ottiene solo `read` + `blackout`.
- Validazione `JwtBearer` standard (issuer, audience, lifetime, signing key; `alg=none` rifiutato; tight
  clock skew). Rate limit per-client token-bucket + limiter globale; `429` con `Retry-After`. Tutti gli auth
  failure sono auditati.
- Disabilitare il client ferma immediatamente la futura emissione di token; il lifetime del JWT short
  limita un token leak. L'intero albero `/api/calendar/**` fa `404` quando la feature è disabilitata.

## Conventions

- **Base path & versioning:** `/api/calendar/v1/...` (URL-versioned; cambiamenti additivi non fanno bump).
- **Format:** JSON; istanti UTC RFC 3339 più un `sourceTimeZone` esplicito; opzionale `tz=` renderizza un
  orario locale di convenienza senza perdere l'ancoraggio UTC.
- **Pagination:** cursor-based (`cursor`, `limit` ≤ 1000); `next` cursor nel body e header `Link`.
- **Caching:** `ETag` + `If-None-Match`; range storici hanno un TTL lungo, quelli imminenti uno corto.
- **Errors:** RFC 7807 `problem+json`, mai un naked `500`.
- **Degraded reads:** un fault source/DB restituisce `200` dati best-known più un `X-Calendar-Freshness`
  / `stale=true` signal (o `503 Retry-After` solo se veramente non si sa nulla) — decide il cBot.

## Endpoint

| Method & path | Scopo | Key params |
|---|---|---|
| `POST /v1/token` | Scambia client id+secret → JWT short | body: `clientId`, `clientSecret` |
| `GET /v1/events` | Eventi in una finestra (imminenti o storici) | `from`,`to`,`countries`,`currencies`,`series`,`minImpact`,`category`,`q`,`asOf`,`cursor`,`limit`,`tz` |
| `GET /v1/events/{id}` | Un evento: catena revisione completa, sorpresa, rationale impatto, simboli affected | `watchlist?`,`asOf?` |
| `GET /v1/events/{id}/revisions` | Storia ordinata delle revisioni | — |
| `GET /v1/history` | Pull storico profondo per una series (≥10y) | `series`,`from`,`to`,`asOf`,`cursor`,`limit` |
| `GET /v1/series` | Catalogo degli indicatori tracciati + cadence + source | `countries`,`currencies`,`q` |
| `GET /v1/surprises` | Serie storica z-score actual/forecast/surprise | `series`,`count`/`from,to` |
| `GET /v1/next` | Prossima release rilevante per un simbolo (country→symbol mappato) | `symbol`,`minImpact` |
| `GET /v1/blackout` | Se un simbolo è dentro una finestra high-impact ora/at T | `symbol`,`at?`,`minImpact`,`before`,`after` |
| `GET /v1/affected-symbols` | Risolve un evento → simboli in una watchlist | `eventId`,`watchlist` |
| `POST /v1/events:batch` | Multiplex diverse query in un round-trip | body: array di query |
| `GET /v1/stream` (SSE) | Push live: releases/revisions/window-enter | `currencies`,`minImpact` (scope `calendar:stream`) |
| `POST /v1/webhooks` | Registra un callback con firma HMAC per release/revision/blackout | body: url, filters, secret |
| `GET /v1/health` | Freschezza per-source + copertura | — |

## Blackout — il cBot news filter

`GET /v1/blackout` restituisce `{ inBlackout, event, startsAt, endsAt, stale }`. In incertezza default
alla **risposta conservativa configurata** (fail-closed per default: "assume in-blackout" per bot
risk-off), più un flag `stale` — un gap di dati non green-light mai il trading attraverso NFP.
L'endpoint è una pura lettura DB/cache con un hard server timeout; non c'è fetch sincrono dell'origine
sul hot path.

Un typed client spedito (`Infrastructure.Calendar.CmindCalendarClient`) racchiude questo: punta il suo
`HttpClient` alla root API, chiama `GetTokenAsync(clientId, clientSecret)` una volta, poi
`GetBlackoutAsync(token, symbol)` prima di ogni ordine — è **fail-safe by construction** (qualsiasi
non-success o parse error restituisce `InBlackout = true, Stale = true`, così un gap di dati non
green-light mai il trading). Un cBot pausa attorno alle notizie così:

```csharp
// Pseudocode per un cTrader cBot usando WebRequest + un token Calendar API client.
var jwt = CalendarApi.GetToken(clientId, clientSecret);           // POST /v1/token
var res = CalendarApi.Blackout(jwt, symbol: SymbolName,           // GET  /v1/blackout
                               minImpact: "High", before: 15, after: 15);
if (res.InBlackout || res.Stale)                                  // fail-safe: stale ⇒ treat as blackout
    return;                                                       // skip new entries in the news window
// ...otherwise proceed to place the order
```

## Point-in-time per backtest

Passa `asOf` su qualsiasi read per ottenere il calendario esattamente come era a un istante passato —
gli actual, forecast e revision *as they were then*. Perché le letture `asOf` sono pure e cacheable, un
backtest che martella la history ottiene bytes identici ogni volta, e una regola news backtested si
comporta esattamente come quella live (no look-ahead da valori revisionati).

## Resilience per algo caller

L'API siede in un trading hot path, quindi non lancia mai in un bot live: ogni path restituisce un
ben-formato `problem+json` o un body degraded typed. Riutilizza le primitive di resilience del
copy-trading — l'HTTP resilience handler standard su ogni source client, un domain circuit breaker per
source, un lease-guarded singleton ingestion worker con riconciliazione all'avvio, e health checks
cablat in `/health`. Lo typed client spedito viene con retry + timeout + circuit-breaker preconfigurati
così i bot author ereditano resilience.

## Sibling: AI currency-strength (`market:read`)

Il read model [AI macro currency-strength](./currency-strength.md) cavalca la **stessa** machinery JWT —
un scheme, una signing secret, un rate-limiter — aggiungendo solo uno scope `market:read`. Registra un API
client con quello scope, scambialo per un token esattamente come sopra, e chiama:

```
GET /api/market/v1/currency-strength/latest?horizon=3M&tier=Majors
GET /api/market/v1/currency-strength/history?days=30
GET /api/market/v1/currency-strength/pair/EUR/USD?horizon=3M
```

```csharp
// ottieni un token via POST /api/calendar/v1/token come sopra, poi:
http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
var view = await http.GetFromJsonAsync<JsonElement>(
    baseUrl + "/api/market/v1/currency-strength/latest?horizon=3M");
// view.ranking[], view.forecasts[], view.pairs[] (bias/conviction), view.narrative
```

Un token che manca di `market:read` ottiene `403`; un token expired/tampered ottiene `401`. Gli endpoint
sono gated sulla feature flag AI e serviti sotto `/api/market/v1` così restano indipendenti dal feature
gate del calendario. Al run/backtest dispatch un deployment può iniettare `CMIND_API_BASEURL` + un token
short-lived `market:read` così un cBot chiama back con zero registrazione client.
