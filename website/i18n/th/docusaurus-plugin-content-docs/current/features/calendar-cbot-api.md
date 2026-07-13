# Calendar REST & cBot API

economic calendar exposed เป็น **versioned JWT-secured rate-limited REST API** — flagship
integration surface any external service dashboard หรือ cBot integrates against มัน เป็น product มัน has
feature parity ด้วย FXStreet Calendar API และ goes past มัน: point-in-time `asOf` full revision
chains deterministic impact rationale surprise analytics country→symbol resolution และ blackout
math ที่ other calendar APIs ไม่ expose

> **Status** JWT security (client issuance + token exchange) gating และ core read
> endpoints — `token` `events` `events/{id}` `history` `series` `surprises` `next` `blackout`
> `affected-symbols` `health` — **implemented และ integration-tested** (auth scope enforcement
> feature/white-label 404) บวก **`events/batch`** (bounded multiplex) และ discoverable
> **`/openapi.json`** document **`ETag`/`If-None-Match` 304** on event/history reads และ
> **keyset cursor pagination** (`Link: rel="next"`) **SSE `stream`** (live `event: release` push
> poll-backed) **HMAC-signed webhooks** (`X-CMind-Signature: sha256=…` owner-registered delivered โดย
> config-gated worker off persisted watermark) และ shipped **typed client** (`CmindCalendarClient`)
> full public API surface implemented

## Security — JWT

API reuses repo's existing HS256 token machinery (same pattern CtraderCliNode agents
ใช้) ไม่ใช่ new scheme:

- app admin issues **Calendar API client** (name + scopes + expiry) client exchanges มัน id
  และ secret ที่ `POST /api/calendar/v1/token` สำหรับ **short-lived HS256 JWT**
  (`iss=cmind-calendar` `aud=calendar-api` `exp` ~15 min `scope` claim) เฉพาะ short JWT rides
  on requests (`Authorization: Bearer <jwt>`)
- client secret stored **encrypted** ผ่าน `ISecretProtector` — ไม่เคยplaintext ไม่เคยlogged
- **Scopes** (least-privilege): `calendar:read` `calendar:blackout` `calendar:surprises`
  `calendar:stream` cBot token typically gets `read` + `blackout` เท่านั้น
- Standard `JwtBearer` validation (issuer audience lifetime signing key; `alg=none` rejected; tight
  clock skew) per-client token-bucket rate limit + global limiter; `429` ด้วย `Retry-After` ทั้งหมด auth
  failures audited
- disabling client stops future token issuance immediately; short JWT lifetime bounds leaked
  token whole `/api/calendar/**` tree `404`s เมื่อ feature disabled

## Conventions

- **Base path & versioning:** `/api/calendar/v1/...` (URL-versioned; additive changes don't bump)
- **Format:** JSON; RFC 3339 UTC instants บวก explicit `sourceTimeZone`; optional `tz=` renders
  convenience local time โดยไม่ lose UTC anchor
- **Pagination:** cursor-based (`cursor` `limit` ≤ 1000); `next` cursor ใน body และ `Link` header
- **Caching:** `ETag` + `If-None-Match`; historical ranges get long TTL upcoming short one
- **Errors:** RFC 7807 `problem+json` ไม่เคยbare `500`
- **Degraded reads:** source/DB fault returns `200` best-known data บวก `X-Calendar-Freshness`
  / `stale=true` signal (หรือ `503 Retry-After` เฉพาะ if truly nothing known) — cBot decides

## Endpoints

| Method & path | Purpose | Key params |
|---|---|---|
| `POST /v1/token` | Exchange client id+secret → short JWT | body: `clientId`, `clientSecret` |
| `GET /v1/events` | Events ใน window (upcoming หรือ historical) | `from`,`to`,`countries`,`currencies`,`series`,`minImpact`,`category`,`q`,`asOf`,`cursor`,`limit`,`tz` |
| `GET /v1/events/{id}` | One event: full revision chain surprise impact rationale affected symbols | `watchlist?`,`asOf?` |
| `GET /v1/events/{id}/revisions` | Ordered revision history | — |
| `GET /v1/history` | Deep historical pull สำหรับ series (≥10y) | `series`,`from`,`to`,`asOf`,`cursor`,`limit` |
| `GET /v1/series` | Catalog ของ tracked indicators + cadence + source | `countries`,`currencies`,`q` |
| `GET /v1/surprises` | Historical actual/forecast/surprise z-score series | `series`,`count`/`from,to` |
| `GET /v1/next` | Next relevant release สำหรับ symbol (country→symbol mapped) | `symbol`,`minImpact` |
| `GET /v1/blackout` | Is symbol inside high-impact window now/at T | `symbol`,`at?`,`minImpact`,`before`,`after` |
| `GET /v1/affected-symbols` | Resolve event → symbols ใน watchlist | `eventId`,`watchlist` |
| `POST /v1/events:batch` | Multiplex several queries ใน one round-trip | body: array ของ queries |
| `GET /v1/stream` (SSE) | Live push: releases/revisions/window-enter | `currencies`,`minImpact` (scope `calendar:stream`) |
| `POST /v1/webhooks` | Register HMAC-signed callback สำหรับ release/revision/blackout | body: url filters secret |
| `GET /v1/health` | Per-source freshness + coverage | — |

## Blackout — the cBot news filter

`GET /v1/blackout` returns `{ inBlackout, event, startsAt, endsAt, stale }` on uncertainty มัน defaults
ไป **configured conservative answer** (fail-closed by default: "assume in-blackout" สำหรับ risk-off
bots) บวก `stale` flag — data gap ไม่เคยgreen-lights trading ผ่าน NFP endpoint pure
DB/cache read ด้วย hard server timeout; มี ไม่มี synchronous origin fetch on hot path

shipped typed client (`Infrastructure.Calendar.CmindCalendarClient`) wraps นี่: point `HttpClient` ของมัน
ที่ API root call `GetTokenAsync(clientId, clientSecret)` once จากนั้น `GetBlackoutAsync(token, symbol)`
ก่อน each order — มัน **fail-safe by construction** (ใด ๆ non-success หรือ parse error returns
`InBlackout = true, Stale = true` ดังนั้น data gap ไม่เคยgreen-lights trading) cBot pauses around news
like นี่:

```csharp
// Pseudocode สำหรับ cTrader cBot ใช้ WebRequest + Calendar API client token
var jwt = CalendarApi.GetToken(clientId, clientSecret);           // POST /v1/token
var res = CalendarApi.Blackout(jwt, symbol: SymbolName,           // GET  /v1/blackout
                               minImpact: "High", before: 15, after: 15);
if (res.InBlackout || res.Stale)                                  // fail-safe: stale ⇒ treat as blackout
    return;                                                       // skip new entries ใน news window
// ...otherwise proceed ไป place order
```

## Point-in-time สำหรับ backtests

Pass `asOf` on any read ไป get calendar exactly เช่นมันstood ที่ past instant — actuals
forecasts และ revisions *as they were then* เพราะ `asOf` reads pure และ cacheable backtest
hammering history gets identical bytes every time และ backtested news rule behaves exactly like
live one (ไม่มี look-ahead จาก revised values)

## Resilience สำหรับ algo callers

API sits ใน trading hot path ดังนั้นมันไม่เคยthrow ไป live bot: ทุก path returns
well-formed `problem+json` หรือ typed degraded body มันreuses copy-trading's resilience primitives —
standard HTTP resilience handler on each source client domain circuit breaker per source
lease-guarded singleton ingestion worker ด้วย startup reconciliation และ health checks wired ไป
`/health` shipped typed client snippet comes ด้วย retry + timeout + circuit-breaker preconfigured
ดังนั้น bot authors inherit resilience

## Sibling: AI currency-strength (`market:read`)

[AI macro currency-strength](./currency-strength.md) read model rides **same** JWT machinery —
one scheme one signing secret one rate-limiter — adding เฉพาะ `market:read` scope register API
client ด้วย scope นั้น exchange มัน สำหรับ token exactly เช่นข้างบน และ call:

```
GET /api/market/v1/currency-strength/latest?horizon=3M&tier=Majors
GET /api/market/v1/currency-strength/history?days=30
GET /api/market/v1/currency-strength/pair/EUR/USD?horizon=3M
```

```csharp
// obtain token ผ่าน POST /api/calendar/v1/token เช่นข้างบน จากนั้น:
http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
var view = await http.GetFromJsonAsync<JsonElement>(
    baseUrl + "/api/market/v1/currency-strength/latest?horizon=3M");
// view.ranking[], view.forecasts[], view.pairs[] (bias/conviction), view.narrative
```

token missing `market:read` gets `403`; expired/tampered token gets `401` endpoints gated
on AI feature flag และ served ภายใต้ `/api/market/v1` ดังนั้นพวกเขา stay independent ของ calendar
feature gate at run/backtest dispatch deployment อาจ inject `CMIND_API_BASEURL` + short-lived
`market:read` token ดังนั้น cBot calls back ด้วย zero client registration
