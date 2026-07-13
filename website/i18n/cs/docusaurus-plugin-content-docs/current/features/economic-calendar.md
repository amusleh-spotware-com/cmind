# Ekonomický kalendář

cMind dodává **svůj vlastní** ekonomický kalendář — plán uvolnění, aktuální hodnoty, předpovědi, revize a
daty řízený model dopadu — zdroje z **primárních autorit** (centrální banky a národní
statistické agentury), se **žádnou závislostí** na ForexFactory, FXStreet, Investing.com nebo jakémkoliv
aggregátoru. Je point-in-time správný, uchovává ≥10 let historie a je propojen do tradingu, public
API, MCP, cBots, AI, alertů a backtestů. Je to dekaplový modul: lze ho deaktivovat s
nulovým dopadem na trading core.

> **Status.** P0–P4 jsou implementovány a dodány. Doménový core, persistence (EF `calendar` schema,
> append-only read/write, zdroje FRED + BLS + central-bank-schedule, config-gated ingestion worker
> s per-source tracking čerstvosti), verzované JWT REST API, mobile-first `/economic-calendar`
> UI, MCP tools, cBot JWT API, upozornění na high-impact události, copy-trade news-blackout pauza,
> backtest event overlay, SSE stream, HMAC-signed webhooks a typovaný `CmindCalendarClient` jsou
> všechny implementovány a integration-testovány. Extras P5 (surprise analytics, iCal/CSV export,
> keyword search, pluggable consensus) jsou zbývající položky — viz rollout fáze níže.

## Co ho dělá jiným

Opakující se stížnosti na přední kalendáře se staly našimi design constraints:

- **Žádné tiché změny ratingu dopadu.** Náš rating dopadu je **deterministický, verzovaný a
  auditovatelný**. Každá změna je zaznamenaná revize s časovým razítkem — nikdy ne tichý overwrite. Uživatel
  může přesně vidět *proč* je událost High.
- **Jeden UTC anchor per událost.** Každá událost je ukotvena k jednomu UTC okamžiku z oficiálního
  plánu primárního zdroje; vlastní timezone zdroje je uložena, a per-user rendering používá
  explicitní IANA timezone s DST handled by zone database — nikdy manuální ±1h toggle.
- **Plné revision chains, všude.** Původní hodnota a každá revize jsou first-class, vystaveny
  identicky přes API, MCP a cBot surfaces.
- **≥10 let historie, žádná zeď.** Neomezený rozsah prohlížení; žádný 60denní cap, žádná registrační brána.
- **Point-in-time by construction.** Každý fakt nese `KnownAt` (kdy *my* jsme se to dověděli) a
  `EffectiveAt` (okamžik události). "Jak kalendář vypadal v čase T" je first-class query, takže
  backtestované news pravidlo se chová přesně jako live — žádný look-ahead z použití revidovaných hodnot v historii.

## Model dopadu

Skóre dopadu je pure, deterministic function in `[0, 100]`, banded to Low / Medium / High /
Critical. Jeho vstupy jsou pouze data známá v čase skórování (žádný future leak):

- **Series prior** — baseline váha per třída indikátoru (rozhodnutí o sazbě outweighs CPI, které
  outweighs minor survey).
- **Realized-volatility footprint** — median absolutního returnu primárně affected symbols in
  window after this series' *past* releases: "this release historically moves price this much."
- **Surprise sensitivity** — jak silně absolutní surprise (z-score) historically
  correlated with post-release move.

Skóre blenduje tyto s fixed weights a stamps an `ImpactModelVersion`. Recompute je
explicit, logged operation which produces a **new revision** — nikdy mutate — takže skóre je vždy
reproducible from its inputs.

## Country → currency → symbol mapping

Nejčastěji citovaný algo integration papercut vyřešen jednou, jako pure function: country mapuje k
jeho měně (každý euro-area member fans in to EUR), a currency mapuje k watchlist symbols
quoting it on either leg. Takže **EURUSD je affected both EU and US events**; XAUUSD je USD-exposed;
US500 maps to USD. To pohání news filter, affected-symbols resolution a blackout math.

## News-window politika

A `NewsWindowRule` je `{ minImpact, beforeMinutes, afterMinutes, currencies?, series? }`. A single,
shared, pure implementation answers "is instant T inside a blackout for symbol S?" — used by the cBot
news filter, copy-trade pause a AI risk guard, takže nemohou nikdy divergovat. On uncertainty the
blackout answer defaultuje ke konfigurované konzervativní hodnotě (fail-closed by default) takže data gap
nikdy neutišeně green-lights trading through high-impact release.

## Point-in-time & revisions

Aktuální hodnoty, předpovědi a skóre dopadu jsou **append-only**. Každá událost vlastní ordered chain of
revisions, monotonic in `KnownAt`:

- `Scheduled` — událost byla poprvé naplánována (prior impact, žádný actual).
- `Released` — dorazila první vytištěná aktuální hodnota.
- `Revised` — dorazila pozdější revidovaná hodnota.
- `Rescheduled` — zdroj posunul release instant (auditabilní, alertable).
- `Rescored` — skóre dopadu bylo přepočítáno pod nové verze modelu.

Querying `as of` past instant vrací přesně revizi known then — záruka která zabíjí
look-ahead v backtested news rules.

## Forecast / consensus

Průzkumový medián ekonomů **není** volně publikován primárními zdroji — je to
aggregátorova proprietary value-add, a my ho nefabrikuje. Event schema nese nullable
`Forecast`; nasazení může wire optional `IForecastProvider` port (bring-your-own key, off by default). Previous values and revisions vždy přicházejí z official source.

## Zdroje dat

Dvě dekaplované vrstvy, vše primární — nikdy aggregátor:

- **Plán / timing:** FRED release calendar; národní statistické agentury (BLS, BEA, Census,
  Eurostat, ONS, Destatis, INSEE, e-Stat, ABS, StatCan); kalendáře centrální bank (Fed, ECB,
  BoE, BoJ, RBA, BoC, SNB, RBNZ).
- **Aktuální hodnoty:** FRED (with vintage dates for revisions and point-in-time), plus BLS, BEA, Census,
  ECB SDW, Eurostat and OECD SDMX APIs.

Mrtvý zdroj degraduje pokrytí pouze **pro ten zdroj**; kalendář nadále slouží všechno ostatní
a surfaces gap jako freshness metric.

## Rate limiting & záložní plán

Externí provideri publikují rate limity (FRED allows ~120 requests/minute). Kalendář je built takže nikdy
**nevyhodí provider's limit**, a že being throttled nebo cut off nikdy nezmírní čtení:

- **Proaktivní throttling.** Každý HTTP klient zdroje jde přes shared, thread-safe rate gate
  that spaces outbound requests to a configured budget (`App:Calendar:FredRequestsPerMinute`, default
  100 — deliberateně pod provider ceiling). Requests are queued and paced, nikdy bursted.
- **Honour `429 Retry-After`.** Pokud provider někdy vrátí `429 Too Many Requests`, gate backing
  the whole source off by server-requested cooldown (or `App:Calendar:RateLimitBackoff`, default 60s)
  before the next call — žádný tight retry loop.
- **Standard resilience.** Každý source klient also inherits app-wide resilience handler (retry with
  backoff + jitter, circuit breaker, timeouts), takže transient blips jsou absorbovány a persistently
  failing source is parked (jeho pokrytí jde stale) without affecting others.
- **Záložní plán — the durable read-through cache.** Čtení jsou **nikdy** servírován voláním
  providera. Once a range is fetched it is persisted append-only to Postgres and served from there
  forever after (viz §"On-demand load"). Takže i když je zdroj rate-limited nebo down, kalendář
  nadále odpovídá z cached, point-in-time-correct data; chybějící span simply stays uncovered and
  is retried on next ingestion cycle. Blackout answers additionally fail to the conservative
  default under uncertainty, takže data gap nikdy neutišeně green-lights trading through a release.
- **Levné polling.** Conditional fetch (ETag / If-Modified-Since / source vintage cursors) and the
  "fetch a span once, never again" cache keep actual request volume far below any limit in normal
  operation — rate gate is a safety net, not the common path.

## Enable / disable

Dvě nezávislé vrstvy, přesně jako ostatní cMind funkce:

- **Tier 1 — runtime feature toggle** (`Feature.EconomicCalendar`) flipped from Features admin UI;
  no redeploy, takes effect live.
- **Tier 2 — white-label hard gate** (`App:Branding:EnableEconomicCalendar`, default `true`). A
  reseller nastaví `false` pro úplné odebrání funkce; operátor pak nemůže znovu povolit.

Effective state is `Branding.EnableEconomicCalendar && FeatureToggle.EconomicCalendar`. When disabled,
nav entry is hidden and `/economic-calendar`, `/api/calendar/**` a MCP calendar tools return
clean feature-disabled `404` — nikdy `500`. Persisted history is retained on a runtime toggle-off
takže re-enabling je okamžitý.

## Rollout fáze

- **P0 — doménový core** *(implementováno)*: aggregates, value objects, ports, impact model,
  country→symbol mapping, news-window policy, two-tier gating, full unit suite.
- **P1 — persistence + one source** *(implementováno)*: EF `calendar` schema (own tables, append-only,
  hot indexes), the read-through `IEconomicCalendar` reader with point-in-time `asOf`, the idempotent
  append-only write service, FRED konektor behind resilient typed client, and config-gated
  ingestion worker; Testcontainers integration tests (persistence, PIT, idempotency, blackout).
- **P2 — public JWT REST API + Web UI** *(implementováno)*: versioned, JWT-secured `/api/calendar/v1`
  API — client issuance, token exchange, and core read endpoints (events, history, series,
  surprises, next, blackout, affected-symbols, health) with scope enforcement and two-tier gating,
  integration-tested. Plus the mobile-first **`/economic-calendar` page** — gated, fully-localized
  (23 languages) agenda of upcoming releases jako phone-friendly cards with colour-banded impact chips
  a MudBlazor **filter dialog** (currencies + minimum impact + **From-date** picker to jump to
  **jakékoliv** minulé datum across full history — žádný 60denní cap, žádná zeď); nav entry, smoke/mobile/a11y/E2E
  tested. A **per-indicator series history page** (`/economic-calendar/series/{code}`, linked from each
  event) lists a series' full print history. Surprise charts + infinite-scroll browser follow.
- **P3 — more sources & warm-up** *(started)*: a **core-series catalog** (CPI, Core CPI, NFP,
  unemployment, GDP, PCE, Fed funds, retail sales → their FRED ids) is seeded automatically on startup,
  a one-time, idempotent, year-chunked **proactive backfill** pulls their ≥10-year history so the
  common case is warm without waiting for user miss. **Ingestion is on by default**
  (`App:Calendar:IngestionEnabled`, default `true`): the **central-bank schedule source** needs **žádný API
  key**, takže FOMC / ECB / BoE decision calendar populates out of the box — the backfill seeds those
  meeting dates across **both recent history and the forward horizon**, takže browsing *last month* (nebo jakékoliv
  past window) shows the meetings even before any FRED/BLS key is configured; value series fill in
  once their keys are set. Workers honour the calendar's two-tier gate — a white-label deployment or
  owner disabling the economic-calendar feature stops ingestion, and `App:Calendar:IngestionEnabled=false`
  turns it off explicitly. **Per-source freshness** is now real too: worker records each source's last
  successful poll, consecutive-failure count a tripped circuit flag (persisted in app settings,
  cross-process), and `/health` endpoint + `calendar_health` MCP tool report truthful `stale`
  verdict per source. **BLS** (a 2nd value source) and the **central-bank schedule source** (FOMC / ECB /
  BoE decision dates, backfilled across history and synced forward into a horizon window by worker)
  are in. Still to come: BEA/Census/ECB-SDW/Eurostat/OECD value sources and reconciliation pass.
- **P4 — deep integration**: **MCP tools** *(implementováno — full read-API parity: `calendar_events`,
  `calendar_event`, `calendar_history`, `calendar_series`, `calendar_surprises`, `calendar_next`,
  `calendar_blackout`, `calendar_affected_symbols`, `calendar_health`, gated on the feature)* a
  **alerts `EconomicEvent` trigger** *(implementováno — `AlertRule` that fires N minutes ahead of an
  upcoming release at/above a chosen impact, volitelně narrowed to currencies; evaluated by the
  existing alert worker with no AI, de-duplicated per release; created via
  `POST /api/alerts/rules/economic-event`)*. Prop-guard news-blackout gate **and copy-trade blackout pause** are in (§5.1 — opt-in `App:Copy:NewsPauseEnabled`, default off: source
  open whose symbol sits in Critical-impact blackout is skipped, byte-identical hot path when off). The
  **backtest event overlay** is in — `GET /api/calendar/v1/for-symbol` a
  `calendar_events_for_symbol` MCP tool return the point-in-time-correct events affecting a symbol in a
  window, and **instance/backtest report page** renders the high-impact releases that fell inside the
  backtest window beneath the equity curve (so author sees which trades landed on NFP), gated and
  localized. Celý plán je nyní implementován.
- **P5 — extras**: surprise analytics, iCal/CSV export, keyword search, pluggable consensus.

Viz [cBot & REST API reference](calendar-cbot-api.md) pro integration surface.
