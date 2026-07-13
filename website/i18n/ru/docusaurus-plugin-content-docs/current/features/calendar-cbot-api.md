---
description: "Economic calendar exposed как versioned, JWT-secured, rate-limited REST API — флагманская integration surface. Feature parity с FXStreet Calendar API плюс point-in-time asOf, полные цепочки пересмотров, deterministic impact rationale."
---

# Calendar REST & cBot API

Economic calendar представлен как **versioned, JWT-secured, rate-limited REST API** — флагманская
integration surface. Любой внешний сервис, dashboard или cBot интегрируется против него как продукт. Имеет
feature parity с FXStreet Calendar API и превосходит: point-in-time `asOf`, полные цепочки пересмотров,
deterministic impact rationale, surprise analytics, country→symbol resolution и blackout
math, который другие calendar APIs не раскрывают.

> **Status.** JWT security (client issuance + token exchange), gating, и core read
> endpoints — `token`, `events`, `events/{id}`, `history`, `series`, `surprises`, `next`, `blackout`,
> `affected-symbols`, `health` — **реализованы и integration-tested** (auth, scope enforcement,
> feature/white-label 404), плюс **`events/batch`** (bounded multiplex) и discoverable
> **`/openapi.json`** документ, **`ETag`/`If-None-Match` 304** на event/history reads, и
> **keyset cursor pagination** (`Link: rel="next"`), **SSE `stream`** (live `event: release` push),
> **HMAC-signed webhooks** (`X-CMind-Signature: sha256=…`, owner-registered), и shipped **typed client**
> (`CmindCalendarClient`). Полная public API surface реализована.

## Security — JWT

API переиспользует существующую HS256 token machinery репо (тот же паттерн что CtraderCliNode agents
используют), не новую схему:

- Админ приложения выпускает **Calendar API client** (name + scopes + expiry). Клиент обменивает свой id
  и secret на `POST /api/calendar/v1/token` для **short-lived HS256 JWT**
  (`iss=cmind-calendar`, `aud=calendar-api`, `exp` ~15 min, `scope` claim). Только short JWT rides
  on requests (`Authorization: Bearer <jwt>`).
- Client secret хранится **encrypted** via `ISecretProtector` — никогда plaintext, никогда logged.
- **Scopes** (least-privilege): `calendar:read`, `calendar:blackout`, `calendar:surprises`,
  `calendar:stream`. cBot токен обычно получает `read` + `blackout` только.
- Standard `JwtBearer` validation (issuer, audience, lifetime, signing key; `alg=none` rejected; tight
  clock skew). Per-client token-bucket rate limit + global limiter; `429` с `Retry-After`. All auth
  failures are audited.
- Disabling клиента останавливает будущую выдачу токенов немедленно; short JWT lifetime bounds a leaked
  token. Всё `/api/calendar/**` дерево `404`s когда feature disabled.

## Соглашения

- **Base path & versioning:** `/api/calendar/v1/...` (URL-versioned; additive changes don't bump).
- **Format:** JSON; RFC 3339 UTC instants плюс explicit `sourceTimeZone`; optional `tz=` renders a
  convenience local time без потери UTC anchor.
- **Pagination:** cursor-based (`cursor`, `limit` ≤ 1000); `next` cursor in body и `Link` header.
- **Caching:** `ETag` + `If-None-Match`; historical ranges получают long TTL, upcoming short one.
- **Errors:** RFC 7807 `problem+json`, никогда bare `500`.
- **Degraded reads:** source/DB fault returns `200` best-known data плюс `X-Calendar-Freshness`
  / `stale=true` signal (или `503 Retry-After` только если вообще ничего не известно) — cBot решает.

## Endpoints

| Метод & путь | Назначение | Ключевые параметры |
|---|---|---|
| `POST /v1/token` | Обмен client id+secret → short JWT | body: `clientId`, `clientSecret` |
| `GET /v1/events` | Events в окне (upcoming или historical) | `from`,`to`,`countries`,`currencies`,`series`,`minImpact`,`category`,`q`,`asOf`,`cursor`,`limit`,`tz` |
| `GET /v1/events/{id}` | Одно событие: full revision chain, surprise, impact rationale, affected symbols | `watchlist?`,`asOf?` |
| `GET /v1/events/{id}/revisions` | Упорядоченная история пересмотров | — |
| `GET /v1/history` | Deep historical pull для серии (≥10y) | `series`,`from`,`to`,`asOf`,`cursor`,`limit` |
| `GET /v1/series` | Каталог отслеживаемых индикаторов + cadence + source | `countries`,`currencies`,`q` |
| `GET /v1/surprises` | Historical actual/forecast/surprise z-score series | `series`,`count`/`from,to` |
| `GET /v1/next` | Следующий релевантный релиз для символа | `symbol`,`minImpact` |
| `GET /v1/blackout` | Находится ли символ внутри high-impact окна сейчас/в T | `symbol`,`at?`,`minImpact`,`before`,`after` |
| `GET /v1/affected-symbols` | Resolve событие → symbols в watchlist | `eventId`,`watchlist` |
| `POST /v1/events:batch` | Multiplex several queries в одном round-trip | body: array of queries |
| `GET /v1/stream` (SSE) | Live push: releases/revisions/window-enter | `currencies`,`minImpact` (scope `calendar:stream`) |
| `POST /v1/webhooks` | Register HMAC-signed callback для release/revision/blackout | body: url, filters, secret |
| `GET /v1/health` | Per-source freshness + coverage | — |

## Blackout — cBot news filter

`GET /v1/blackout` returns `{ inBlackout, event, startsAt, endsAt, stale }`. При неопределённости defaults
к **настроенному консервативному ответу** (fail-closed по умолчанию: "assume in-blackout" для risk-off
bots), плюс `stale` flag — data gap никогда не включает торговлю через NFP. Endpoint это чистый
DB/cache/read with hard server timeout; на hot path нет синхронного origin fetch.

Shipped typed client (`Infrastructure.Calendar.CmindCalendarClient`) wraps this: point its `HttpClient`
at the API root, call `GetTokenAsync(clientId, clientSecret)` once, then `GetBlackoutAsync(token, symbol)`
перед каждым ордером — это **fail-safe by construction** (any non-success или parse error returns
`InBlackout = true, Stale = true`, so data gap никогда не включает торговлю). cBot приостанавливает around news так:

```csharp
// Pseudocode for a cTrader cBot using WebRequest + a Calendar API client token.
var jwt = CalendarApi.GetToken(clientId, clientSecret);           // POST /v1/token
var res = CalendarApi.Blackout(jwt, symbol: SymbolName,           // GET  /v1/blackout
                               minImpact: "High", before: 15, after: 15);
if (res.InBlackout || res.Stale)                                  // fail-safe: stale ⇒ treat as blackout
    return;                                                       // skip new entries in the news window
// ...otherwise proceed to place the order
```

## Point-in-time для бэктестов

Pass `asOf` на любой read чтобы получить календарь exactly как он выглядел в прошлом instant — факты,
forecasts и revisions *as they were then*. Because `asOf` reads are pure and cacheable, a backtest
hammering history получает identical bytes every time, и бэктестнутое news rule ведёт себя exactly like
the live one (no look-ahead от revised values).

## Resilience для algo callers

API sits in trading hot path, поэтому никогда не бросает в live bot: every path returns well-formed
`problem+json` или typed degraded body. Оно переиспользует copy-trading's resilience primitives —
standard HTTP resilience handler on each source client, domain circuit breaker per source, a
lease-guarded singleton ingestion worker with startup reconciliation, и health checks wired into
`/health`. Shipped typed client snippet comes with retry + timeout + circuit-breaker preconfigured
so bot authors inherit resilience.

## Sibling: AI currency-strength (`market:read`)

[AI macro currency-strength](./currency-strength.md) read model rides the **same** JWT machinery —
one scheme, one signing secret, one rate-limiter — adding only a `market:read` scope. Register an API
client with that scope, exchange it for a token exactly as above, и call:

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

Токен без `market:read` получает `403`; expired/tampered token получает `401`. Endpoints gated
on AI feature flag и served under `/api/market/v1` so they stay independent of calendar
feature gate.
