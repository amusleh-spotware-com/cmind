# Calendar REST & cBot API

The economic calendar is exposed as a **versioned, JWT-secured, rate-limited REST API** — the flagship
integration surface. Any external service, dashboard or cBot integrates against it as a product. It has
feature parity with the FXStreet Calendar API and goes past it: point-in-time `asOf`, full revision
chains, deterministic impact rationale, surprise analytics, country→symbol resolution, and blackout
math that other calendar APIs do not expose.

> **Status.** The JWT security (client issuance + token exchange), the gating, and the core read
> endpoints — `token`, `events`, `events/{id}`, `history`, `series`, `surprises`, `next`, `blackout`,
> `affected-symbols`, `health` — are **implemented and integration-tested** (auth, scope enforcement,
> feature/white-label 404), plus **`events/batch`** (bounded multiplex) and a discoverable
> **`/openapi.json`** document, and **`ETag`/`If-None-Match` 304** on the event/history reads. Still to
> come: cursor pagination, the SSE `stream`, signed `webhooks`, and the shipped typed client snippet.

## Security — JWT

The API reuses the repo's existing HS256 token machinery (the same pattern the CtraderCliNode agents
use), not a new scheme:

- An app admin issues a **Calendar API client** (name + scopes + expiry). The client exchanges its id
  and secret at `POST /api/calendar/v1/token` for a **short-lived HS256 JWT**
  (`iss=cmind-calendar`, `aud=calendar-api`, `exp` ~15 min, `scope` claim). Only the short JWT rides
  on requests (`Authorization: Bearer <jwt>`).
- The client secret is stored **encrypted** via `ISecretProtector` — never plaintext, never logged.
- **Scopes** (least-privilege): `calendar:read`, `calendar:blackout`, `calendar:surprises`,
  `calendar:stream`. A cBot token typically gets `read` + `blackout` only.
- Standard `JwtBearer` validation (issuer, audience, lifetime, signing key; `alg=none` rejected; tight
  clock skew). Per-client token-bucket rate limit + global limiter; `429` with `Retry-After`. All auth
  failures are audited.
- Disabling the client stops future token issuance immediately; the short JWT lifetime bounds a leaked
  token. The whole `/api/calendar/**` tree `404`s when the feature is disabled.

## Conventions

- **Base path & versioning:** `/api/calendar/v1/...` (URL-versioned; additive changes don't bump).
- **Format:** JSON; RFC 3339 UTC instants plus an explicit `sourceTimeZone`; optional `tz=` renders a
  convenience local time without losing the UTC anchor.
- **Pagination:** cursor-based (`cursor`, `limit` ≤ 1000); `next` cursor in the body and a `Link` header.
- **Caching:** `ETag` + `If-None-Match`; historical ranges get a long TTL, upcoming a short one.
- **Errors:** RFC 7807 `problem+json`, never a bare `500`.
- **Degraded reads:** a source/DB fault returns `200` best-known data plus an `X-Calendar-Freshness`
  / `stale=true` signal (or `503 Retry-After` only if truly nothing is known) — the cBot decides.

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

## Blackout — the cBot news filter

`GET /v1/blackout` returns `{ inBlackout, event, startsAt, endsAt, stale }`. On uncertainty it defaults
to the **configured conservative answer** (fail-closed by default: "assume in-blackout" for risk-off
bots), plus a `stale` flag — a data gap never green-lights trading through NFP. The endpoint is a pure
DB/cache read with a hard server timeout; there is no synchronous origin fetch on the hot path.

A cBot pauses around news like this:

```csharp
// Pseudocode for a cTrader cBot using WebRequest + a Calendar API client token.
var jwt = CalendarApi.GetToken(clientId, clientSecret);           // POST /v1/token
var res = CalendarApi.Blackout(jwt, symbol: SymbolName,           // GET  /v1/blackout
                               minImpact: "High", before: 15, after: 15);
if (res.InBlackout || res.Stale)                                  // fail-safe: stale ⇒ treat as blackout
    return;                                                       // skip new entries in the news window
// ...otherwise proceed to place the order
```

## Point-in-time for backtests

Pass `asOf` on any read to get the calendar exactly as it stood at a past instant — the actuals,
forecasts and revisions *as they were then*. Because `asOf` reads are pure and cacheable, a backtest
hammering history gets identical bytes every time, and a backtested news rule behaves exactly like the
live one (no look-ahead from revised values).

## Resilience for algo callers

The API sits in a trading hot path, so it never throws into a live bot: every path returns a
well-formed `problem+json` or a typed degraded body. It reuses copy-trading's resilience primitives —
the standard HTTP resilience handler on each source client, a domain circuit breaker per source, a
lease-guarded singleton ingestion worker with startup reconciliation, and health checks wired into
`/health`. The shipped typed client snippet comes with retry + timeout + circuit-breaker preconfigured
so bot authors inherit resilience.
