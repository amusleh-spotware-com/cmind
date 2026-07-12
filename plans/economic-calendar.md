# Economic Calendar — Own FX/CFD Calendar, Deeply Integrated

**Goal:** build cMind's own economic calendar — release schedule, actuals, forecasts, revisions,
and a **data-driven impact model** — sourced from **primary authorities** (central banks + national
statistical agencies), with **zero dependency** on ForexFactory, FXStreet, Investing.com,
TradingEconomics, or any aggregator. Fast, ≥10 years of history, point-in-time correct, and wired
into **Trading, MCP, cBots, AI, Alerts, and Backtests**. Decoupled module, fully integrated.
Enabled by default; disablable via white-label branding.

Held to the house bars: **resilience, reliability, full E2E testability, security, ease of use.**

---

## 1. Research findings

### 1.1 What FX/CFD traders complain about (the leading calendars)

Recurring, evidence-backed pain points — these become our **differentiators**, not just features:

- **Silent last-minute impact-rating changes.** The #1 complaint against ForexFactory: impact color
  flips (or the event vanishes) minutes before the release, with no notification — blows up news
  traders. → *Our impact rating is **deterministic, versioned, and auditable**; every change is a
  recorded revision with a timestamp, never a silent overwrite.*
- **Timestamp discrepancies between providers.** Same event shows 13:34 / 13:35 / 16:32 across FF /
  Investing / FXStreet even at the same GMT offset. → *We anchor every event to a single
  **UTC instant from the primary source's official schedule**, store the source's own timezone, and
  render per-user TZ deterministically.*
- **Missing market-moving events.** Price spikes with no calendar entry. → *Coverage tracked per
  source; gaps are observable (freshness/So-far-vs-expected metrics) and alertable.*
- **Timezone / DST confusion**, especially on mobile (FXStreet/IG app have no TZ picker). → *Explicit
  per-user IANA timezone, DST handled by the zone db, never a manual ±1h toggle.*
- **Short/locked history & browsing range.** FF caps ~60 days; TradingEconomics gates custom dates
  behind registration. → *≥10 years, unrestricted range, no wall.*
- **Inconsistent revisions display.** Some calendars hide revised values (esp. mobile). → *Full
  revision chain (original → each revision) first-class, everywhere incl. API/MCP/cBot.*
- **Ad clutter / broker promo.** → *Clean, branded, mobile-first, dialog-driven UI.*
- **Missing extras:** keyword search, export/print, saved settings, calculators, sentiment. → *We add
  search, filtering, iCal/CSV export, and — uniquely — **surprise analytics** and **backtestable
  news-window rules**.*

### 1.2 What algo/cBot traders specifically want

- **News filter that pauses/guards automated strategies** in a window before/after high-impact
  releases — not a crude "disable the whole bot for the day."
- **Country→symbol mapping** done for them: EURUSD cares about **both** EUR (EU) and USD (US) events.
  This is the single most-cited integration papercut.
- **Impact classification they can trust**, plus **historical surprise data** (actual−forecast) to
  backtest news rules over 50+ past NFP/CPI prints.
- **Backtest-faithful** news data (point-in-time) so a backtested news filter behaves like live —
  today's calendars leak look-ahead (using *revised* values in historical tests).

### 1.3 Primary data sources (no aggregators)

Two layers, decoupled:

**(a) Schedule / release-timing** — when a release will happen:
- **FRED release calendar** (`fred/releases`, `fred/release/dates`) — St. Louis Fed publishes the
  official release schedule for a huge set of US series.
- **National statistical agencies' release calendars:** BLS (CPI, NFP, PPI, JOLTS), BEA (GDP, PCE),
  Census (retail sales, durable goods, housing), Eurostat, ONS (UK), Destatis (DE), INSEE (FR),
  Japan e-Stat / MIC, ABS (AU), StatCan.
- **Central bank meeting calendars:** Fed/FOMC, ECB, BoE, BoJ, RBA, BoC, SNB, RBNZ — each publishes
  a forward schedule of decision dates + press conferences.

**(b) Actual values** — the printed number, pulled at/after release:
- **FRED API** — best all-around: full history + **vintage dates** (revision history) for revisions
  and point-in-time.
- **BLS, BEA, Census, ECB SDW, Eurostat, OECD SDMX** direct APIs for authoritative actuals and
  revision vintages.

**Honest gaps & how we handle them:**
- **Consensus/forecast** (survey median of economists) is *not* freely published by primary sources —
  it is the aggregators' proprietary value-add. We do **not** fabricate it. Options, in priority:
  1. Store **previous** value + **revision** from official sources (always available).
  2. `IForecastProvider` **pluggable port** — a deployment may wire a licensed consensus feed
     (bring-your-own key); off by default. Event schema carries `Forecast?` as nullable.
  3. **Derived "model expectation"** (clearly labeled, non-consensus): our own baseline from the
     series' history / seasonal model, used only for surprise context, never presented as consensus.
- **Impact rating** is *ours*, computed — see §4.3 — precisely to escape the "silent change" problem.

**Sources:** FRED API (`fred/releases`, `series/vintagedates`); BLS/BEA/Census developer APIs;
ECB SDW; Eurostat & OECD SDMX; EarnForex *Top Forex Calendars* comparison; ForexFactory/FXStreet
trader complaint threads; ClickAlgo / AlgoCreators cTrader news-filter tooling; MQL5 news-filter
articles (country→symbol mapping, PIT/look-ahead pitfalls).

---

## 2. Design principles

- **Point-in-time (PIT) by construction.** Every stored fact carries `KnownAt` (when *we* learned it)
  + `EffectiveAt` (event instant). A query "as the calendar looked at time T" is a first-class API.
  This kills look-ahead in backtested news rules and matches the existing repo PIT stance
  (`plans/institutional-edge-quant-ai.md`).
- **Append-only truth.** Actuals, forecasts, impact scores are **never overwritten** — a new value is
  a new `EventRevision`. The complaint we're solving is silent mutation; our answer is an audit trail.
- **Decoupled, not disconnected.** Calendar is its own module + its own DB schema + its own ingestion
  worker; the rest of the app consumes it only through a Core port (`IEconomicCalendar`) and
  read-models. It can be disabled with zero effect on the trading core.
- **Deterministic & reproducible.** Impact scoring and country→symbol mapping are pure domain
  functions — fully unit-testable, versioned, no external call at query time.
- **Fast reads.** Hot path (next events, today, per-symbol window) served from indexed read-models /
  cache; ingestion is the only thing that touches external HTTP.
- **Degradable.** A dead source degrades coverage for *that* source only; the calendar keeps serving
  everything else and surfaces the gap. AI/forecast layers are gated and optional.

---

## 3. Architecture

Mirror the existing **decoupled-but-integrated** patterns (`ExternalNode`, `Nodes`, AI gating).

```
src/Core (calendar subdomain)
  Calendar/
    EconomicEvent.cs          — aggregate root (schedule + revisions)
    EventRevision.cs          — owned: (KnownAt, Actual?, Forecast?, Previous?, ImpactScore, Source)
    EconomicSeries.cs         — aggregate: a tracked indicator (CPI, NFP...) + its release cadence
    CalendarValueObjects.cs   — SeriesCode, CountryCode, ImpactLevel, ImpactScore, Surprise,
                                ReleaseWindow, CalendarEventId, SeriesId
    ImpactModel.cs            — PURE deterministic impact scoring (domain service impl in Core)
    CurrencyExposure.cs       — PURE country→currency→symbol mapping
    INewsWindowPolicy.cs      — "is instant T inside a blackout for symbol S?" (pure)
    IEconomicCalendar.cs      — port: query events/windows/surprises (read side)
    IForecastProvider.cs      — port: optional consensus (nullable, off by default)
    ICalendarSource.cs        — port: one primary source connector (ingest side)
    Events: EconomicEventReleased, EconomicEventRevised, HighImpactWindowEntered

src/Infrastructure
  Calendar/
    Sources/  FredSource, BlsSource, BeaSource, CensusSource, EcbSource, EurostatSource,
              OecdSource, CentralBankScheduleSource (per-bank)   — each : ICalendarSource
    EconomicCalendarReader.cs  — IEconomicCalendar impl: read-through cache over Postgres (§9.2)
    CoverageLedger.cs          — tracks materialized (series, span) ranges; single-flight lazy fetch
    Persistence: own CalendarDbContext, own schema `calendar`, own migration history (decoupled)
    Http: typed HttpClient per source w/ Polly (retry+jitter+circuit-breaker), ETag/If-Modified

src/Nodes (or new src/Calendar host worker)
  CalendarIngestionService.cs  — BackgroundService: schedule-sync + actual-poll loops
  ReleasePoller.cs             — around each scheduled release, tight-polls the source for the print
  ImpactRecomputeService.cs    — recomputes impact scores as new realized-vol history accrues

src/Web
  Components/Pages/EconomicCalendar.razor          — mobile-first agenda + Day/Week/Month + History browser
  Components/Pages/CalendarSeries.razor            — single-indicator 10y chart + table
  Components/Dialogs/CalendarFilterDialog.razor    — TZ, currencies, impact, keyword (dialog, per rule)
  Components/Dialogs/EventDetailDialog.razor       — revision chain, surprise, affected symbols
  Components/Pages/CalendarApiClients.razor        — admin: issue/revoke JWT API clients + scopes
  Api/CalendarApi/                                 — versioned public REST (/api/calendar/v1/**):
     TokenEndpoint, EventsEndpoints, HistoryEndpoint, BlackoutEndpoint, SurprisesEndpoint,
     StreamEndpoint (SSE), WebhookEndpoints, OpenApi  — JWT-secured, rate-limited, paginated
  Security: CalendarJwt (issue/validate HS256), ApiClient store; gated by
     BrandingOptions.EnableEconomicCalendar

src/Mcp
  CalendarTools.cs             — MCP tools for AI clients (see §5.2)

cBot-facing
  Calendar REST API (stable, documented) consumed by cBots via WebRequest/HttpClient (see §5.3)
```

**Why a separate ingestion host/worker:** ingestion is I/O-bound, bursty around releases, and must
not share a failure domain with trading. It can run on the web host or a node; keyed idempotent
writes make it safe to run singleton (lease-guarded, like the copy-engine host).

---

## 4. Domain model (Core)

### 4.1 Aggregates & value objects

- **`EconomicSeries`** (root) — a tracked indicator: `SeriesId`, `SeriesCode` ("US.CPI.MoM"),
  `CountryCode`, human name, `ReleaseCadence`, source binding(s), default `ImpactLevel` prior.
  Owns its known future `ReleaseWindow`s. Method `ScheduleRelease(window, now)`,
  `SupersedeSchedule(...)` — schedule changes are recorded, never silently replaced.
- **`EconomicEvent`** (root) — a concrete scheduled release instance: `CalendarEventId`, `SeriesId`
  (strong ID ref, not nav), `EffectiveAt` (UTC instant), `SourceTimeZone`. Owns an ordered list of
  **`EventRevision`** (append-only): `(KnownAt, Actual?, Forecast?, Previous?, ImpactScore, Unit,
  SourceRef)`. Methods: `Release(actual, now, source)`, `Revise(actual, now, source)`,
  `AdjustSchedule(newInstant, now)`, `RescoreImpact(score, now)`. Each raises the matching domain
  event. Invariant: revisions are monotonic in `KnownAt`; `Actual` only set on/after `EffectiveAt`
  unless source explicitly early-releases.
- **Value objects:** `SeriesCode`, `CountryCode` (ISO), `CurrencyCode`, `ImpactLevel`
  (Low/Medium/High/Critical enum-VO), `ImpactScore` (0–100 double VO, range-checked),
  `Surprise` (actual−forecast normalized by rolling stdev, i.e. z-score), `ReleaseWindow`
  (instant + precision: Exact / Day / Tentative), `MarketMovingCategory`.

Strong IDs in `StrongIds.cs` convention. No public setters. Construction via `Create(...)` →
`DomainException` on bad input.

### 4.2 Country → currency → symbol mapping (`CurrencyExposure`)

Pure function solving the top algo papercut. `AffectedSymbols(event, watchlist)`:
`CountryCode → CurrencyCode(s) → symbols in the account/watchlist quoting that currency` (base
**or** quote). EURUSD ⇐ {EU events, US events}. EUR-bloc countries (DE/FR/IT…) all map to EUR.
Fully unit-tested with a matrix of majors/minors/crosses + metals (XAU⇐USD) + indices (US500⇐US).

### 4.3 Impact model (`ImpactModel`) — the anti-"silent change" differentiator

Deterministic, versioned, reproducible impact score in [0,100] → banded to `ImpactLevel`. Inputs are
**only** historical/known-at-scoring-time data (no future leak):
- **Series prior** — baseline weight per indicator class (rate decision ≫ CPI ≫ minor survey).
- **Realized-volatility footprint** — median |return| of the primary affected symbols in the N
  minutes after this series' *past* releases (data we already have from backtests/price history).
  This is the honest, data-driven heart: "this release *historically* moves price this much."
- **Surprise sensitivity** — how strongly |surprise z-score| correlated with post-release move.

`ImpactModelVersion` is stored on each score. Recompute is an explicit, logged operation producing a
new revision — never a mutate. Bands are config, but the raw score is reproducible from inputs. A
user can see *why* an event is High. This is exactly what FF's silent flip denies traders.

### 4.4 News-window policy (`INewsWindowPolicy`) — the algo hook

Pure: `IsBlackout(symbol, instant, rule)` where a `NewsWindowRule` = `{minImpact, beforeMinutes,
afterMinutes, currencies?/series?}`. Returns blackout + the triggering event. Drives cBot news filter,
copy-trade pause, and AI risk guard **with one shared, tested implementation** — no divergence.

---

## 5. Integration surfaces

### 5.1 Trading

- **Copy-trading pause:** `CopyEngineHost` subscribes to `HighImpactWindowEntered`; while a symbol is
  in blackout per the account's `NewsWindowRule`, mirroring for that symbol is paused/queued
  (configurable: skip vs. defer). Failure path E2E: enter window → mirror suppressed → exit → resume.
- **Prop-firm / AI risk guard:** feed calendar blackout into `AiRiskGuard` so a challenge account can
  auto-flatten or block new entries around Critical events (drawdown protection). Deterministic; no AI
  call needed for the gate itself.
- **Instance/backtest annotation:** backtest reports overlay event markers (PIT-correct) so a strategy
  author sees which trades landed on NFP. Uses the same PIT query — no look-ahead.

### 5.2 MCP (`CalendarTools`)

Tools for AI clients (all read-only, gated by the same branding flag + MCP API key):
- `calendar_upcoming(currencies?, minImpact?, within?)` → next events, per-user TZ.
- `calendar_event(eventId)` → full revision chain + affected symbols + surprise.
- `calendar_surprise_history(seriesCode, count)` → last N actual/forecast/surprise for backtesting.
- `calendar_blackout(symbol, at?)` → is/when a symbol is in a high-impact window.
- `calendar_affected_symbols(eventId, watchlist)` → country→symbol resolution.
Feeds the existing `AiFeatureService` so agent/alerts can reason over "what's on the calendar."

### 5.3 Public Calendar REST API — JWT-secured, the headline integration

A **full-featured, versioned, JWT-secured REST API** — the flagship surface. Any external service or
cBot calls it to pull upcoming *and* full historical events. Feature-parity with the FXStreet
Calendar API, then past it: point-in-time, full revision chains, deterministic impact rationale,
surprise analytics, symbol-resolution, and blackout math the FXStreet API does not expose. See §5.6
for the complete API spec.

### 5.4 Alerts

New `AlertRule` trigger type **`EconomicEvent`**: notify N minutes before a matching release, or on
actual/surprise crossing a threshold (e.g. "CPI surprise > +2σ"). Reuses the existing alert routing
(`AlertRule` owns `AlertEvent`, host→alert bridge).

### 5.5 UI (Web) — modern, parity-plus, full-history

Mobile-first (360px), design-tokens-only, MudBlazor, branded — matches the polish of FXStreet /
Investing / Myfxbook, then adds what they lack. Filtering (TZ, currencies, impact, keyword) via a
**dialog** (mandate 7), not an inline form.

**Not just live/upcoming — full historical browsing is a first-class UI mode.** A user can scroll or
jump to **any date across the full ≥10-year history** (no 60-day cap, no registration wall — the
FF/TradingEconomics limits we're beating), see the printed actuals/forecasts/revisions as they were,
and land on any past NFP/CPI in two taps.

- **Views:** *Upcoming* (default agenda, "next N days"), *Day/Week/Month* calendar grid,
  *This-week* strip, and **History browser** — date-range picker + infinite virtualized scroll back
  through years, `q` keyword search, country/currency/impact facets. Deep-linkable
  (`/economic-calendar?date=2018-02-02&currencies=USD&impact=high`).
- **Modern interactions:** sticky day headers, color-coded impact chips (with a tooltip showing the
  *impact rationale*, not an opaque color), countdown-to-next-release, live "just released"
  highlight (SSE), collapsible currency groups, saved filter presets, per-user timezone (IANA, DST
  auto — no ±1h toggle), light/dark via tokens.
- **Event detail dialog:** the **revision chain** (original → each revision with KnownAt),
  surprise z-score with a mini sparkline of past prints, impact rationale + affected symbols, and
  actions: add-alert, add-to-news-rule, open-in-backtest-overlay.
- **Series page:** a single indicator's full history as a chart + table (actual vs forecast vs
  surprise over 10y) — the "click the event to see its track record" flow traders ask for.
- **Export:** iCal subscription + CSV/JSON download of any filtered range (record-keeping — a gap in
  IG/others). Virtualized rendering keeps a multi-year range fast on a phone.
- New routes added to `PageSmokeTests` **and** `MobileLayoutTests` (ui-guidelines §9); E2E covers
  upcoming, history jump-to-past-date, filter dialog, event detail, series chart, export.

**UI inspiration → mapped to our guidelines/theme.** Reviewed ForexFactory, FXStreet, Investing.com,
TradingEconomics. Take their *information architecture*, drop their clutter/ads, and render entirely
through our tokens + MudBlazor per `website/docs/ui-guidelines.md`:

- **Row anatomy** (all four converge on this — adopt it): `time · country/flag · currency · event name
  · impact · actual · forecast(consensus) · previous`. On desktop = columns; **on phone this is NOT a
  wide table — it collapses to a card** with `DataLabel`s (ui-guidelines §3, `Nodes.razor` template).
  `actual` colored better/worse vs forecast using `--app-success`/`--app-error` tokens (their
  green/red beat/miss cue), never a hard-coded hex.
- **Grouping** (TradingEconomics/FF pattern): sticky day headers + temporal buckets *Recent · Today ·
  Tomorrow · This Week · Next Week · This/Next Month*, plus our History browser beyond their range.
- **Impact cue** (FF's color dots / TE's 3-bar importance): our impact chip uses banded tokens
  (Low→Critical) **with a `HelpTip`/tooltip showing the rationale** — their opaque color, made
  transparent. Every control gets a `HelpTip` sourced from docs (ui-guidelines §5).
- **Filters** (TE's country/category/impact + FF's currency/impact + date range): all in the
  **filter dialog** (mandate 7 — never inline), full-screen on phone, with quick-picks
  (All/Major/currencies) and keyword `q` (the search FF lacks). Timezone = per-user IANA picker
  (fixes the missing-TZ-on-mobile gap in FXStreet/IG apps), DST automatic.
- **Detail** (Investing/TE click-through to indicator page): our event dialog + series page — but with
  the revision chain + surprise sparkline they don't surface.
- **Bottom-nav** entry (ui-guidelines §3) for the calendar as a high-traffic destination; light/dark
  and white-label palette for free via tokens. Zero ads, zero broker banners — the clutter complaint.

---

### 5.6 Calendar REST API — full specification

**Goal:** an API a third-party service, dashboard, or cBot integrates against as a *product* — stable,
documented (OpenAPI), versioned, JWT-secured, rate-limited, paginated. Matches FXStreet's
`/calendar` + `/history` endpoints and exceeds them (PIT `asOf`, revision chains, impact rationale,
blackout, symbol resolution).

#### Security — JWT

Reuse the repo's existing token machinery, don't invent a new scheme.

- **Two token modes:**
  1. **API-client JWT** — an app admin issues a **Calendar API client** (name + scopes + expiry) from
     an `ApiClient` aggregate (mirrors `McpApiKey`). The client authenticates once
     (`POST /api/calendar/v1/token` with a client id + secret) and receives a **short-lived HS256
     JWT** (`iss=cmind-calendar`, `aud=calendar-api`, `exp` 15 min, `scope` claim). Same HS256 pattern
     the ExternalNode agents already use (5-min main→node JWT) — proven in-repo.
  2. **Refresh / long-lived client secret** stays server-side; only the short JWT rides on requests
     (`Authorization: Bearer <jwt>`). Secret stored encrypted via `ISecretProtector`
     (`EncryptionPurposes`), never plaintext, never logged.
- **Scopes** (least-privilege): `calendar:read` (events/history), `calendar:blackout`,
  `calendar:surprises`, `calendar:stream`. A cBot token typically gets `read`+`blackout` only.
- **Validation:** standard ASP.NET `JwtBearer` — validate issuer, audience, lifetime, signing key;
  reject `alg=none`; clock-skew tight. Per-client **rate limit** (token-bucket) + global limiter;
  429 with `Retry-After`. All auth failures → `AuditLog`.
- **Revocation:** disabling the `ApiClient` invalidates future token issuance immediately; short JWT
  lifetime bounds a leaked token's blast radius. Optional per-client `jti` denylist for instant kill.
- Gated by `BrandingOptions.EnableEconomicCalendar` — off ⇒ the whole `/api/calendar/**` tree 404s.

#### Conventions

- **Base path & versioning:** `/api/calendar/v1/...` (URL-versioned; additive changes don't bump).
- **Format:** JSON; RFC 3339 UTC instants + explicit `sourceTimeZone`; optional `tz=` query renders a
  convenience local time without losing the UTC anchor.
- **Pagination:** cursor-based (`cursor`, `limit`≤1000) — stable for large history pulls; `next`
  cursor in the body + `Link` header.
- **Caching:** `ETag` + `If-None-Match`, `Cache-Control` per-endpoint; historical ranges are
  immutable-ish → long TTL; upcoming → short.
- **Errors:** RFC 7807 `problem+json` (`type/title/status/detail`), never a bare 500.
- **Discoverability:** OpenAPI 3 doc at `/api/calendar/v1/openapi.json` + a docs page; a generated
  **`CMind.Calendar.Client`** (typed C#) snippet shipped for cBot authors so they don't hand-roll.

#### Endpoints

| Method & path | Purpose | Key query params |
|---|---|---|
| `POST /v1/token` | Exchange client id+secret → short JWT | body: `clientId`, `clientSecret` |
| `GET /v1/events` | Events in a window (upcoming or historical) | `from`,`to`,`countries`,`currencies`,`series`,`minImpact`,`category`,`q` (keyword),`asOf`,`cursor`,`limit`,`tz` |
| `GET /v1/events/{id}` | One event: full **revision chain**, surprise, impact rationale, affected symbols | `watchlist?`,`asOf?` |
| `GET /v1/events/{id}/revisions` | Ordered revision history (KnownAt → actual/forecast/previous) | — |
| `GET /v1/history` | Deep historical pull for a series (≥10y) | `series`,`from`,`to`,`asOf`,`cursor`,`limit` |
| `GET /v1/series` | Catalog of tracked indicators + cadence + source | `countries`,`currencies`,`q` |
| `GET /v1/surprises` | Historical actual/forecast/**surprise z-score** series | `series`,`count`/`from,to` |
| `GET /v1/next` | Next relevant release for a symbol (country→symbol mapped) | `symbol`,`minImpact` |
| `GET /v1/blackout` | Is a symbol inside a high-impact window now/at T | `symbol`,`at?`,`minImpact`,`before`,`after` → `{inBlackout,event,startsAt,endsAt}` |
| `GET /v1/affected-symbols` | Resolve an event → symbols in a watchlist | `eventId`,`watchlist` |
| `POST /v1/events:batch` | Multiplex several of the above in one round-trip (cBot efficiency) | body: array of queries |
| `GET /v1/stream` (SSE) | Live push: releases/revisions/window-enter as they happen | `currencies`,`minImpact` (scope `calendar:stream`) |
| `POST /v1/webhooks` | Register a callback URL for release/revision/blackout events (HMAC-signed delivery, retries) | body: url, filters, secret |
| `GET /v1/health` | Per-source freshness + coverage (public-ish, no PII) | — |

#### What makes it "more capable than FXStreet"

- **`asOf` point-in-time on every read** — pull the calendar exactly as it stood at any past instant.
  FXStreet returns latest/revised; ours guarantees no look-ahead → backtested news rules == live.
- **Full revision chain** as a first-class resource, not a single "revised" field.
- **Impact rationale** — the deterministic, versioned score + its inputs (why it's High), not an
  opaque color that can silently flip.
- **`blackout` + `affected-symbols`** — the country→symbol + news-window math done server-side, the
  exact papercut cBot authors hit; FXStreet makes you build it.
- **Surprise z-scores** ready for backtesting, over ≥10y.
- **Batch + SSE stream + signed webhooks** for both pull and push integration.
- **Self-hostable & white-labelable** — the deployment owns the data and the keys; no per-call vendor
  cost, no external ToS on redistribution to your own cBots.

#### Testing the API (adds to §8)

- Integration: JWT issue→call→expire→refresh; scope enforcement (blackout scope can't hit surprises);
  rate-limit 429 + `Retry-After`; `asOf` PIT correctness; cursor pagination stability across a large
  history pull; `ETag`/304; disabled-`ApiClient` → 401; feature-off → 404; problem+json on bad input.
- E2E/API: authenticated cBot-style flow — token → `blackout` → suppress a copy-trade in the window;
  SSE stream receives a fixture release; webhook delivery with valid HMAC + retry on 5xx.
- Unit: JWT claim building/validation, scope mapping, rate-limiter token-bucket math.

## 6. White-label toggle

Add `bool EnableEconomicCalendar { get; init; } = true;` to `BrandingOptions` (`App:Branding`,
defaults **true**). When false:
- Nav entry, `/economic-calendar` route, and REST/MCP endpoints return 404/gated (reuse the
  feature-gate pattern; guard page load + recover `ErrorBoundary` on nav — see the known gotcha).
- Ingestion worker not registered → no external calls, no DB growth.
- cBot/MCP tools report feature-disabled cleanly (not a 500).
Single source of truth: one `IsCalendarEnabled` check surfaced from options; endpoints, nav, worker,
and MCP all read it. Integration test asserts full gating (route 404, worker not scheduled).

---

## 7. Resilience & reliability

- **Per-source isolation.** Each `ICalendarSource` behind its own typed `HttpClient` + Polly
  (exponential backoff + jitter, timeout, **circuit breaker**). One source down ⇒ its coverage stale,
  everything else fine; state exposed as a per-source **freshness** metric.
- **Idempotent, keyed ingestion.** Upsert by `(SeriesCode, EffectiveAt)`; writes are append-only
  revisions → safe to retry, safe to run the worker as singleton under a **lease** (like the copy
  host) with reclaim on death. Re-ingesting the same print is a no-op.
- **Conditional fetch.** ETag / If-Modified-Since / source vintage-date cursors → cheap polls, respect
  rate limits, no hammering.
- **Release-window tight-poll with backoff cap.** Around a scheduled instant, `ReleasePoller` polls
  faster, then backs off; a source that's late doesn't spin forever — the event stays "awaiting" and
  is observable, not silently missing (fixes complaint #3).
- **Reconciliation pass.** Periodic full re-sync of the last K days catches missed revisions /
  late-published actuals; diffs produce revisions, never overwrites.
- **Schedule drift handling.** If a source moves a release, `AdjustSchedule` records it as a revision
  with `KnownAt` → the "they moved FOMC and didn't tell anyone" scenario is now an auditable event we
  can alert on.
- **Observability.** Source freshness, last-successful-poll, coverage-vs-expected, revision counts,
  circuit state → existing cloud observability (X-Ray/CloudWatch/App Insights, trace/span correlation).
- **Degradation contract.** Reads never fail because ingestion failed; queries return best-known PIT
  data + a freshness stamp so consumers (and cBots) can decide.

---

## 8. Testing (three tiers, failure paths mandatory)

**Unit (pure Core — the bulk):**
- `ImpactModel`: score monotonic in vol footprint & surprise sensitivity; versioned; reproducible;
  band boundaries.
- `CurrencyExposure`: majors/minors/crosses/metals/indices matrix; EURUSD⇐{EU,US}; EUR-bloc fan-in.
- `INewsWindowPolicy`: before/after window math, impact threshold, series/currency filters, boundary
  instants (exactly at edge), overlapping events.
- `EconomicEvent`/`EventRevision`: append-only invariant, monotonic `KnownAt`, revise vs. release,
  schedule adjust; `DomainException` on bad construction. **PIT query**: as-of T excludes later-known
  revisions (the look-ahead regression test).
- `Surprise` z-score math; TZ rendering (DST boundaries) determinism.

**Integration (real Postgres, Testcontainers):**
- Each `ICalendarSource` parser against **recorded source fixtures** (canned FRED/BLS/BEA/ECB
  payloads — no live HTTP in CI) → correct events/revisions persisted.
- Idempotent re-ingest = no dup, appends revision only on change.
- **Failure paths:** source 500/timeout → circuit opens → other sources unaffected → freshness stale;
  source returns moved schedule → recorded revision; late actual caught by reconciliation pass;
  worker lease reclaim after simulated host death (mirror stress-suite style).
- White-label gating: `EnableEconomicCalendar=false` ⇒ endpoints 404, worker not registered.
- PIT persistence: query as-of past instant returns then-known values (integration-level look-ahead
  guard).

**E2E (Playwright, mobile + desktop) + API:**
- Calendar page loads, filters via dialog (TZ/currency/impact/keyword), event detail shows revision
  chain + affected symbols; add-alert flow; export downloads. New route in `PageSmokeTests`.
- API/cBot: authenticated `GET /api/calendar/blackout` returns correct window; `asOf` PIT response.
- MCP tool call returns upcoming events.
- **Failure-path E2E:** ingest a fixture High event into a blackout window → copy-trade mirror for
  that symbol is suppressed, then resumes after the window (drives the real host).
- Screenshot capture wired into the existing `CAPTURE_SCREENSHOTS` E2E for docs.

Seed a deterministic **10-year fixture dataset** (generated, checked into test assets) so integration
+ E2E exercise history/PIT without live calls. `FakeTimeProvider` throughout — never the real clock.

---

## 9. Storage model, caching & history (≥10 years)

### 9.1 Where the data lives — decision

**Options weighed:**

| Option | Pros | Cons |
|---|---|---|
| **A. Main app DB, shared tables** | simplest | pollutes trading schema; couples calendar churn to core migrations; violates "decoupled" |
| **B. Main Postgres instance, own `calendar` schema** *(chosen)* | one DB to operate/back up; EF migrations already wired; logical isolation; no distributed txn; can `DROP SCHEMA` to fully disable | shares an instance's resources (fine at this scale) |
| **C. Separate physical Postgres DB** | hard resource isolation | 2nd DB to run/back up/monitor; cross-DB reads need HTTP/port; heavier ops for little gain now |
| **D. Time-series DB (Timescale/Influx)** | built for time-indexed data | new dependency + skills; overkill for low-millions of rows; loses relational revision-chain joins |
| **E. Columnar/OLAP (DuckDB/Parquet)** | fast analytical scans | not for live point-reads (blackout/next); another engine |

**Decision: B — same Postgres instance, dedicated `calendar` schema**, append-only tables
(`series`, `economic_event`, `event_revision`) owned by the calendar module's own `DbContext`
(separate context, separate migration history table, same connection string / instance). Rationale:
- **Decoupled but not disconnected** — its own schema + `DbContext` + migrations means calendar
  changes never touch core tables; disabling white-label can leave the schema dormant or drop it.
- **Right-sized** — 10y × thousands of series ≈ **low-millions of rows**; Postgres with the correct
  indexes point-reads this in sub-ms. No need for a time-series engine (revisit only if we later add
  intraday tick-level footprints).
- **One operational surface** — shared backup, PITR, observability, connection pool; no distributed
  transaction (we never write calendar + a trading aggregate in one txn anyway — they integrate via
  domain events / the API).
- **Escape hatch kept** — because it's a distinct schema/context, promoting to option C (separate DB)
  later is a connection-string change, not a rewrite. If row count ever explodes (intraday vol
  footprints), add a **Timescale hypertable for the footprint table only**, leaving the relational
  event/revision tables as-is.

**Schema:** `event_revision` append-only (never `UPDATE`); index `economic_event(EffectiveAt)` and
`(SeriesCode, EffectiveAt)`; covering indexes for the two hot queries — "next events by
currency+impact" and "blackout by symbol+instant". Consider monthly range **partition** of
`economic_event`/`event_revision` by `EffectiveAt` once history is large (keeps the hot recent
partition small); optional, not day-one.

### 9.2 On-demand load = read-through cache that persists (lazy backfill)

Requirement: *once a historical event/range is loaded, cache it and persist it to the DB.* Model the
reader as a **read-through cache over Postgres, with the primary source as the origin**:

1. **Query** (`IEconomicCalendar` / API `GET /v1/events|history`) for a window/series.
2. **Coverage check** — a `series_coverage` ledger records which `(SeriesCode, dateRange)` spans are
   already **materialized** in `calendar` (and at what `KnownAt`). If the requested span is fully
   covered → serve from DB (+ in-memory/distributed cache).
3. **Miss / partial** → the source connector fetches only the missing span, **persists it
   append-only** (`economic_event` + `event_revision`), extends `series_coverage`, then serves. The
   fetch is **idempotent** and **lease/lock-guarded per (series, span)** so concurrent requests don't
   double-fetch (single-flight).
4. **Cache tiers:** (a) in-process `IMemoryCache` for the hot "upcoming/next/blackout" projections;
   (b) optional distributed cache for multi-instance; (c) **Postgres itself is the durable cache** —
   once persisted, a span is never re-fetched unless a reconciliation pass invalidates it. Cache keys
   include `asOf` bucket so PIT queries stay correct. ETag on the API is derived from the span's
   max `KnownAt`.
5. **Result:** the *first* time anyone (UI history scroll, a cBot pulling 10y, an MCP tool) touches an
   old range, it's fetched once, written once, and every later read is a local DB/cache hit — history
   fills in on demand *and* accumulates permanently. A user scrolling back years transparently
   materializes those years into the DB.

This composes with §7 resilience: a source miss during lazy load degrades to "best-known + freshness
stamp," never a hard error; the span stays uncovered and is retried.

### 9.3 Proactive backfill (complements lazy load)

- **Backfill job** (first enable / opt-in) pulls full vintage history for the *core* high-impact
  series (NFP/CPI/GDP/rate decisions/PCE/PMI…) from FRED + agency APIs so the common case is warm
  without waiting for a user miss. Idempotent, resumable, rate-limit-aware, chunked by series×year,
  progress-tracked, re-runnable. Long-tail series fill in lazily via §9.2.

### 9.4 Read performance

- **Read-models / cache:** denormalized "upcoming window" and "per-symbol next event" projections,
  cache-warmed; PIT queries hit the revision table directly (cold path). Target: hot reads < a few ms,
  **no external HTTP at query time** (origin is only touched on a coverage miss during ingestion/lazy
  load, never on the trading/cBot hot path).
- **UI history performance:** the History browser pages the indexed `(EffectiveAt)` table via the
  API's cursor pagination; compact rows + client virtualization → scrolling back years stays smooth on
  a 360px phone. Series-page charts pull pre-aggregated per-series history so a 10y chart is one
  cached query, not thousands of rows over the wire.

---

## 10. Docs (same commit — mandate 8)

- `website/docs/features/economic-calendar.md` — what it is, sources, impact model (how scores are
  computed + versioned), PIT guarantee, TZ handling, revisions.
- `website/docs/features/calendar-cbot-api.md` — REST reference + copy-paste cBot news-filter example
  (pause-around-news), PIT `asOf` for backtests.
- MCP tools reference update; `deployment/` (source API keys/config, backfill, worker) +
  `operations/` (freshness metrics, source-down runbook, reconciliation) + white-label toggle doc.
- Add page ids to `website/sidebars.ts`; `npm run build` (broken-link check) before PR.

---

## 11. Rollout phases

- **P0 — Domain core.** Core aggregates/VOs/ports, `ImpactModel`, `CurrencyExposure`,
  `INewsWindowPolicy`, full unit suite. No infra. *(Highest value, zero external risk.)*
- **P1 — Persistence + one source.** EF schema + migration, `FredSource`, ingestion worker
  (schedule-sync + release-poll), idempotent/append-only, integration tests w/ fixtures + PIT.
- **P2 — Public JWT REST API + Web UI.** `IEconomicCalendar` reader, **versioned JWT-secured
  `/api/calendar/v1` API** (§5.6: token, events, history, blackout, surprises, OpenAPI, rate limit,
  pagination), `ApiClient` aggregate + admin page; calendar page (upcoming + **full-history browser**)
  + series page + dialogs; white-label gate; `PageSmokeTests`; E2E.
- **P3 — More sources.** BLS/BEA/Census/ECB/Eurostat/OECD + central-bank schedules; reconciliation;
  10-year backfill; per-source freshness/observability.
- **P4 — Deep integration.** cBot API + client snippet, MCP tools, Alerts trigger type,
  copy-trade/prop-guard blackout, backtest overlay. Failure-path E2E for each.
- **P5 — Extras.** Surprise analytics, iCal/CSV export, keyword search, optional `IForecastProvider`
  (bring-your-own consensus), impact-model recompute service.

Each phase ships all three test tiers for the surface it adds; analyzer sweep + `get_file_problems`
clean on touched files; DoD checklist per commit.

## 12. Open questions / deferrals

- **Consensus forecast** stays pluggable/off by default — do not fabricate. Revisit if a licensable
  primary-compatible feed is chosen.
- **Impact-model calibration data:** initial vol footprints need a price-history source per symbol —
  confirm what realized-price history the app already has vs. needs.
- **Speeches / unscheduled events** (central banker remarks) are semi-structured — P3+; model as
  `ReleaseWindow.Tentative`.
- **Non-US agency API quirks** (SDMX verbosity, rate limits) — spike per source in P3.
- Which host runs ingestion (web host vs. dedicated node) — default web host under lease; revisit if
  load warrants a dedicated `src/Calendar` host.
