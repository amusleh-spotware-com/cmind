# AI macro currency strength & forward outlook

cMind ships an **AI-assisted, math-deterministic** macro currency-strength engine. It ranks a
configurable universe of currencies — the 8 majors plus emerging-market and exotic currencies — by
**current** fundamental strength, and projects a **forward directional outlook** for every pair over a
chosen horizon (1M / 3M / 6M / 12M). Every rank, every pair bias and every number is computed by pure
deterministic math in the domain core; the LLM only *gathers* the forward-looking inputs the data
can't publish and *explains* the result in plain English. It never invents a rank, a direction or a
number.

> **Honest limitation.** Fundamentals predict medium-to-long-term value well and short-term value
> poorly. Treat this as a positioning / confluence filter, **not** a short-term timing signal. Readings
> near high-impact releases (NFP/CPI/central-bank) are noisy. Not financial advice.

## How it works

1. **Current fundamentals come from the Economic Calendar, not the LLM.** The hard numbers — policy
   rates, CPI vs target, GDP, employment, trade balance — and their **surprise z-scores** are sourced
   **point-in-time** from the [economic calendar](./economic-calendar.md) module (FRED/BLS/BEA/ECB and
   central-bank schedules). A historical snapshot never leaks look-ahead.
2. **The LLM gathers only what the calendar can't publish** — per currency: the **forward** trajectory
   (expected policy-rate path in bp, inflation-trend-vs-target, growth momentum) and a **geopolitical**
   outlook (risk-on/off, tariffs, fiscal/debt, elections), plus any EM/exotic current figures the
   calendar lacks. Strict JSON, tier-aware validation, web search on.
3. **The domain computes the ranking and the forward matrix deterministically.** Each driver is scored
   as a **within-tier z-score** (so a 50%-inflation exotic never distorts the majors), winsorized,
   weight-summed into a composite, and ranked strongest→weakest with a stable ISO tie-break. The
   forward layer carries each composite along its trajectory —
   `projected = current + horizonScale · Σ trajectoryDriver·weight` — and maps each pair's projected
   differential to a **directional bias** (▲ appreciate / ▬ neutral / ▼ depreciate) with a conviction.
4. **The LLM explains** the ranking and the top pair calls in plain language.

## The drivers

| Driver | Effect on strength | Notes |
|---|---|---|
| Policy rate & trajectory | Higher / hawkish ⇒ stronger | Highest weight; central-bank divergence drives the biggest gaps. |
| Inflation (CPI vs target) | Above target ⇒ weaker | Scored inversely (purchasing-power drag). |
| GDP growth | Higher relative growth ⇒ stronger | Differential vs the panel. |
| Employment | Stronger labour ⇒ stronger | Feeds the policy path. |
| Trade balance / current account | Surplus ⇒ stronger | Structural demand. |
| Policy stance | Hawkish ⇒ stronger | The primary long-term driver. |
| Surprise momentum | Recent beats ⇒ stronger | From the calendar's surprise z-scores. |
| Geopolitical / risk | Risk-off ⇒ safe havens (USD/JPY/CHF) stronger | Bounded forward risk delta. |
| Real yield / carry *(EM/exotic)* | Positive real rate ⇒ stronger | Dominant EM driver in calm regimes. |
| External vulnerability *(EM/exotic)* | Deficits / low reserves / USD debt ⇒ weaker | Structural depreciation pressure. |
| Terms of trade *(commodity exporters)* | Rising export prices ⇒ stronger | BRL, ZAR, CLP, NOK, AUD, CAD. |
| Political / institutional risk *(EM/exotic)* | Instability ⇒ weaker | Wider dead-band, capped conviction. |

## Tiered universe (majors + EM + exotics)

The universe is **deployment-configurable** (`App:CurrencyStrength:Universe`) — adding a currency is
config, not code. Each currency carries a **tier** (`Major` / `EmergingMarket` / `Exotic`) that tunes
weighting, dead-band width and conviction cap:

- **Majors** — USD, EUR, GBP, JPY, AUD, NZD, CAD, CHF (rate-level led).
- **Emerging markets** — CNH, INR, BRL, MXN, ZAR, KRW, SGD, PLN (+ Scandi NOK/SEK); carry + risk +
  external-vulnerability weighted up, medium confidence.
- **Exotics** — TRY, HUF, CZK, plus USD-pegged HKD/SAR; low confidence, wider dead-band, capped
  conviction. **Pegged / heavily-managed** currencies (HKD, SAR, CNH) are flagged, their trajectory is
  down-weighted, and their pair outlook is clamped toward `Neutral` so a peg is never read as a
  free-floating signal.

Because official EM/exotic stats are lower-frequency, revised and sometimes opaque, the AI-gathered
figures carry a **per-tier confidence** shown as a reliability badge.

## Graceful degradation

| Calendar | AI | Result |
|---|---|---|
| ✅ | ✅ | Full ranking + forward projection + narrative (`CalendarAndAi`). |
| ✅ | ❌ | Calendar-only current ranking, no forward projection (`CalendarOnly`). |
| ❌ | ✅ | AI-gathered current figures + forward, lower confidence (`AiOnly`). |
| ❌ | ❌ | No snapshot — the widget hides and the page shows an empty state. |

The app runs unchanged either way. AI is gated on the AI key; the calendar leg respects its own
white-label gate + runtime toggle.

## Using it

- **Enable AI** (Settings → AI) and **turn on the widget** from your own dashboard **Customize** dialog
  ("Currency strength" — opt-in, hidden by default). The widget shows the top strong/weak currencies and
  the top 3M pair call; it links to the full page.
- **Full page** — `/ai/currency-strength`: a horizon selector (1M/3M/6M/12M), a tier filter
  (All/Majors/EM/Exotics), the current ranking, the forward forecast, the pair-outlook matrix (bias +
  conviction, pegged/low-confidence flagged), and the AI narrative. Press **Refresh now** (owner) to
  regenerate. A background worker (`App:CurrencyStrength:RefreshEnabled`, **default `true`**) refreshes on
  a schedule so the page is populated out of the box; a deployment or the owner turns it off (or disables
  the AI / economic-calendar feature, which the refresher honours by degrading to no snapshot).

## Programmatic access

One shared read model (`ICurrencyStrengthQuery`) is reachable three ways:

- **In-app AI** — injected directly (in-process) into AI features.
- **MCP** — the `currency_strength` tool (params `horizon`, `tier`) for AI clients/agents.
- **cBot REST** — `GET /api/market/v1/currency-strength/{latest,history,pair/{base}/{quote}}`, secured
  by the **same** `CalendarJwt` machinery as the [calendar cBot API](./calendar-cbot-api.md) with an
  added **`market:read`** scope. A cBot registers an API client with `market:read`, exchanges its
  id + secret for a short-lived JWT at `POST /api/calendar/v1/token`, and calls the endpoints with a
  `Bearer` token. No second JWT scheme, no second secret — a leaked token is read-only, market-scoped,
  short-lived and revocable.

See the [calendar cBot API](./calendar-cbot-api.md) for the token flow and a copy-paste sample.
