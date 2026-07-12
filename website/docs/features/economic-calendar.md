# Economic calendar

cMind ships its **own** economic calendar — release schedule, actuals, forecasts, revisions and a
data-driven impact model — sourced from **primary authorities** (central banks and national
statistical agencies), with **zero dependency** on ForexFactory, FXStreet, Investing.com or any
aggregator. It is point-in-time correct, keeps ≥10 years of history, and is wired into trading, the
public API, MCP, cBots, AI, alerts and backtests. It is a decoupled module: it can be disabled with
zero effect on the trading core.

> **Status.** The domain core (impact model, country→symbol mapping, news-window policy, point-in-time
> revision chains, two-tier gating) **and** persistence (the `calendar` Postgres schema, the append-only
> read/write side, the FRED connector and the config-gated ingestion worker) are implemented and tested
> (unit + Testcontainers integration). The JWT REST API, the MCP tools and the UI land in the subsequent
> rollout phases described below.

## What makes it different

The recurring complaints against the leading calendars became our design constraints:

- **No silent impact-rating changes.** Our impact rating is **deterministic, versioned and
  auditable**. Every change is a recorded revision with a timestamp — never a silent overwrite. A
  user can see exactly *why* an event is High.
- **One UTC anchor per event.** Every event is anchored to a single UTC instant from the primary
  source's official schedule; the source's own timezone is stored, and per-user rendering uses an
  explicit IANA timezone with DST handled by the zone database — never a manual ±1h toggle.
- **Full revision chains, everywhere.** The original value and every revision are first-class, exposed
  identically through the API, MCP and cBot surfaces.
- **≥10 years of history, no wall.** Unrestricted browsing range; no 60-day cap, no registration gate.
- **Point-in-time by construction.** Every fact carries `KnownAt` (when *we* learned it) and
  `EffectiveAt` (the event instant). "As the calendar looked at time T" is a first-class query, so a
  backtested news rule behaves exactly like live — no look-ahead from using revised values in history.

## The impact model

The impact score is a pure, deterministic function in `[0, 100]`, banded to Low / Medium / High /
Critical. Its inputs are only data known at scoring time (no future leak):

- **Series prior** — a baseline weight per indicator class (a rate decision outweighs CPI, which
  outweighs a minor survey).
- **Realized-volatility footprint** — the median absolute return of the primary affected symbols in
  the window after this series' *past* releases: "this release historically moves price this much."
- **Surprise sensitivity** — how strongly the absolute surprise (a z-score) has historically
  correlated with the post-release move.

The score blends these with fixed weights and stamps an `ImpactModelVersion`. Recompute is an
explicit, logged operation that produces a **new revision** — never a mutate — so the score is always
reproducible from its inputs.

## Country → currency → symbol mapping

The single most-cited algo integration papercut is solved once, as a pure function: a country maps to
its currency (every euro-area member fans in to EUR), and a currency maps to the watchlist symbols
quoting it on either leg. So **EURUSD is affected by both EU and US events**; XAUUSD is USD-exposed;
US500 maps to USD. This drives the news filter, the affected-symbols resolution and the blackout math.

## News-window policy

A `NewsWindowRule` is `{ minImpact, beforeMinutes, afterMinutes, currencies?, series? }`. A single,
shared, pure implementation answers "is instant T inside a blackout for symbol S?" — used by the cBot
news filter, the copy-trade pause and the AI risk guard, so they can never diverge. On uncertainty the
blackout answer defaults to the configured conservative value (fail-closed by default) so a data gap
never silently green-lights trading through a high-impact release.

## Point-in-time & revisions

Actuals, forecasts and impact scores are **append-only**. Each event owns an ordered chain of
revisions, monotonic in `KnownAt`:

- `Scheduled` — the event was first scheduled (prior impact, no actual).
- `Released` — the first printed actual arrived.
- `Revised` — a later revised value arrived.
- `Rescheduled` — the source moved the release instant (auditable, alertable).
- `Rescored` — the impact score was recomputed under a new model version.

Querying `as of` a past instant returns exactly the revision known then — the guarantee that kills
look-ahead in backtested news rules.

## Forecast / consensus

The survey median of economists is **not** freely published by primary sources — it is the
aggregators' proprietary value-add, and we do not fabricate it. The event schema carries a nullable
`Forecast`; a deployment may wire a licensed consensus feed through the optional `IForecastProvider`
port (bring-your-own key, off by default). Previous values and revisions always come from the official
source.

## Data sources

Two decoupled layers, all primary — never an aggregator:

- **Schedule / timing:** FRED release calendar; national statistical agencies (BLS, BEA, Census,
  Eurostat, ONS, Destatis, INSEE, e-Stat, ABS, StatCan); central-bank meeting calendars (Fed, ECB,
  BoE, BoJ, RBA, BoC, SNB, RBNZ).
- **Actual values:** FRED (with vintage dates for revisions and point-in-time), plus BLS, BEA, Census,
  ECB SDW, Eurostat and OECD SDMX APIs.

A dead source degrades coverage for **that source only**; the calendar keeps serving everything else
and surfaces the gap as a freshness metric.

## Rate limiting & the backup plan

External providers publish rate limits (FRED allows ~120 requests/minute). The calendar is built so it
**never trips a provider's limit**, and so that being throttled or cut off never degrades reads:

- **Proactive throttling.** Every source's HTTP client goes through a shared, thread-safe rate gate
  that spaces outbound requests to a configured budget (`App:Calendar:FredRequestsPerMinute`, default
  100 — deliberately under the provider ceiling). Requests are queued and paced, never bursted.
- **Honour `429 Retry-After`.** If a provider ever returns `429 Too Many Requests`, the gate backs the
  whole source off by the server-requested cooldown (or `App:Calendar:RateLimitBackoff`, default 60s)
  before the next call — no tight retry loop.
- **Standard resilience.** Each source client also inherits the app-wide resilience handler (retry with
  backoff + jitter, circuit breaker, timeouts), so transient blips are absorbed and a persistently
  failing source is parked (its coverage goes stale) without affecting the others.
- **The backup plan — the durable read-through cache.** Reads are **never** served by calling a
  provider. Once a range is fetched it is persisted append-only to Postgres and served from there
  forever after (see §"On-demand load"). So even when a source is rate-limited or down, the calendar
  keeps answering from cached, point-in-time-correct data; the missing span simply stays uncovered and
  is retried on the next ingestion cycle. Blackout answers additionally fail to the conservative
  default under uncertainty, so a data gap never green-lights trading through a release.
- **Cheap polling.** Conditional fetch (ETag / If-Modified-Since / source vintage cursors) and the
  "fetch a span once, never again" cache keep the actual request volume far below any limit in normal
  operation — the rate gate is a safety net, not the common path.

## Enable / disable

Two independent tiers, exactly like other cMind features:

- **Tier 1 — runtime feature toggle** (`Feature.EconomicCalendar`) flipped from the Features admin UI;
  no redeploy, takes effect live.
- **Tier 2 — white-label hard gate** (`App:Branding:EnableEconomicCalendar`, default `true`). A
  reseller sets it `false` to remove the feature entirely; an operator then cannot re-enable it.

Effective state is `Branding.EnableEconomicCalendar && FeatureToggle.EconomicCalendar`. When disabled,
the nav entry is hidden and `/economic-calendar`, `/api/calendar/**` and the MCP calendar tools return
a clean feature-disabled `404` — never a `500`. Persisted history is retained on a runtime toggle-off
so re-enabling is instant.

## Rollout phases

- **P0 — domain core** *(implemented)*: aggregates, value objects, ports, impact model,
  country→symbol mapping, news-window policy, two-tier gating, full unit suite.
- **P1 — persistence + one source** *(implemented)*: EF `calendar` schema (own tables, append-only,
  hot indexes), the read-through `IEconomicCalendar` reader with point-in-time `asOf`, the idempotent
  append-only write service, the FRED connector behind a resilient typed client, and the config-gated
  ingestion worker; Testcontainers integration tests (persistence, PIT, idempotency, blackout).
- **P2 — public JWT REST API + Web UI** *(implemented)*: the versioned, JWT-secured `/api/calendar/v1`
  API — client issuance, token exchange, and the core read endpoints (events, history, series,
  surprises, next, blackout, affected-symbols, health) with scope enforcement and two-tier gating,
  integration-tested. Plus the mobile-first **`/economic-calendar` page** — a gated, fully-localized
  (23 languages) agenda of upcoming releases as phone-friendly cards with colour-banded impact chips
  and a MudBlazor **filter dialog** (currencies + minimum impact + a **From-date** picker to jump to
  **any** past date across the full history — no 60-day cap, no wall); nav entry, smoke/mobile/a11y/E2E
  tested. The per-indicator series charts + infinite-scroll history browser follow.
- **P3 — more sources & warm-up** *(started)*: a **core-series catalog** (CPI, Core CPI, NFP,
  unemployment, GDP, PCE, Fed funds, retail sales → their FRED ids) is seeded on first enable, and a
  one-time, idempotent, year-chunked **proactive backfill** pulls their ≥10-year history so the common
  case is warm without waiting for a user miss. **Per-source freshness** is now real too: the worker
  records each source's last successful poll, consecutive-failure count and a tripped-circuit flag
  (persisted in app settings, cross-process), and the `/health` endpoint + `calendar_health` MCP tool
  report a truthful `stale` verdict per source. Still to come: BLS/BEA/Census/ECB/Eurostat/OECD sources
  + central-bank schedules and the reconciliation pass.
- **P4 — deep integration**: **MCP tools** *(implemented — full read-API parity: `calendar_events`,
  `calendar_event`, `calendar_history`, `calendar_series`, `calendar_surprises`, `calendar_next`,
  `calendar_blackout`, `calendar_affected_symbols`, `calendar_health`, gated on the feature)*; cBot
  client, alerts trigger, copy-trade / prop-guard blackout, backtest overlay still to come.
- **P5 — extras**: surprise analytics, iCal/CSV export, keyword search, pluggable consensus.

See the [cBot & REST API reference](calendar-cbot-api.md) for the integration surface.
