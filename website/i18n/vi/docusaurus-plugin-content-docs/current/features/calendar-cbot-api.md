# Calendar REST & cBot API

Economic calendar được expose như một **versioned, JWT-secured, rate-limited REST API** — flagship
integration surface. Bất kỳ external service, dashboard hoặc cBot nào integrate đối với nó như một product. Nó có
feature parity với FXStreet Calendar API và vượt qua nó: point-in-time `asOf`, full revision
chains, deterministic impact rationale, surprise analytics, country→symbol resolution, và blackout
math mà các calendar API khác không expose.

> **Status.** JWT security (client issuance + token exchange), gating, và core read
> endpoints — `token`, `events`, `events/{id}`, `history`, `series`, `surprises`, `next`, `blackout`,
> `affected-symbols`, `health` — là **implemented và integration-tested** (auth, scope enforcement,
> feature/white-label 404), cộng **`events/batch`** (bounded multiplex) và một discoverable
> **`/openapi.json`** document, **`ETag`/`If-None-Match` 304** on event/history reads, và
> **keyset cursor pagination** (`Link: rel="next"`), SSE **`stream`** (live `event: release` push,
> poll-backed), **HMAC-signed webhooks** (`X-CMind-Signature: sha256=…`, owner-registered, delivered by a
> config-gated worker off a persisted watermark), và shipped **typed client** (`CmindCalendarClient`).
> Full public API surface is implemented.

## Security — JWT

API reuse repo's existing HS256 token machinery (cùng pattern CtraderCliNode agents
use), không phải scheme mới:

- Một app admin issues một **Calendar API client** (name + scopes + expiry). Client exchanges its id
  và secret tại `POST /api/calendar/v1/token` cho một **short-lived HS256 JWT**
  (`iss=cmind-calendar`, `aud=calendar-api`, `exp` ~15 min, `scope` claim). Chỉ short JWT ride
  on requests (`Authorization: Bearer <jwt>`).
- Client secret được store **encrypted** via `ISecretProtector` — không bao giờ plaintext, không bao giờ logged.
- **Scopes** (least-privilege): `calendar:read`, `calendar:blackout`, `calendar:surprises`,
  `calendar:stream`. Một cBot token thường gets `read` + `blackout` only.
- Standard `JwtBearer` validation (issuer, audience, lifetime, signing key; `alg=none` rejected; tight
  clock skew). Per-client token-bucket rate limit + global limiter; `429` with `Retry-After`. All auth
  failures được audit.
- Disabling client stops future token issuance immediately; short JWT lifetime bounds a leaked
  token. Toàn bộ `/api/calendar/**` tree `404`s khi feature disabled.

## Conventions

- **Base path & versioning:** `/api/calendar/v1/...` (URL-versioned; additive changes không bump).
- **Format:** JSON; RFC 3339 UTC instants plus an explicit `sourceTimeZone`; optional `tz=` renders a
  convenience local time without losing the UTC anchor.
- **Pagination:** cursor-based (`cursor`, `limit` ≤ 1000); `next` cursor in body và a `Link` header.
- **Caching:** `ETag` + `If-None-Match`; historical ranges get a long TTL, upcoming a short one.
- **Errors:** RFC 7807 `problem+json`, không bao giờ bare `500`.
- **Degraded reads:** source/DB fault returns `200` best-known data plus an `X-Calendar-Freshness`
  / `stale=true` signal (hoặc `503 Retry-After` chỉ khi thực sự không có gì được biết) — cBot decides.

## Endpoints

| Method & path | Purpose | Key params |
|---|---|---|
| `POST /v1/token` | Exchange client id+secret → short JWT | body: `clientId`, `clientSecret` |
| `GET /v1/events` | Events in a window (upcoming or historical) | `from`,`to`,`countries`,`currencies`,`series`,`minImpact`,`category`,`q`,`asOf`,`cursor`,`limit`,`tz` |
| `GET /v1/events/{id}` | One event: full revision chain, surprise, impact rationale, affected symbols | `watchlist?`,`asOf?` |
| `GET /v1/events/{id}/revisions` | Ordered revision history | — |
| `GET /v1/history` | Deep historical pull for a series (≥10y) | `series`,`from`,`to`,`asOf`,`cursor`,`limit` |
| `GET /v1/series` | Catalog of tracked indicators + cadence + source | `countries`,`currencies`,`q` |
| `GET /v1/surprises` | Historical actual/forecast/surprise z-score series | `series`,`count`/`from,to` |
| `GET /v1/next` | Next relevant release for a symbol (country→symbol mapped) | `symbol`,`minImpact` |
| `GET /v1/blackout` | Is a symbol inside a high-impact window now/at T | `symbol`,`at?`,`minImpact`,`before`,`after` |
| `GET /v1/affected-symbols` | Resolve an event → symbols in a watchlist | `eventId`,`watchlist` |
| `POST /v1/events:batch` | Multiplex several queries in one round-trip | body: array of queries |
| `GET /v1/stream` (SSE) | Live push: releases/revisions/window-enter | `currencies`,`minImpact` (scope `calendar:stream`) |
| `POST /v1/webhooks` | Register an HMAC-signed callback for release/revision/blackout | body: url, filters, secret |
| `GET /v1/health` | Per-source freshness + coverage | — |

## Blackout — cBot news filter

`GET /v1/blackout` returns `{ inBlackout, event, startsAt, endsAt, stale }`. On uncertainty nó defaults
to **configured conservative answer** (fail-closed by default: "assume in-blackout" cho risk-off
bots), cộng một `stale` flag — một data gap không bao giờ green-lights trading through NFP. Endpoint là một pure
DB/cache read với hard server timeout; không có synchronous origin fetch on hot path.

Một shipped typed client (`Infrastructure.Calendar.CmindCalendarClient`) wrap này: point its `HttpClient`
tại API root, call `GetTokenAsync(clientId, clientSecret)` một lần, rồi `GetBlackoutAsync(token, symbol)`
before each order — nó **fail-safe by construction** (bất kỳ non-success hoặc parse error return
`InBlackout = true, Stale = true`, vì vậy một data gap không bao giờ green-lights trading). Một cBot pauses around news
như thế này:

```csharp
// Pseudocode cho một cTrader cBot sử dụng WebRequest + Calendar API client token.
var jwt = CalendarApi.GetToken(clientId, clientSecret);           // POST /v1/token
var res = CalendarApi.Blackout(jwt, symbol: SymbolName,           // GET  /v1/blackout
                               minImpact: "High", before: 15, after: 15);
if (res.InBlackout || res.Stale)                                  // fail-safe: stale ⇒ treat as blackout
    return;                                                       // skip new entries in the news window
// ...otherwise proceed to place the order
```

## Point-in-time cho backtests

Pass `asOf` on any read để get calendar exactly as it stood at a past instant — actuals,
forecasts và revisions *as they were then*. Because `asOf` reads là pure và cacheable, một backtest
hammering history gets identical bytes every time, và một backtested news rule behaves exactly like
live (không look-ahead from revised values in history).

## Resilience cho algo callers

API sits in a trading hot path, vì vậy nó không bao giờ throw into live bot: every path returns a
well-formed `problem+json` hoặc typed degraded body. Nó reuse copy-trading's resilience primitives —
standard HTTP resilience handler on each source client, domain circuit breaker per source, a
lease-guarded singleton ingestion worker với startup reconciliation, và health checks wired into
`/health`. Shipped typed client snippet đi kèm retry + timeout + circuit-breaker preconfigured
nên bot authors inherit resilience.

## Sibling: AI currency-strength (`market:read`)

[AI macro currency-strength](./currency-strength.md) read model ride **cùng** JWT machinery —
một scheme, một signing secret, một rate-limiter — chỉ thêm `market:read` scope. Register API
client với scope đó, exchange nó cho token như trên, và call:

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

Một token thiếu `market:read` gets `403`; an expired/tampered token gets `401`. Endpoints là gated
on AI feature flag và served under `/api/market/v1` nên chúng stay independent của calendar
feature gate. At run/backtest dispatch một deployment có thể inject `CMIND_API_BASEURL` + một short-lived
`market:read` token nên một cBot calls back với zero client registration.
