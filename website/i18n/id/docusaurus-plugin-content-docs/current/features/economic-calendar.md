# Kalender ekonomi

cMind mengirim **sendiri** kalender ekonomi — jadwal release, actuals, forecasts, revisions dan data-driven
impact model — bersumber dari **primary authorities** (central banks dan national statistical agencies), dengan **zero dependency** pada ForexFactory, FXStreet, Investing.com atau aggregator apa pun. Ini point-in-time correct, menyimpan ≥10 tahun history, dan diwiring ke trading, public API, MCP, cBots, AI, alerts dan backtests. Ini adalah modul yang decoupled: ia dapat dinonaktifkan dengan zero effect pada trading core.

> **Status.** Domain core (impact model, country→symbol mapping, news-window policy, point-in-time
> revision chains, two-tier gating) **dan** persistence (Postgres `calendar` schema, append-only
> read/write side, FRED connector dan config-gated ingestion worker) diimplementasikan dan diuji
> (unit + Testcontainers integration). JWT REST API, MCP tools dan UI mendarat dalam rollout phases berikutnya
> yang dijelaskan di bawah.

## Apa yang membuatnya berbeda

Keluhan berulang terhadap kalender terkemuka menjadi design constraints kami:

- **Tidak ada silent impact-rating changes.** Impact rating kami adalah **deterministic, versioned dan
  auditable**. Setiap change adalah recorded revision dengan timestamp — tidak pernah silent overwrite. Pengguna
  dapat see exactly *mengapa* event adalah High.
- **Satu UTC anchor per event.** Setiap event adalah anchored ke single UTC instant dari primary
  source's official schedule; source's own timezone disimpan, dan per-user rendering menggunakan explicit IANA timezone
  dengan DST ditangani oleh zone database — tidak pernah manual ±1h toggle.
- **Full revision chains, di mana-mana.** Original value dan setiap revision adalah first-class, exposed
  identically melalui API, MCP dan cBot surfaces.
- **≥10 tahun history, tidak ada wall.** Unrestricted browsing range; tidak ada 60-day cap, tidak ada registration gate.
- **Point-in-time by construction.** Setiap fact membawa `KnownAt` (kapan *kami* mempelajarinya) dan
  `EffectiveAt` (event instant). "Seperti kalender yang terlihat pada waktu T" adalah first-class query, jadi
  backtested news rule berperilaku exactly seperti live — tidak ada look-ahead dari menggunakan revised values dalam history.

## Model impact

Impact score adalah pure, deterministic function dalam `[0, 100]`, dibanded ke Low / Medium / High /
Critical. Input-nya hanya data yang dikenal pada scoring time (tidak ada future leak):

- **Series prior** — baseline weight per indicator class (rate decision outweighs CPI, yang
  outweighs minor survey).
- **Realized-volatility footprint** — median absolute return dari primary affected symbols dalam
  window setelah series ini' *past* releases: "release ini historically moves price ini much."
- **Surprise sensitivity** — seberapa strongly absolute surprise (z-score) telah historically
  correlated dengan post-release move.

Score memblend ini dengan fixed weights dan stamps `ImpactModelVersion`. Recompute adalah
explicit, logged operation yang menghasilkan **new revision** — tidak pernah mutate — jadi score selalu
reproducible dari input-nya.

## Country → currency → symbol mapping

Single most-cited algo integration papercut diselesaikan sekali, sebagai pure function: country maps ke
its currency (setiap euro-area member fans ke EUR), dan currency maps ke watchlist symbols
quoting it pada either leg. Jadi **EURUSD dipengaruhi oleh EU dan US events**; XAUUSD adalah USD-exposed;
US500 maps ke USD. Ini mendorong news filter, affected-symbols resolution dan blackout math.

## News-window policy

`NewsWindowRule` adalah `{ minImpact, beforeMinutes, afterMinutes, currencies?, series? }`. Satu, shared, pure
implementation menjawab "apakah instant T inside blackout untuk symbol S?" — digunakan oleh cBot
news filter, copy-trade pause dan AI risk guard, jadi mereka tidak dapat pernah diverge. Pada uncertainty
blackout answer defaults ke configured conservative value (fail-closed by default) jadi data gap
tidak pernah silently green-lights trading through high-impact release.

## Point-in-time & revisions

Actuals, forecasts dan impact scores adalah **append-only**. Setiap event memiliki ordered chain dari
revisions, monotonic dalam `KnownAt`:

- `Scheduled` — event pertama kali dijadwalkan (prior impact, tidak ada actual).
- `Released` — first printed actual tiba.
- `Revised` — later revised value tiba.
- `Rescheduled` — source pindah release instant (auditable, alertable).
- `Rescored` — impact score dihitung ulang di bawah model version baru.

Querying `as of` past instant mengembalikan exactly revision yang dikenal kemudian — guarantee yang membunuh
look-ahead dalam backtested news rules.

## Forecast / consensus

Median survey dari ekonom adalah **tidak** freely published oleh primary sources — ini adalah
aggregators' proprietary value-add, dan kami tidak fabricate itu. Event schema membawa nullable
`Forecast`; deployment dapat wire licensed consensus feed melalui optional `IForecastProvider`
port (bring-your-own key, off by default). Previous values dan revisions selalu datang dari official
source.

## Data sources

Dua decoupled layers, semua primary — tidak pernah aggregator:

- **Schedule / timing:** FRED release calendar; national statistical agencies (BLS, BEA, Census,
  Eurostat, ONS, Destatis, INSEE, e-Stat, ABS, StatCan); central-bank meeting calendars (Fed, ECB,
  BoE, BoJ, RBA, BoC, SNB, RBNZ).
- **Actual values:** FRED (dengan vintage dates untuk revisions dan point-in-time), ditambah BLS, BEA, Census,
  ECB SDW, Eurostat dan OECD SDMX APIs.

Dead source degrades coverage untuk **source itu saja**; kalender terus melayani segalanya
dan surfaces gap sebagai freshness metric.

## Rate limiting & backup plan

External providers publish rate limits (FRED memungkinkan ~120 requests/minute). Kalender dibangun jadi
**tidak pernah trips provider's limit**, dan jadi being throttled atau cut off tidak pernah degrades reads:

- **Proactive throttling.** Setiap source's HTTP client goes through shared, thread-safe rate gate
  yang spaces outbound requests ke configured budget (`App:Calendar:FredRequestsPerMinute`, default
  100 — deliberately di bawah provider ceiling). Requests diqueuekan dan dipaced, tidak pernah bursted.
- **Honour `429 Retry-After`.** Jika provider pernah return `429 Too Many Requests`, gate backs
  whole source off oleh server-requested cooldown (atau `App:Calendar:RateLimitBackoff`, default 60s)
  sebelum next call — tidak ada tight retry loop.
- **Standard resilience.** Setiap source client juga inherits app-wide resilience handler (retry dengan
  backoff + jitter, circuit breaker, timeouts), jadi transient blips diserap dan persistently
  failing source adalah parked (coverage-nya goes stale) tanpa affecting yang lain.
- **Backup plan — durable read-through cache.** Reads **tidak pernah** disajikan dengan calling
  provider. Sekali range diambil itu adalah persisted append-only ke Postgres dan disajikan dari sana
  selamanya setelahnya (lihat §"On-demand load"). Jadi bahkan ketika source rate-limited atau down, kalender
  terus menjawab dari cached, point-in-time-correct data; missing span hanya tetap uncovered dan
  dicoba kembali pada next ingestion cycle. Blackout answers juga fail ke conservative default
  di bawah uncertainty, jadi data gap tidak pernah green-lights trading through release.
- **Cheap polling.** Conditional fetch (ETag / If-Modified-Since / source vintage cursors) dan
  "fetch span sekali, tidak pernah lagi" cache menjaga actual request volume jauh di bawah limit apa pun
  dalam normal operation — rate gate adalah safety net, tidak common path.

## Enable / disable

Dua independent tiers, exactly seperti cMind features lainnya:

- **Tier 1 — runtime feature toggle** (`Feature.EconomicCalendar`) flipped dari Features admin UI;
  tidak ada redeploy, takes effect live.
- **Tier 2 — white-label hard gate** (`App:Branding:EnableEconomicCalendar`, default `true`). Reseller
  mengatur itu `false` untuk remove feature sepenuhnya; operator tidak dapat re-enable itu.

Effective state adalah `Branding.EnableEconomicCalendar && FeatureToggle.EconomicCalendar`. Ketika disabled,
nav entry hidden dan `/economic-calendar`, `/api/calendar/**` dan MCP calendar tools return
clean feature-disabled `404` — tidak pernah `500`. Persisted history retained pada runtime toggle-off
jadi re-enabling adalah instant.

## Rollout phases

- **P0 — domain core** *(implemented)*: aggregates, value objects, ports, impact model,
  country→symbol mapping, news-window policy, two-tier gating, full unit suite.
- **P1 — persistence + one source** *(implemented)*: EF `calendar` schema (own tables, append-only,
  hot indexes), read-through `IEconomicCalendar` reader dengan point-in-time `asOf`, idempotent
  append-only write service, FRED connector di belakang resilient typed client, dan config-gated
  ingestion worker; Testcontainers integration tests (persistence, PIT, idempotency, blackout).
- **P2 — public JWT REST API + Web UI** *(implemented)*: versioned, JWT-secured `/api/calendar/v1`
  API — client issuance, token exchange, dan core read endpoints (events, history, series,
  surprises, next, blackout, affected-symbols, health) dengan scope enforcement dan two-tier gating,
  integration-tested. Plus mobile-first **`/economic-calendar` page** — gated, fully-localized
  (23 languages) agenda dari upcoming releases sebagai phone-friendly cards dengan colour-banded impact chips
  dan MudBlazor **filter dialog** (currencies + minimum impact + **From-date** picker untuk jump ke
  **any** past date di seluruh full history — tidak ada 60-day cap, tidak ada wall); nav entry, smoke/mobile/a11y/E2E
  tested. **Per-indicator series history page** (`/economic-calendar/series/{code}`, linked dari each
  event) lists series' full print history. Surprise charts + infinite-scroll browser follow.
- **P3 — more sources & warm-up** *(started)*: **core-series catalog** (CPI, Core CPI, NFP,
  unemployment, GDP, PCE, Fed funds, retail sales → their FRED ids) seeded automatically pada startup,
  dan one-time, idempotent, year-chunked **proactive backfill** pulls their ≥10-year history jadi
  common case warm tanpa waiting untuk user miss. **Ingestion adalah on by default**
  (`App:Calendar:IngestionEnabled`, default `true`): **central-bank schedule source** memerlukan **no API
  key**, jadi FOMC / ECB / BoE decision calendar populates out of the box — backfill seeds meeting dates
  di seluruh **kedua recent history dan forward horizon**, jadi browsing *last month* (atau any
  past window) shows meetings bahkan sebelum any FRED/BLS key dikonfigurasi; value series fill dalam
  sekali keys-nya diatur. Workers honour calendar's two-tier gate — white-label deployment atau
  owner disabling economic-calendar feature stops ingestion, dan `App:Calendar:IngestionEnabled=false`
  turns itu off explicitly. **Per-source freshness** sekarang real juga: worker records setiap source's last
  successful poll, consecutive-failure count dan tripped-circuit flag (persisted dalam app settings,
  cross-process), dan `/health` endpoint + `calendar_health` MCP tool report truthful `stale`
  verdict per source. **BLS** (2nd value source) dan **central-bank schedule source** (FOMC / ECB /
  BoE decision dates, backfilled lintas history dan synced forward ke horizon window oleh worker)
  ada. Masih datang: BEA/Census/ECB-SDW/Eurostat/OECD value sources dan reconciliation pass.
- **P4 — deep integration**: **MCP tools** *(implemented — full read-API parity: `calendar_events`,
  `calendar_event`, `calendar_history`, `calendar_series`, `calendar_surprises`, `calendar_next`,
  `calendar_blackout`, `calendar_affected_symbols`, `calendar_health`, gated pada feature)* dan
  **alerts `EconomicEvent` trigger** *(implemented — `AlertRule` yang fires N minutes ahead dari
  upcoming release pada/above chosen impact, optionally narrowed ke currencies; evaluated oleh
  existing alert worker tanpa AI, de-duplicated per release; created via
  `POST /api/alerts/rules/economic-event`)*. Prop-guard news-blackout gate **dan
  copy-trade blackout pause** ada (§5.1 — opt-in `App:Copy:NewsPauseEnabled`, default off: source
  open yang symbol-nya duduk dalam Critical-impact blackout dilewati, byte-identical hot path ketika off). **Backtest
  event overlay** ada — `GET /api/calendar/v1/for-symbol` dan
  `calendar_events_for_symbol` MCP tool return point-in-time-correct events mempengaruhi symbol dalam
  window, dan **instance/backtest report page** renders high-impact releases yang jatuh dalam backtest window
  di bawah equity curve (jadi author melihat trades mana yang mendarat pada NFP), gated dan
  localized. Whole plan sekarang implemented.
- **P5 — extras**: surprise analytics, iCal/CSV export, keyword search, pluggable consensus.

Lihat [cBot & REST API reference](calendar-cbot-api.md) untuk integration surface.
