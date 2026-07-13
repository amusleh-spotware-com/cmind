# Economic calendar

cMind ship **own** economic calendar — release schedule, actuals, forecasts, revisions và a
data-driven impact model — sourced from **primary authorities** (central banks và national
statistical agencies), với **zero dependency** trên ForexFactory, FXStreet, Investing.com hoặc bất kỳ
aggregator nào. Nó point-in-time correct, giữ ≥10 years of history, và được wired vào trading, public
API, MCP, cBots, AI, alerts và backtests. Nó là một decoupled module: có thể disabled với
zero effect on trading core.

> **Status.** Domain core (impact model, country→symbol mapping, news-window policy, point-in-time
> revision chains, two-tier gating) **and** persistence (the `calendar` Postgres schema, append-only
> read/write side, FRED connector và config-gated ingestion worker) được implement và tested
> (unit + Testcontainers integration). JWT REST API, MCP tools và UI land in subsequent
> rollout phases described below.

## What makes it different

Các complaint thường gặp chống lại leading calendars thành our design constraints:

- **No silent impact-rating changes.** Our impact rating là **deterministic, versioned và auditable**. Mọi thay đổi là một recorded revision với timestamp — không bao giờ silent overwrite. User
  có thể see exactly *why* một event là High.
- **One UTC anchor per event.** Mọi event được anchor vào một single UTC instant từ primary
  source's official schedule; source's own timezone stored, và per-user rendering uses an
  explicit IANA timezone với DST handled by zone database — không bao giờ manual ±1h toggle.
- **Full revision chains, everywhere.** Original value và mọi revision là first-class, exposed
  identically through API, MCP và cBot surfaces.
- **≥10 years of history, no wall.** Unrestricted browsing range; không có 60-day cap, không registration gate.
- **Point-in-time by construction.** Mọi fact mang `KnownAt` (khi *chúng tôi* learn nó) và
  `EffectiveAt` (event instant). "As calendar looked at time T" là một first-class query, vì vậy a
  backtested news rule behaves exactly like live — không look-ahead from using revised values in history.

## The impact model

Impact score là một pure, deterministic function in `[0, 100]`, banded to Low / Medium / High /
Critical. Its inputs là only data known at scoring time (no future leak):

- **Series prior** — baseline weight per indicator class (a rate decision outweighs CPI, which
  outweighs a minor survey).
- **Realized-volatility footprint** — median absolute return của primary affected symbols in
  window after this series' *past* releases: "this release historically moves price this much."
- **Surprise sensitivity** — how strongly absolute surprise (a z-score) đã historically
  correlated với post-release move.

Score blends these với fixed weights và stamps an `ImpactModelVersion`. Recompute là một
explicit, logged operation tạo ra **new revision** — không bao giờ mutate — vì vậy score luôn
reproducible từ its inputs.

## Country → currency → symbol mapping

Single most-cited algo integration papercut solved once, as pure function: a country maps to
its currency (every euro-area member fans in to EUR), và a currency maps to watchlist symbols
quoting it on either leg. Vì vậy **EURUSD affected by both EU and US events**; XAUUSD is USD-exposed;
US500 maps to USD. Điều này drives news filter, affected-symbols resolution và blackout math.

## News-window policy

A `NewsWindowRule` là `{ minImpact, beforeMinutes, afterMinutes, currencies?, series? }`. Một single,
shared, pure implementation answers "is instant T inside a blackout for symbol S?" — used by cBot
news filter, copy-trade pause và AI risk guard, vì vậy chúng không bao giờ diverge. On uncertainty the
blackout answer defaults to configured conservative value (fail-closed by default) vì vậy một data gap
không bao giờ silently green-lights trading through a high-impact release.

## Point-in-time & revisions

Actuals, forecasts và impact scores là **append-only**. Mỗi event owns an ordered chain of
revisions, monotonic in `KnownAt`:

- `Scheduled` — event was first scheduled (prior impact, no actual).
- `Released` — first printed actual arrived.
- `Revised` — a later revised value arrived.
- `Rescheduled` — source moved the release instant (auditable, alertable).
- `Rescored` — impact score recomputed under new model version.

Querying `as of` a past instant returns exactly the revision known then — guarantee that kills
look-ahead in backtested news rules.

## Forecast / consensus

Survey median của economists là **not** freely published by primary sources — nó là
aggregators' proprietary value-add, và chúng tôi không fabricate nó. Event schema carries nullable
`Forecast`; deployment có thể wire a licensed consensus feed through optional `IForecastProvider`
port (bring-your-own key, off by default). Previous values và revisions luôn come from official
source.

## Data sources

Two decoupled layers, all primary — không bao giờ an aggregator:

- **Schedule / timing:** FRED release calendar; national statistical agencies (BLS, BEA, Census,
  Eurostat, ONS, Destatis, INSEE, e-Stat, ABS, StatCan); central-bank meeting calendars (Fed, ECB,
  BoE, BoJ, RBA, BoC, SNB, RBNZ).
- **Actual values:** FRED (với vintage dates cho revisions và point-in-time), cộng BLS, BEA, Census,
  ECB SDW, Eurostat và OECD SDMX APIs.

A dead source degrades coverage for **that source only**; calendar keeps serving everything else
và surfaces gap as a freshness metric.

## Rate limiting & backup plan

External providers publish rate limits (FRED allows ~120 requests/minute). Calendar built sao nó
**không bao giờ trips a provider's limit**, và sao cho being throttled hoặc cut off không bao giờ degrades reads:

- **Proactive throttling.** Mọi source's HTTP client goes through a shared, thread-safe rate gate
  spacing outbound requests to a configured budget (`App:Calendar:FredRequestsPerMinute`, default
  100 — deliberately under provider ceiling). Requests queued và paced, không bursted.
- **Honour `429 Retry-After`.** If a provider ever returns `429 Too Many Requests`, gate backs
  whole source off by server-requested cooldown (hoặc `App:Calendar:RateLimitBackoff`, default 60s)
  before next call — no tight retry loop.
- **Standard resilience.** Each source client also inherits app-wide resilience handler (retry với
  backoff + jitter, circuit breaker, timeouts), vì vậy transient blips absorbed và persistently
  failing source parked (its coverage goes stale) without affecting others.
- **Backup plan — durable read-through cache.** Reads **never** served by calling a
  provider. Once a range fetched nó persisted append-only to Postgres và served from there
  forever after (see §"On-demand load"). Vì vậy even when a source is rate-limited hoặc down, calendar
  keeps answering from cached, point-in-time-correct data; missing span simply stays uncovered và
  is retried on next ingestion cycle. Blackout answers additionally fail to conservative
  default under uncertainty, vì vậy a data gap không bao giờ green-lights trading through a release.
- **Cheap polling.** Conditional fetch (ETag / If-Modified-Since / source vintage cursors) và
  "fetch a span once, never again" cache keep actual request volume far below any limit in normal
  operation — rate gate là safety net, không phải common path.

## Enable / disable

Two independent tiers, exactly like other cMind features:

- **Tier 1 — runtime feature toggle** (`Feature.EconomicCalendar`) flipped từ Features admin UI;
  no redeploy, takes effect live.
- **Tier 2 — white-label hard gate** (`App:Branding:EnableEconomicCalendar`, default `true`). A
  reseller sets it `false` để remove feature entirely; operator sau đó không thể re-enable it.

Effective state là `Branding.EnableEconomicCalendar && FeatureToggle.EconomicCalendar`. When disabled,
nav entry hidden và `/economic-calendar`, `/api/calendar/**` và MCP calendar tools return
clean feature-disabled `404` — không bao giờ `500`. Persisted history retained on runtime toggle-off
nên re-enabling instant.

## Rollout phases

- **P0 — domain core** *(implemented)*: aggregates, value objects, ports, impact model,
  country→symbol mapping, news-window policy, two-tier gating, full unit suite.
- **P1 — persistence + one source** *(implemented)*: EF `calendar` schema (own tables, append-only,
  hot indexes), read-through `IEconomicCalendar` reader với point-in-time `asOf`, idempotent
  append-only write service, FRED connector behind resilient typed client, và config-gated
  ingestion worker; Testcontainers integration tests (persistence, PIT, idempotency, blackout).
- **P2 — public JWT REST API + Web UI** *(implemented)*: versioned, JWT-secured `/api/calendar/v1`
  API — client issuance, token exchange, và core read endpoints (events, history, series,
  surprises, next, blackout, affected-symbols, health) với scope enforcement và two-tier gating,
  integration-tested. Cộng mobile-first **`/economic-calendar` page** — gated, fully-localized
  (23 languages) agenda của upcoming releases as phone-friendly cards với colour-banded impact chips
  và MudBlazor **filter dialog** (currencies + minimum impact + **From-date** picker to jump to
  **any** past date across full history — no 60-day cap, no wall); nav entry, smoke/mobile/a11y/E2E
  tested. A **per-indicator series history page** (`/economic-calendar/series/{code}`, linked from each
  event) lists a series' full print history. Surprise charts + infinite-scroll browser follow.
- **P3 — more sources & warm-up** *(started)*: **core-series catalog** (CPI, Core CPI, NFP,
  unemployment, GDP, PCE, Fed funds, retail sales → their FRED ids) seeded automatically on startup,
  và one-time, idempotent, year-chunked **proactive backfill** pulls their ≥10-year history nên
  common case warm without waiting for user miss. **Ingestion on by default**
  (`App:Calendar:IngestionEnabled`, default `true`): **central-bank schedule source** needs **no API
  key**, vì vậy FOMC / ECB / BoE decision calendar populates out of the box — backfill seeds those
  meeting dates across **both recent history và forward horizon**, vì vậy browsing *last month* (hoặc any
  past window) shows meetings even before any FRED/BLS key configured; value series fill in
  once their keys set. Workers honour calendar's two-tier gate — a white-label deployment hoặc
  owner disabling economic-calendar feature stops ingestion, và `App:Calendar:IngestionEnabled=false`
  turns it off explicitly. **Per-source freshness** now real too: worker records each source's last
  successful poll, consecutive-failure count và a tripped-circuit flag (persisted in app settings,
  cross-process), và `/health` endpoint + `calendar_health` MCP tool report truthful `stale`
  verdict per source. **BLS** (a 2nd value source) và **central-bank schedule source** (FOMC / ECB /
  BoE decision dates, backfilled across history và synced forward into a horizon window by worker)
  are in. Still to come: BEA/Census/ECB-SDW/Eurostat/OECD value sources và reconciliation pass.
- **P4 — deep integration**: **MCP tools** *(implemented — full read-API parity: `calendar_events`,
  `calendar_event`, `calendar_history`, `calendar_series`, `calendar_surprises`, `calendar_next`,
  `calendar_blackout`, `calendar_affected_symbols`, `calendar_health`, gated on feature)* và **alerts `EconomicEvent` trigger** *(implemented — `AlertRule` fires N minutes ahead of an
  upcoming release at/above chosen impact, optionally narrowed to currencies; evaluated by existing
  alert worker với no AI, de-duplicated per release; created via
  `POST /api/alerts/rules/economic-event`)*. Prop-guard news-blackout gate **and copy-trade
  blackout pause** are in (§5.1 — opt-in `App:Copy:NewsPauseEnabled`, default off: source
  open whose symbol sits in a Critical-impact blackout skipped, byte-identical hot path when off). **backtest event overlay** is in — `GET /api/calendar/v1/for-symbol` và
  `calendar_events_for_symbol` MCP tool return point-in-time-correct events affecting a symbol in a
  window, và **instance/backtest report page** renders high-impact releases that fell inside
  backtest window beneath equity curve (vì vậy author sees which trades landed on NFP), gated và
  localized. Whole plan now implemented.
- **P5 — extras**: surprise analytics, iCal/CSV export, keyword search, pluggable consensus.

Xem [cBot & REST API reference](calendar-cbot-api.md) cho integration surface.
