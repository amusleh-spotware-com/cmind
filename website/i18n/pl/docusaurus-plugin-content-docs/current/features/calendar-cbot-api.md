# Kalendarz REST & cBot API

Kalendarz ekonomiczny jest dostępny jako **wersjonowany, JWT-secured, rate-limited REST API** — flagowy
surface integracyjny. Każda usługa zewnętrzna, dashboard lub cBot integruje się z tym jako produkt. Ma
parity funkcji z FXStreet Calendar API i idzie poza: point-in-time `asOf`, pełne revision
chains, deterministyczne rationale impact, surprise analytics, country→symbol resolution, i blackout
math które inne calendar APIs nie eksponują.

> **Status.** JWT security (client issuance + token exchange), gating, i core read
> endpoints — `token`, `events`, `events/{id}`, `history`, `series`, `surprises`, `next`, `blackout`,
> `affected-symbols`, `health` — są **zaimplementowane i integration-tested** (auth, scope enforcement,
> feature/white-label 404), plus **`events/batch`** (bounded multiplex) i discoverable
> **`/openapi.json`** dokument, **`ETag`/`If-None-Match` 304** na event/history reads, i
> **keyset cursor pagination** (`Link: rel="next"`), **SSE `stream`** (live `event: release` push,
> poll-backed), **HMAC-signed webhooks** (`X-CMind-Signature: sha256=…`, owner-registered, delivered przez
> config-gated worker z persisted watermark), i shipped **typed client** (`CmindCalendarClient`).
> Pełny public API surface jest zaimplementowany.

## Security — JWT

API ponownie używa istniejącej HS256 token machinery repo (ten sam pattern którego CtraderCliNode agents
używają), nie nowy schemat:

- Admin aplikacji wydaje **Calendar API client** (nazwa + scopes + expiry). Client wymienia swoje id
  i secret w `POST /api/calendar/v1/token` za **short-lived HS256 JWT**
  (`iss=cmind-calendar`, `aud=calendar-api`, `exp` ~15 min, `scope` claim). Tylko short JWT jeździ
  na requests (`Authorization: Bearer <jwt>`).
- Client secret jest przechowywany **szyfrowany** przez `ISecretProtector` — nigdy plaintext, nigdy zalogowany.
- **Scopes** (least-privilege): `calendar:read`, `calendar:blackout`, `calendar:surprises`,
  `calendar:stream`. cBot token typowo otrzymuje `read` + `blackout` tylko.
- Standard `JwtBearer` validation (issuer, audience, lifetime, signing key; `alg=none` rejected; tight
  clock skew). Per-client token-bucket rate limit + global limiter; `429` z `Retry-After`. Wszystkie auth
  failures są audytowane.
- Wyłączenie clienta zatrzymuje przyszłą token issuance natychmiast; short JWT lifetime bounds wycieknięty
  token. Cały `/api/calendar/**` tree `404`s gdy feature jest wyłączony.

## Konwencje

- **Base path & versioning:** `/api/calendar/v1/...` (URL-versioned; additive changes nie bump).
- **Format:** JSON; RFC 3339 UTC instants plus explicit `sourceTimeZone`; optional `tz=` renderuje
  convenience local time bez utraty UTC anchor.
- **Pagination:** cursor-based (`cursor`, `limit` ≤ 1000); `next` cursor w body i `Link` header.
- **Caching:** `ETag` + `If-None-Match`; historical ranges otrzymują long TTL, upcoming short one.
- **Errors:** RFC 7807 `problem+json`, nigdy bare `500`.
- **Degraded reads:** source/DB fault zwraca `200` best-known data plus `X-Calendar-Freshness`
  / `stale=true` signal (lub `503 Retry-After` tylko jeśli naprawdę nic nie jest znane) — cBot decyduje.

## Endpoints

| Method & path | Cel | Key params |
|---|---|---|
| `POST /v1/token` | Exchange client id+secret → short JWT | body: `clientId`, `clientSecret` |
| `GET /v1/events` | Events w window (upcoming lub historical) | `from`,`to`,`countries`,`currencies`,`series`,`minImpact`,`category`,`q`,`asOf`,`cursor`,`limit`,`tz` |
| `GET /v1/events/{id}` | One event: pełny revision chain, surprise, impact rationale, affected symbols | `watchlist?`,`asOf?` |
| `GET /v1/events/{id}/revisions` | Ordered revision history | — |
| `GET /v1/history` | Deep historical pull series (≥10y) | `series`,`from`,`to`,`asOf`,`cursor`,`limit` |
| `GET /v1/series` | Catalog tracked indicators + cadence + source | `countries`,`currencies`,`q` |
| `GET /v1/surprises` | Historical actual/forecast/surprise z-score series | `series`,`count`/`from,to` |
| `GET /v1/next` | Next relevant release symbol (country→symbol mapped) | `symbol`,`minImpact` |
| `GET /v1/blackout` | Jest symbol wewnątrz high-impact window teraz/na T | `symbol`,`at?`,`minImpact`,`before`,`after` |
| `GET /v1/affected-symbols` | Resolve event → symbols w watchlist | `eventId`,`watchlist` |
| `POST /v1/events:batch` | Multiplex kilka queries w one round-trip | body: array queries |
| `GET /v1/stream` (SSE) | Live push: releases/revisions/window-enter | `currencies`,`minImpact` (scope `calendar:stream`) |
| `POST /v1/webhooks` | Register HMAC-signed callback release/revision/blackout | body: url, filters, secret |
| `GET /v1/health` | Per-source freshness + coverage | — |

## Blackout — cBot news filter

`GET /v1/blackout` zwraca `{ inBlackout, event, startsAt, endsAt, stale }`. Na uncertainty to
defaults do **configured conservative answer** (fail-closed domyślnie: "assume in-blackout" dla risk-off
bots), plus `stale` flag — data gap nigdy nie green-lights trading przez NFP. Endpoint to pure
DB/cache read z hard server timeout; nie ma synchronicznego origin fetch na hot path.

Shipped typed client (`Infrastructure.Calendar.CmindCalendarClient`) wraps to: point jego `HttpClient`
na API root, call `GetTokenAsync(clientId, clientSecret)` raz, wtedy `GetBlackoutAsync(token, symbol)`
przed każdym order — to jest **fail-safe przez konstrukcję** (każdy non-success lub parse error zwraca
`InBlackout = true, Stale = true`, więc data gap nigdy nie green-lights trading). cBot pauzuje wokół news
tak:

```csharp
// Pseudocode dla cTrader cBot używającego WebRequest + Calendar API client token.
var jwt = CalendarApi.GetToken(clientId, clientSecret);           // POST /v1/token
var res = CalendarApi.Blackout(jwt, symbol: SymbolName,           // GET  /v1/blackout
                               minImpact: "High", before: 15, after: 15);
if (res.InBlackout || res.Stale)                                  // fail-safe: stale ⇒ treat as blackout
    return;                                                       // skip new entries w news window
// ...otherwise proceed to place order
```

## Point-in-time dla backtestów

Pass `asOf` na każdy read aby dostać calendar dokładnie jak stał past instant — actuals,
forecasts i revisions *jak były wtedy*. Ponieważ `asOf` reads są pure i cacheable, backtest
hammering history otrzymuje identical bytes każdy raz, i backtested news rule behaves dokładnie jak
live one (nie look-ahead z revised values).

## Resilience dla algo callers

API siedzi w trading hot path, więc nigdy nie wyrzuca do live bot: każdy path zwraca
well-formed `problem+json` lub typed degraded body. Ponownie używa copy-trading's resilience primitives —
standard HTTP resilience handler na każdy source client, domain circuit breaker per source, lease-guarded
singleton ingestion worker z startup reconciliation, i health checks wired do `/health`. Shipped
typed client snippet przychodzi z retry + timeout + circuit-breaker preconfigured więc bot authors
inherit resilience.

## Sibling: AI currency-strength (`market:read`)

[AI macro currency-strength](./currency-strength.md) read model jeździ **ten sam** JWT machinery —
jeden scheme, jeden signing secret, jeden rate-limiter — dodając tylko `market:read` scope. Zarejestruj API
client z tym scope, wymień go za token dokładnie jak wyżej, i call:

```
GET /api/market/v1/currency-strength/latest?horizon=3M&tier=Majors
GET /api/market/v1/currency-strength/history?days=30
GET /api/market/v1/currency-strength/pair/EUR/USD?horizon=3M
```

```csharp
// obtain token via POST /api/calendar/v1/token jak wyżej, wtedy:
http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
var view = await http.GetFromJsonAsync<JsonElement>(
    baseUrl + "/api/market/v1/currency-strength/latest?horizon=3M");
// view.ranking[], view.forecasts[], view.pairs[] (bias/conviction), view.narrative
```

Token brakujący `market:read` otrzymuje `403`; expired/tampered token otrzymuje `401`. Endpoints są gated
na AI feature flag i served pod `/api/market/v1` więc stay independent calendar
feature gate. Przy run/backtest dispatch deployment może inject `CMIND_API_BASEURL` + short-lived
`market:read` token aby cBot calluj back z zero client registration.
