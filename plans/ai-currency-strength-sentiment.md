# AI Macro Currency Strength & Forward Outlook

**Goal:** a new AI-backed tool that (a) ranks a **configurable universe of currencies — the 8 majors
plus emerging-market and exotic currencies** — by **current** fundamental strength, and (b) projects
a **forward directional outlook** — how each currency is likely to do **against every other** over a
chosen horizon (1M / 3M / 6M / 12M), driven by fundamentals + monetary-policy trajectory +
geopolitics. It shows *why* each rank and each **pair** outlook is what it is through interactive
charts, and surfaces a compact **dashboard widget** that each user opts into from their own settings —
only after the AI key is configured (deployment-level) **and** the user enables the widget (per-user).

**Currency universe (tiered, config-driven — not a hardcoded 8):**
- **Majors** — USD, EUR, GBP, JPY, AUD, NZD, CAD, CHF.
- **Emerging markets** — e.g. CNH, INR, BRL, MXN, ZAR, KRW, SGD, TWD, PLN, IDR, THB (+ Scandi
  NOK/SEK sit between major and EM).
- **Exotics** — e.g. TRY, HKD, HUF, CZK, CLP, COP, PHP, MYR, AED/SAR (USD-pegged), plus long-tail.
- The set is **deployment-configurable** (`AppOptions`), defaults to majors + a curated EM/exotic
  list, and each currency carries a **`CurrencyTier`** that tunes weighting, dead-band, and the data
  caveat (below). Adding a currency is config, not code.

**The forward view is the headline.** The "who's strong now" ranking is the base layer; on top sits a
**pair-outlook matrix** (28 crosses) giving a directional bias (appreciate / neutral / depreciate) +
conviction + the driver breakdown behind it, per horizon — "EUR/USD: bullish over 3M because ECB on
hold vs Fed cutting, EU growth improving, risk-neutral geopolitics".

**House bars (non-negotiable, per CLAUDE.md):** strict DDD, three test tiers every change (unit +
integration + E2E, failure paths included), zero warnings, no `DateTime.UtcNow`, no secrets/magic
strings/raw logging, dialogs + mobile-first + branded UI, docs in the same commit.

---

## 0. Design north star

The durable, testable edge is **deterministic macro scoring + a deterministic forward projection**,
not "AI predicts price". Research (below) shows currency-strength ranking is a *weighted aggregation
of macro drivers*, and the forward view is a *deterministic projection of those drivers along their
expected trajectory* (rate path, inflation trend, growth momentum, geopolitical shift) — pure math in
`src/Core`, fully unit-testable with zero flaky external calls.

**The LLM never invents a rank, a direction, or a number.** Its job is narrow and degradable:
1. **Gather** — for each major, the *current* fundamentals **and** their *forward* trajectory:
   expected policy-rate path over the horizon, inflation trend vs target, growth momentum, and a
   geopolitical outlook (risk-on/off, tariffs, fiscal/debt, elections) — returned as **structured
   JSON** matching a strict schema.
2. **Explain** — narrate the deterministic ranking *and* the pair outlook in plain language.

Core computes both the current ranking **and** the forward pair-outlook matrix deterministically from
the gathered indicators + trajectories. If the AI is off, the key is missing, or the JSON is malformed
→ the tool degrades gracefully (widget hidden / last snapshot shown / clear error), and **the app runs
unchanged** — same contract as every other AI feature (`AiResult.Fail`, gated on `AppOptions.Ai.ApiKey`).

---

## 1. Research findings (grounding the model)

### 1.1 Two methodologies — we use **fundamental/macro**, not price-based
- **Price-based** currency-strength meters aggregate % change across the 28 major crosses (DXY-style).
  Fast but pure momentum; we already surface price/backtest data elsewhere.
- **Fundamental/macro** meters aggregate central-bank rates, CPI/inflation, GDP, employment, trade
  balance, and policy stance into a relative score, then **rank the currency universe** (majors + EM +
  exotics). This can *lead* price and is the institution-grade, explainable signal retail rarely gets.
  **This is our tool.**

### 1.2 The drivers and their sign (from CFA parity conditions + institutional 2026 outlooks)
| Driver | Effect on strength | Notes |
|---|---|---|
| **Interest-rate level & trajectory** (policy rate + hike/hold/cut path) | Higher / hawkish ⇒ **stronger** | Highest weight. Divergence between central banks drives the biggest ranking gaps. |
| **Inflation (CPI vs target)** | High/above-target ⇒ **weaker** (purchasing-power drag) | Scored inversely; but *rising* inflation can pull hikes → interacts with rate trajectory. |
| **GDP growth (relative)** | Higher relative growth ⇒ **stronger** | Measured as differential vs benchmark (USD/EUR area). |
| **Employment** (unemployment rate / job growth) | Stronger labour ⇒ **stronger** | Feeds the policy path. |
| **Trade balance / current account** | Surplus ⇒ **stronger** | Structural currency demand. |
| **Monetary-policy stance** (hawkish/neutral/dovish) | Hawkish ⇒ **stronger** | Central-bank policy = the primary long-term driver. |
| **Geopolitical / risk sentiment** | Risk-off ⇒ safe havens (USD, JPY, CHF) **stronger** | Also captures tariffs, fiscal/debt concerns, elections. |
| **Real yield / carry** *(EM/exotic-weighted)* | Positive real rate ⇒ **stronger** (inflows) | Dominant EM driver in calm regimes; sharp reversal risk-off. |
| **External vulnerability** *(EM/exotic)* | CA/fiscal deficit, low reserves, USD debt ⇒ **weaker** | Structural EM depreciation pressure. |
| **Terms of trade** *(commodity exporters)* | Rising export prices ⇒ **stronger** | BRL, ZAR, CLP, IDR, NOK, AUD, CAD. |
| **Political / institutional risk** *(EM/exotic)* | Instability / unorthodox policy ⇒ **weaker** | Wider dead-band, capped conviction (e.g. TRY). |

### 1.3 Method (institutional practice — Macrosynergy point-in-time, CFA parity)
- Score each driver as a **differential vs a benchmark** (USD or EUR area), **normalize** across the
  panel of 8, **winsorize** outliers, then **weight-sum** into one composite per currency, and **rank**.
- Honest calibration to bake into UX copy: fundamentals predict **medium-to-long-term** value well,
  **short-term poorly**. Present as a *positioning/confluence filter*, never a standalone signal.
  Add the standard caution: readings near high-impact releases (NFP/CPI/central-bank) are noisy.

### 1.5 Forward outlook — projecting the drivers (CFA parity + institutional trajectory scoring)
The forward view is **not price prediction**; it is the *current strength differential carried along
each driver's expected trajectory*, which is exactly how bank/fund FX desks frame a 3–12M view:
- **Rate trajectory dominates** — the *change* in the policy-rate differential (who is hiking vs
  cutting over the horizon) is the biggest forward driver; divergence widens/narrows the gap (e.g.
  2026: ECB-on-hold vs Fed-cutting ⇒ EUR forward-positive; BoJ hiking vs Fed cutting ⇒ JPY-positive).
- **Inflation trend** — moving toward vs away from target shifts the expected policy path (feeds rate
  trajectory) and real-yield.
- **Growth momentum** — accelerating vs decelerating relative growth.
- **Geopolitical outlook** — forward risk regime (risk-off favours USD/JPY/CHF; tariffs, fiscal/debt,
  elections). This is the geopolitics leg the user asked for, scored as a bounded forward risk delta.
- **Horizon scales the projection** — longer horizon ⇒ trajectory terms weigh more vs the current
  level; near-term ⇒ current level dominates. Convention: **buy the currency projected strongest,
  sell the weakest** for the horizon. Honest caveat stays: medium/long-term positioning, not timing.

### 1.6 Emerging-market & exotic currencies — extra drivers, weaker data
Majors and EM/exotics share the driver skeleton but EM/exotic strength leans on **additional**
factors and is **less data-reliable**, so the tool treats them by tier:
- **Carry / real-yield** — high nominal + positive real rates attract carry inflows; the dominant EM
  FX driver in calm regimes (and the sharpest reversal in risk-off).
- **Risk regime (heavier weight)** — EM/exotics are high-beta to global risk-on/off; the geopolitics
  leg carries **more** forward weight than for majors.
- **External vulnerability** — current-account/fiscal deficits, FX reserves, external-debt/USD funding
  → depreciation pressure; commodity terms-of-trade for commodity exporters (BRL, ZAR, CLP, IDR).
- **Political / institutional risk** — elections, central-bank independence, capital controls,
  unorthodox policy (e.g. TRY) → large idiosyncratic moves; wider dead-band, capped conviction.
- **Managed regimes / pegs** — USD-pegged (HKD, AED, SAR) or heavily-managed (CNH) currencies have
  suppressed variance; the model down-weights trajectory and flags the peg so it isn't read as a free-
  floating signal.
- **Data caveat** — official EM stats are lower-frequency, revised, sometimes opaque; the AI-gathered
  numbers get a **per-tier confidence** and wider validation bands; UX shows a reliability badge.

### 1.4 Sources
- [Currency strength — Wikipedia](https://en.wikipedia.org/wiki/Currency_strength) (price vs fundamental methodology)
- [CFA Institute — Currency Exchange Rates: Understanding Equilibrium Value](https://www.cfainstitute.org/insights/professional-learning/refresher-readings/2026/currency-exchange-rates-understanding-equilibrium-value) (parity conditions)
- [Macrosynergy — Systematic FX with point-in-time GDP](https://macrosynergy.com/research/systematic-fx-trading-with-point-in-time-gdp-growth-estimates/) (normalize/winsorize scoring)
- [MUFG 2026 G10 FX Outlook](https://www.mufgresearch.com/fx/fx-focus-g10-fx-2026-outlook-in-a-post-peak-usd-world-19-december-2025/), [IG 2026 FX outlook](https://www.ig.com/en/news-and-trade-ideas/forex-market-outlook-for-2026-251211) (driver sign, current regime)
- [NordFX](https://nordfx.com/en/traders-guide/currency-strength-meter), [FXPro](https://www.fxpro.com/amp/en/help-section/education/beginners/articles/forex-currency-strength-meter-complete-trading-guide.amp.html) (rate/inflation as primary driver, news-window caution)

---

## 2. Where it plugs into the existing app

Verified against the codebase (patterns to reuse, not reinvent):

- **AI contract** — `src/Core/Ai/AiContracts.cs`: `IAiClient` (web search via `AiTextRequest.EnableWebSearch`),
  `IAiFeatureService`, `AiResult`, `IAiKeyStore`. Already has `MarketSentimentAsync(symbol)` +
  `AiSentiment.razor` (per-symbol *text*). Our tool is the **portfolio-wide, structured, ranked**
  successor — reuse the plumbing, add structured output.
- **Feature gate** — `Core.Features.FeatureFlag.Ai` + `IFeatureGate`; endpoints use
  `.RequireFeature(FeatureFlag.Ai)` (see `AiEndpoints.cs`). Deployment-level on/off.
- **AI key gate** — `IAiKeyStore.HasKey`; `IAiFeatureService.Enabled`; `/api/ai/status`.
  `AiFeaturePageBase` + `AiFeatureNotice` already disable actions until a key exists.
- **AI page pattern** — `src/Web/Components/Pages/Ai/*.razor` inherit `AiFeaturePageBase`.
- **Dashboard** — `src/Web/Components/Pages/Index.razor` composes `Dash*` widgets; charts use
  **ApexCharts** (`DashActivityChart.razor` is the reference: themed via `appReadTokens`, dark mode,
  in-place `UpdateSeriesAsync`, no remount).
- **Settings** — `AiSettings.razor` (Owner, key entry), `FeatureSettings.razor` (Owner, deployment
  flags). **Gap:** there is **no per-user preference store today** — we add one (§3.4).
- **MCP** — `src/Mcp/Tools/AiTools.cs` exposes AI features to agents; add a `currency_strength` tool.
- **Options/constants** — `AppOptions.Ai` (Model, MaxTokens), `AiConstants` (tokens/model/web-search
  tool names). Add currency-strength constants + tuned token budget there (no magic strings).

---

## 3. Architecture — layer by layer (strict DDD)

### 3.1 `src/Core` — pure domain (the deterministic heart, zero infra deps)

New folder `src/Core/Ai/CurrencyStrength/`:

- **`Currency` value object** — wraps any ISO-4217 code in the configured universe (majors + EM +
  exotics), **not** a fixed 8. Validates against the deployment universe; unknown/ill-formed code ⇒
  `DomainException` (new `DomainErrors` entries). No primitive `string` currency crosses a boundary.
- **`CurrencyTier` enum** — `Major`, `EmergingMarket`, `Exotic` — carried by each currency; drives
  tier-aware weighting, dead-band width, and conviction cap.
- **`CurrencyUniverse` VO** — the ordered set of `(Currency, CurrencyTier, bool IsPegged, Currency?
  PegAnchor)` in play, built from config; the calculators operate over *whatever* the universe holds
  (N currencies, N×N matrix), so majors-only and majors+EM+exotics are the same code path.
- **`MacroDriver` enum** — majors + shared: `PolicyRate`, `RateTrajectory`, `Inflation`, `GdpGrowth`,
  `Employment`, `TradeBalance`, `PolicyStance`, `GeopoliticalRisk`; **EM/exotic-relevant:** `RealYield`
  (carry), `ExternalVulnerability` (CA/fiscal/reserves/external-debt), `PoliticalInstitutionalRisk`,
  `TermsOfTrade` (commodity exporters). Drivers absent for a tier get zero weight, not a bad guess.
- **`CurrencyIndicators` VO** — raw per-currency inputs (rate %, CPI %, target %, GDP % YoY,
  unemployment %, trade-balance %GDP, stance enum, geo-risk enum) **plus EM/exotic fields** (real
  yield %, FX-reserves-months / reserve-adequacy, external-debt %GDP, terms-of-trade signal, political
  /institutional-risk enum) and a **`DataConfidence`** (per-tier, AI-reported). Immutable `record`;
  tier-aware range guards (exotic hyperinflation/negative-reserve cases allowed but flagged).
- **`DriverScore` VO** — `(MacroDriver Driver, double Normalized, double Weight, double Contribution,
  string Rationale)`. The per-driver breakdown that powers the "why" charts.
- **`CurrencyStrengthScore` VO** — `(Currency, double Composite, IReadOnlyList<DriverScore> Breakdown)`.
- **`CurrencyStrengthRanking` VO/aggregate root** — the full ranked table for one point in time:
  ordered `IReadOnlyList<CurrencyStrengthScore>`, `Rank(Currency)`, strongest/weakest, `AsOf`
  (`DateTimeOffset` passed in — **never** `UtcNow`).
- **`CurrencyStrengthCalculator` domain service** — the deterministic engine:
  `Compute(IReadOnlyList<CurrencyIndicators> panel, StrengthWeights weights, DateTimeOffset asOf)`.
  Steps mirror institutional method: per-driver **differential vs panel mean → normalize (z-score)
  → winsorize → weight-sum → rank** (deterministic, stable tie-break by ISO code). Pure, no I/O.
- **`StrengthWeights` VO** — **tier-keyed** driver weights (majors: rate-level highest; EM/exotic:
  carry + risk + external-vulnerability weighted up), each tier's weights validated to sum to 1;
  overridable without touching the engine. Pegged currencies down-weight trajectory drivers.
- **Tier handling in the engine** — normalization is computed **within comparable groups** (avoid a
  50%-inflation exotic distorting the majors' z-scores) then reconciled onto one cross-universe scale;
  exotics get a **wider dead-band** and **capped conviction** so thin/opaque data can't manufacture a
  high-confidence call. A pegged currency's outlook is clamped toward `Neutral` and flagged.

**Forward outlook (the headline layer):**
- **`Horizon` enum** — `OneMonth`, `ThreeMonths`, `SixMonths`, `TwelveMonths`.
- **`CurrencyTrajectory` VO** — per-currency forward inputs: expected policy-rate path (bp over
  horizon), inflation trend vs target, growth momentum, geopolitical forward-risk delta. Immutable,
  range-guarded. (Gathered by AI alongside the current `CurrencyIndicators`.)
- **`CurrencyForecast` VO** — `(Currency, Horizon, double ProjectedScore, IReadOnlyList<DriverScore>
  ForwardBreakdown)` — the current composite carried along the trajectory.
- **`DirectionalBias` enum** — `Appreciate`, `Neutral`, `Depreciate` (with dead-band thresholds so a
  tiny differential reads `Neutral`, not false conviction).
- **`PairOutlook` VO** — `(Currency Base, Currency Quote, Horizon, DirectionalBias Bias, double
  Conviction, double ProjectedDifferential, IReadOnlyList<DriverScore> WhyBreakdown)` — the forward
  view for one cross (e.g. EUR/USD @ 3M).
- **`PairOutlookMatrix` VO/aggregate** — all 28 (ordered 56 directional) crosses for one horizon;
  `For(base, quote)`, strongest-vs-weakest pick, `AsOf`, `Horizon`.
- **`ForwardOutlookCalculator` domain service** —
  `Project(panel, trajectories, StrengthWeights, ForwardWeights, Horizon, asOf)`:
  1. per-currency `ProjectedScore = currentComposite + horizonScale(Horizon) · Σ trajectoryDriver·weight`;
  2. per pair, `ProjectedDifferential = forecast(base) − forecast(quote)`;
  3. map differential → `DirectionalBias` via dead-band; `Conviction = |differential|` normalized.
  Deterministic, pure, no I/O; `horizonScale` weights trajectory terms more at longer horizons.
- **`ForwardWeights` VO** — trajectory-driver weights (rate-path highest, geopolitics bounded),
  validated sum-to-1.

**Why in Core:** every ranking **and every forward direction/number** is reproducible from inputs ⇒
exhaustive unit tests, no flaky external dependency, no LLM anywhere in the math path.

### 3.2 AI gathering + explanation contract (Core interface, Infra impl)

Extend `IAiFeatureService` (`AiContracts.cs`) + `AiFeatureService` (`Infrastructure/Ai`):

```csharp
// One gather returns BOTH the current indicators AND the forward trajectory per major.
Task<AiResult> GatherCurrencyMacroAsync(CancellationToken ct);           // JSON: 8× {indicators, trajectory}
Task<AiResult> ExplainCurrencyOutlookAsync(string rankingJson, string pairOutlookJson, int maxTokens, CancellationToken ct);
```

- `GatherCurrencyMacroAsync(universe)` → `AiTextRequest` with `EnableWebSearch: true` and a strict
  **`AiPrompts.CurrencyGatherSystem`** demanding JSON-only output for **every currency in the
  configured universe** (majors + EM + exotics) matching a documented schema — per currency:
  **current** (policy rate, last move, CPI + target, GDP YoY, unemployment, trade balance %GDP,
  stance; for EM/exotic also real yield, reserves, external debt, terms-of-trade, political risk,
  peg) **plus forward** (expected rate path over horizon in bp, inflation trend vs target, growth
  momentum, geopolitical/forward-risk note) **plus a self-reported `dataConfidence` per currency**
  (majors high, exotics often low). Token budget scales with universe size (bounded `AiConstants`
  value); very large universes chunk the gather to stay within limits.
- Parsing lives in **Infrastructure** (`CurrencyMacroParser`): deserialize AI JSON → validate → build
  `CurrencyIndicators` + `CurrencyTrajectory` VOs. Malformed/partial ⇒ `AiResult.Fail` (no throw on
  the request path).
- `ExplainCurrencyOutlookAsync` feeds the *already-computed* ranking + pair-outlook matrix back for a
  plain-English "why #1 / why EUR/USD bullish 3M".

### 3.3 Persistence — snapshot for fast load + history/trend (Infrastructure + EF)

- **`CurrencyStrengthSnapshot` entity** (aggregate; deployment-scoped, not per-user — the macro
  picture is global): `Id`, `AsOf`, `RankingJson` (scores + breakdown), `IndicatorsJson`,
  `TrajectoriesJson`, `PairOutlookJson` (the 28-cross matrix per horizon), `Narrative`, `Source`
  (Ai/Fallback), audited via existing `AuditedEntity`. **One `SaveChanges` = one aggregate.** The
  matrix for all four horizons is computed from the single gathered trajectory and stored together.
- Repository/query in Infrastructure; latest snapshot served to the widget instantly; historical
  snapshots power the **strength-over-time trend chart**.
- **Refresh** is explicit (owner-triggered "Refresh now") and/or a scheduled `BackgroundService`
  in `src/Nodes` (e.g. every N hours, `TimeProvider`-driven) that: gather → parse → `Compute`
  (ranking) → `Project` (pair outlook, all horizons) → explain → persist. Background worker
  **orchestrates**, the domain **decides** (calculators). Gated: no key ⇒ worker no-ops.
- **EF migration** via the `migration` skill: `AddCurrencyStrengthSnapshot` +
  `AddUserDashboardPreferences` (§3.4), canonical layout under `Persistence/Migrations`.

### 3.4 Per-user widget preference (the "enable in his settings" requirement)

No per-user prefs exist today → add a minimal, extensible one:

- **`UserDashboardPreferences`** — owned/associated to `AppUser` by **strong `UserId`** (DDD: no nav
  to another aggregate; reference by ID). Fields start with `ShowCurrencyStrengthWidget` (default
  **false**). Intention-revealing methods (`ShowCurrencyStrength()`, `HideCurrencyStrength()`), no
  public setters. Extensible for future widgets.
- Endpoints `GET/PUT /api/preferences/dashboard` (`RequireAuthorization` — the user's own prefs).
- The user toggles it on **their own** settings surface (see §3.6) — distinct from the Owner-only
  `FeatureSettings`/`AiSettings`.

### 3.5 Web API (`src/Web/Endpoints`)

New group `/api/ai/currency-strength` under `.RequireAuthorization("UserOrAbove").RequireFeature(FeatureFlag.Ai)`:
- `GET /latest?horizon=3M` → latest snapshot: ranking + breakdown + **pair-outlook matrix for the
  horizon** + narrative + `AsOf`; `204`/empty-state if none. Horizon defaults to 3M.
- `GET /history?days=N` → strength time series for the trend chart.
- `POST /refresh` (Owner) → gather → compute → **project (all horizons)** → explain → persist;
  returns fresh snapshot.
- Preferences: `GET/PUT /api/preferences/dashboard` (own user).

### 3.6 UI (`src/Web`, MudBlazor + ApexCharts, mobile-first, tokens only)

**Full page** `AiCurrencyStrength.razor` → `/ai/currency-strength` (inherits `AiFeaturePageBase`,
`AiFeatureNotice`). Interactive, "see *why* each currency ranks **and where each pair is headed**":
- **Horizon selector** (1M / 3M / 6M / 12M) — MudToggle group; re-queries `/latest?horizon=` and
  updates the matrix + forecast charts in place (no remount, per `DashActivityChart`).
- **Tier filter** — chips to scope the view to Majors / EM / Exotics / All, so an N×N matrix over a
  large universe stays legible on a 360px phone (default: Majors + user-selected EM).
- **Pair-outlook matrix (headline)** — N×N heatmap of `DirectionalBias` for the chosen horizon (N =
  currencies in the filtered universe): each cell = base-vs-quote arrow (▲ appreciate / ▬ neutral /
  ▼ depreciate), color-graded by conviction (design tokens), **pegged/low-confidence cells badged**.
  Click a cell → `PairOutlook` detail: projected differential, per-driver `WhyBreakdown`,
  data-confidence, AI "why". Virtualize/scroll gracefully for large N.
- **Forward forecast chart** — projected score per currency at the horizon vs current (delta),
  strongest→weakest → "buy strongest / sell weakest" reading.
- **Current ranking bar chart** — 8 majors sorted by current composite (base layer).
- **Strength-over-time line chart** — composite per currency across snapshots (trend / realized).
- **Per-currency driver breakdown** — select a currency → **radar/stacked-bar** of current + forward
  `DriverScore` contributions (rate, inflation, GDP, employment, trade, stance, geo) with AI rationale.
- **AI narrative panel** — "why #1 / why EUR/USD bullish 3M" (reuse `AiOutputPanel`).
- Empty/degraded states: no key → `AiFeatureNotice`; no snapshot → "Refresh to generate".
- Charts follow `DashActivityChart` pattern (themed, dark, in-place update, no remount race).

**Dashboard widget** `DashCurrencyStrength.razor` in `src/Web/Components/Dashboard/`:
- Compact ranked list/mini-bars (top-strong / top-weak) + **top forward pair calls** (best
  appreciate / depreciate cross at the default 3M horizon) + `AsOf` + link to the full page.
- **Rendered on `Index.razor` only when** `FeatureGate.IsEnabled(Ai)` **and** AI key present **and**
  `UserDashboardPreferences.ShowCurrencyStrongWidget == true`. Never shown by default.

**Settings toggle** — user's own dashboard-preferences surface (new `/settings/dashboard` page **or**
a section in `Account.razor`; a MudBlazor **dialog** per the UI mandate if it's an edit action). A
`MudSwitch` "Show currency-strength widget on my dashboard" that PUTs the preference. Copy notes the
widget needs AI configured (link to `/settings/ai`).

### 3.7 MCP (`src/Mcp/Tools/AiTools.cs`)
- Add `currency_strength` tool → params `{ horizon, tier? }` → returns latest ranking + **pair-outlook
  matrix for the horizon** + narrative JSON for AI clients/agents. Gated identically (flag + key).
  Auth via the existing **MCP key** scheme (`McpKeyAuthHandler`, `mcpk_` bearer) — no new auth.

### 3.8 Programmatic access — one query service, three consumers (MCP · in-app AI · cBot REST/JWT)

The currency snapshot is a **read model** every consumer shares. Define it once, expose three ways:

- **`ICurrencyStrengthQuery` (Core interface, Infra impl)** — `LatestAsync(Horizon, tier filter, ct)`,
  `HistoryAsync(days, ct)`, `PairAsync(base, quote, Horizon, ct)`. Returns Core VOs
  (`CurrencyStrengthRanking` / `PairOutlookMatrix` / `PairOutlook`). The single source the MCP tool,
  the in-app AI features, **and** the cBot REST endpoints all call — no duplicated read logic.

**(a) In-app AI features** — `AiRiskGuard`, `TradingAgent`, `AlertService`, `AiFeatureService`
consumers inject `ICurrencyStrengthQuery` directly (in-process, no HTTP/JWT) to fold the macro
outlook into their prompts/decisions. Pure DI, gated on `FeatureFlag.Ai`.

**(b) MCP** — §3.7, MCP-key auth. For external AI clients/agents.

**(c) cBot REST API, secured by JWT** — cBots run in **sandboxed containers** with no cookie/session,
so they authenticate with a **short-lived HS256 JWT**, mirroring the proven
main→CtraderCliNode scheme (`HttpContainerDispatcher.CreateToken` + `AddJwtBearer` validation):
- **New JWT scheme in `src/Web`** — `AddJwtBearer(AuthSchemes.CBotJwt)` with `TokenValidationParameters`:
  `ValidIssuer = "app-main"`, **`ValidAudience = "app-cbot"`** (new `CBotTokenAuth` constants),
  `ValidateLifetime`/`ValidateIssuerSigningKey` true, `IssuerSigningKey` = `SymmetricSecurityKey` from
  a deployment **cBot-signing secret** (encrypted at rest via `ISecretProtector` /
  `EncryptionPurposes.CBotTokenSecret`, config-seeded — never plaintext/logged). Web's default scheme
  stays cookie; this scheme is additive.
- **Token issuance** — `ICBotAccessTokenIssuer` (Core interface, Infra impl): mints a JWT scoped to
  `{ userId, instanceId, scope=market:read }`, TTL bounded (short, refreshable), `TimeProvider`-clock.
  **Injected into the run/backtest container env at dispatch** (`src/Nodes` Local/Http dispatchers,
  alongside the existing container env) plus the Web base URL, so the cBot reads
  `CMIND_API_TOKEN` + `CMIND_API_BASEURL` from env and calls back. Token lifetime tracks the instance;
  a finished/terminated instance ⇒ token invalid (short TTL + `instanceId` claim checked live).
- **cBot endpoints** — new group `/api/market/currency-strength` (public-facing, machine):
  `GET /latest?horizon=&tier=`, `GET /history?days=`, `GET /pair/{base}/{quote}?horizon=`. Under a
  policy `CBotMarketRead` accepting **`AuthSchemes.CBotJwt`** (and optionally cookie/MCP so the same
  data is reachable from UI/agents), `.RequireFeature(FeatureFlag.Ai)`, **rate-limited**, read-only.
  `scope=market:read` claim required; user-scoped where relevant. Returns the shared query DTOs.
- **`AuthPolicies`/`AuthSchemes`** — add `CBotJwt`, `CBotMarketRead`; `CBotTokenAuth.Issuer/Audience/
  TokenLifetime`; `EncryptionPurposes.CBotTokenSecret`. Constants only, no magic strings.

### 3.9 Constants / options / logging (no magic strings, no raw logs)
- `AppOptions`: **currency universe config** — the tiered list `{ code, tier, isPegged, pegAnchor }`
  with a sensible default (majors + curated EM/exotics), so a deployment adds/removes currencies
  without code. Per-tier default weights + dead-band live here too (overridable).
- `AiConstants`: `CurrencyStrengthMaxTokens` (scales with universe size), default universe, prompt-
  schema markers, refresh interval default. Prompts in `AiPrompts` (Infrastructure).
- Any logging via source-generated `LogMessages` (refresh success/failure, parse failure) — never
  `ILogger.Log*` directly.

---

## 4. Test coverage (all three tiers + failure paths — DoD)

### 4.1 Unit (`tests/UnitTests/Ai/CurrencyStrength/`) — the deterministic core, exhaustive
- `Currency` / `CurrencyUniverse` VO: any configured currency (major/EM/exotic) accepts; code outside
  the universe / bad ISO ⇒ `DomainException`; tier + peg metadata resolved correctly.
- **`ICBotAccessTokenIssuer`** (Infra unit): minted JWT has correct `iss=app-main`/`aud=app-cbot`/
  `instanceId`/`scope` claims, HS256, bounded lifetime (`FakeTimeProvider`); round-trips through the
  same `TokenValidationParameters` the Web scheme uses; expired/wrong-secret ⇒ validation fails.
- `CurrencyIndicators`: tier-aware range guards; out-of-range ⇒ `DomainException`; exotic edge cases
  (hyperinflation, low reserves) accepted-but-flagged, not rejected.
- **Tier/peg behaviour:** EM/exotic weighting lifts carry + risk + external-vulnerability vs majors;
  exotic dead-band wider + conviction capped (thin data can't yield a high-confidence call); a
  **pegged** currency's pair outlook clamps toward `Neutral` and carries the peg flag; low
  `DataConfidence` propagates to the `PairOutlook`; within-tier normalization stops a 50%-inflation
  exotic distorting majors' z-scores; a mixed majors+EM+exotic universe ranks + projects
  deterministically (golden case).
- `CurrencyStrengthCalculator`: **known-input → known-ranking** golden cases (hand-computed);
  hawkish-hike currency outranks dovish-cut (the GBP/JPY-divergence style case from research);
  inflation scored inversely; weights sum-to-1 enforced; deterministic **tie-break** by ISO;
  winsorization clamps an outlier; `AsOf` echoed (no `UtcNow`, `FakeTimeProvider`/hardcoded).
- `StrengthWeights` / `ForwardWeights`: invalid (non-normalized) ⇒ `DomainException`.
- **`ForwardOutlookCalculator`** (the headline math): **known trajectory → known pair bias** golden
  cases; a currency on a hiking path outranks-forward one on a cutting path (Fed-cut vs ECB-hold ⇒
  EUR/USD `Appreciate`); dead-band → tiny differential reads `Neutral` not false conviction;
  `PairOutlook(base,quote)` bias is the exact inverse of `(quote,base)`; **horizon scaling** — longer
  horizon weights trajectory more (a strong forward trajectory flips a pair that current-level alone
  wouldn't); conviction monotonic in |differential|; all 28 crosses present; deterministic, `AsOf`
  echoed. Geopolitics leg: risk-off trajectory lifts USD/JPY/CHF forward scores.
- `CurrencyTrajectory` VO: range guards; out-of-range ⇒ `DomainException`.
- `CurrencyMacroParser` (Infra unit): valid AI JSON → indicators **+ trajectories**;
  malformed/partial/extra-field/missing-trajectory → graceful `Fail`, no throw.
- `AiFeatureService` currency methods with `Substitute.For<IAiClient>()` (existing pattern in
  `AiFeatureServiceTests.cs`): builds correct request (web search on, token budget, schema prompt);
  disabled client ⇒ `Fail`.

### 4.2 Integration (`tests/IntegrationTests/`, Testcontainers PG)
- `CurrencyStrengthSnapshot` round-trip persistence; latest-snapshot + history queries.
- `UserDashboardPreferences` persistence; default false; toggle persists per user.
- Endpoints with a **stubbed `IAiClient`** returning canned indicator+trajectory JSON: `POST /refresh`
  → computes ranking **+ projects all-horizon matrix** + persists; `GET /latest?horizon=` returns the
  right matrix per horizon; `/history` shape; `/api/preferences/dashboard` GET/PUT.
- **Feature-gate off** (`FeatureFlag.Ai=false`) ⇒ endpoints `404`/blocked (matches `RequireFeature`).
- **`ICurrencyStrengthQuery`** shared read model: latest/history/pair shapes; tier filter; empty state.
- **cBot JWT security (`/api/market/currency-strength`):** `ICBotAccessTokenIssuer` mints a valid
  token → endpoint returns data; **expired** token ⇒ `401`; **wrong audience** (node/other) ⇒ `401`;
  **tampered/invalid signature** ⇒ `401`; missing `scope=market:read` ⇒ `403`; token for a
  finished/terminated `instanceId` ⇒ `401`; feature-gated off ⇒ blocked; rate-limit trips on abuse.
- **In-app consumption:** an AI feature (e.g. `AiRiskGuard`/`TradingAgent`) reads the snapshot via
  `ICurrencyStrengthQuery` in-process (no HTTP).
- **Failure paths:** malformed AI JSON ⇒ refresh fails cleanly, **prior snapshot preserved**; AI
  key absent ⇒ `/latest` degrades, refresh returns `Fail`; empty history handled.

### 4.3 E2E (`tests/E2ETests/`, Playwright, mobile + desktop)
- Widget **hidden by default**; user enables it in settings → widget **appears** on dashboard.
- Full `/ai/currency-strength` page renders charts (with AI stubbed/seeded snapshot); **tier filter**
  (Majors/EM/Exotics/All) reshapes the N×N matrix; a **pegged/low-confidence cell shows its badge**.
- Feature flag off ⇒ page + widget absent (no nav entry).
- Universe with EM+exotic currencies renders on 360px without horizontal scroll (matrix
  virtualized/scoped).
- Add both routes (`/ai/currency-strength`, `/settings/dashboard`) to **`PageSmokeTests`** (mandate).
- **Authenticated-API E2E (cBot path):** call `GET /api/market/currency-strength` with a real minted
  cBot JWT → 200 + expected shape; with an expired/wrong-audience token → 401 (the mandate's
  "authenticated API call" E2E form for a machine surface). MCP tool call returns the same data.
- No horizontal scroll 320–1920px; screenshot captured for docs (`CAPTURE_SCREENSHOTS=1`).

---

## 5. Docs (same commit — mandate 8)
- `website/docs/features/currency-strength.md` — what it is, the fundamental method + driver table,
  the **forward pair-outlook / horizon** model, the **tiered universe (majors + EM + exotics)** with
  the EM/exotic extra drivers + data-reliability + peg caveats, the "AI gathers & explains, math is
  deterministic" contract, how to configure the universe + enable (key → per-user widget), and the
  **honest limitation** (medium/long-term positioning filter, not a short-term signal).
- Add id to `website/sidebars.ts`. Screenshot into `website/static/img/screenshots/`.
- `website/docs/operations` note: refresh cadence / background worker + snapshot table.
- **`website/docs/features/currency-strength.md` + a cBot/API section:** MCP tool usage, and the
  **JWT-secured cBot REST API** — how the token is injected (`CMIND_API_TOKEN`/`CMIND_API_BASEURL`),
  a copy-paste cBot sample hitting `/api/market/currency-strength`, and the endpoint reference.
- `website/docs/deployment`: the cBot-token signing secret config + rotation.

---

## 6. Delivery phases (each phase self-contained, green before next)

1. **Core engine** — `Currency`, `CurrencyTier`, `CurrencyUniverse`, `CurrencyIndicators`,
   `CurrencyTrajectory`, `DriverScore`, `CurrencyStrengthScore`, `CurrencyStrengthRanking`,
   `StrengthWeights`/`ForwardWeights` (tier-keyed), `CurrencyStrengthCalculator` +
   `ForwardOutlookCalculator` + full unit suite (majors, EM, exotics, pegs, mixed universe). *No AI,
   no DB, no UI — pure, provable math first.*
2. **AI gather/explain + parser** — contract methods, universe-driven prompts, `CurrencyMacroParser`
   (indicators + trajectories + per-tier confidence), unit tests with substituted client.
3. **Persistence + refresh worker** — snapshot entity, EF migration, repository, gated
   `BackgroundService`; integration tests.
4. **Per-user preference** — `UserDashboardPreferences`, migration, endpoints; integration tests.
5. **Web API + full page UI** — endpoints, `AiCurrencyStrength.razor` charts; E2E page render.
6. **Dashboard widget + settings toggle** — `DashCurrencyStrength.razor`, settings switch, conditional
   render on `Index.razor`; E2E enable-flow; `PageSmokeTests` routes.
7. **Programmatic access** — `ICurrencyStrengthQuery` shared read model; in-app AI consumers wired;
   MCP `currency_strength` tool; **cBot JWT** (`AddJwtBearer` scheme + `CBotMarketRead` policy +
   `ICBotAccessTokenIssuer` + dispatch env injection) and `/api/market/currency-strength` endpoints;
   full security test set (unit + integration + authenticated-API E2E).
8. **Docs + screenshots** — feature doc, sidebar, screenshot; deployment doc for the cBot-signing
   secret + `CMIND_API_BASEURL`/`CMIND_API_TOKEN` env; a minimal cBot code sample calling the REST API.

---

## 7. Risks & mitigations
- **AI data reliability (worse for EM/exotics)** — web-searched macro figures can be stale/wrong, and
  EM/exotic stats are low-frequency, revised, or opaque. Mitigate: strict JSON schema + tier-aware
  range validation; per-currency `DataConfidence` surfaced as a reliability badge; wider dead-band +
  capped conviction for exotics; show `AsOf` + source prominently; deterministic math means a bad
  *number* is visible/bounded, never a silent wrong rank. UX frames it as a research aid.
- **Managed/pegged regimes** — HKD/AED/SAR pegs, CNH management: flagged, trajectory down-weighted,
  outlook clamped toward `Neutral` so a peg isn't read as a free-floating signal.
- **Universe scale (N×N)** — large universes cost tokens + screen space. Mitigate: chunked gather,
  token budget scales with N, tier filters + virtualization in the matrix, majors-default view.
- **LLM non-determinism in the number path** — eliminated by design: AI only supplies inputs +
  narrative; the rank is pure Core math.
- **Snapshot staleness** — widget always shows `AsOf`; refresh preserves last-good on failure.
- **Scope of "geopolitical"** — kept as a bounded risk-score driver (risk-on/off + note), not
  open-ended news scraping, to stay testable.
- **DDD hazard** — snapshot is deployment-scoped, preferences are user-scoped: two aggregates, two
  `SaveChanges`, referenced by strong ID; no cross-aggregate nav.
- **cBot token security** — a leaked JWT is read-only, market-scoped, short-lived, and dies with the
  instance (`instanceId` claim + TTL), limiting blast radius; signed with an encrypted deployment
  secret (rotatable); endpoints rate-limited. Never log the token. If cBots can't reach the Web host
  (network policy), the REST path degrades and the cBot proceeds without the macro read — never blocks
  the trade. Reuses the audited main→node JWT pattern rather than inventing new crypto.

---

## 8. Definition of done (this feature)
- [ ] `dotnet build` 0 warnings; analyzer sweep clean on touched projects; Rider `get_file_problems`
      clean on every `.cs`/`.razor`.
- [ ] Unit + integration + E2E green, **failure paths covered**; new routes in `PageSmokeTests`.
- [ ] `src/Core` stays infra-free; ranking **and forward-projection** math is 100% deterministic &
      unit-proven across majors + EM + exotics + pegs + mixed universe.
- [ ] Data reachable 3 ways off one `ICurrencyStrengthQuery`: in-app AI (DI), MCP (`mcpk_` key),
      **cBot REST secured by HS256 JWT** (`aud=app-cbot`, scoped, short-lived, rate-limited); 401/403
      failure paths proven.
- [ ] No `DateTime.UtcNow`; no secrets/magic strings (cBot-signing secret encrypted via
      `ISecretProtector`); source-gen logging; modern C# 14.
- [ ] Forward pair-outlook matrix (all horizons) + tiered universe (majors/EM/exotics) shipped;
      pegs/low-confidence flagged; N×N view legible on 360px.
- [ ] Widget hidden until (feature flag ✔ + AI key ✔ + per-user toggle ✔); AI-off path verified.
- [ ] EF migrations added; docs + sidebar + screenshot updated in the same commit.
- [ ] `caveman:cavecrew-reviewer` pass on the multi-file diff; every valid note fixed.
