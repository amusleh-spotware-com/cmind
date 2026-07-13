# AI macro currency strength & forward outlook

cMind ship một **AI-assisted, math-deterministic** macro currency-strength engine. Nó ranks một
configurable universe của currencies — 8 majors cộng emerging-market và exotic currencies — by
**current** fundamental strength, và projects một **forward directional outlook** cho mọi pair over a
chosen horizon (1M / 3M / 6M / 12M). Mọi rank, mọi pair bias và mọi số được compute bởi pure
deterministic math trong domain core; LLM chỉ *gathers* các forward-looking inputs mà data
can't publish và *explains* kết quả in plain English. Nó không bao giờ invent một rank, một direction hoặc một
số.

> **Honest limitation.** Fundamentals predict medium-to-long-term value tốt và short-term value
> poorly. Treat cái này như một positioning / confluence filter, **không phải** a short-term timing signal. Readings
> gần high-impact releases (NFP/CPI/central-bank) noisy. Not financial advice.

## How it works

1. **Current fundamentals đến từ Economic Calendar, không phải LLM.** Hard numbers — policy
   rates, CPI vs target, GDP, employment, trade balance — và their **surprise z-scores** được sourced
   **point-in-time** từ [economic calendar](./economic-calendar.md) module (FRED/BLS/BEA/ECB and
   central-bank schedules). Một historical snapshot không bao giờ leaks look-ahead.
2. **LLM gathers chỉ những gì calendar không thể publish** — per currency: **forward** trajectory
   (expected policy-rate path in bp, inflation-trend-vs-target, growth momentum) và a **geopolitical**
   outlook (risk-on/off, tariffs, fiscal/debt, elections), cộng any EM/exotic current figures calendar
   lacks. Strict JSON, tier-aware validation, web search on.
3. **Domain computes ranking và forward matrix deterministically.** Mỗi driver scored as a
   **within-tier z-score** (vì vậy một 50%-inflation exotic không bao giờ distorts majors), winsorized,
   weight-summed into a composite, và ranked strongest→weakest với stable ISO tie-break. Forward layer carries each composite along its trajectory —
   `projected = current + horizonScale · Σ trajectoryDriver·weight` — và maps each pair's projected
   differential to a **directional bias** (▲ appreciate / ▬ neutral / ▼ depreciate) với conviction.
4. **LLM explains** ranking và top pair calls in plain language.

## The drivers

| Driver | Effect on strength | Notes |
|---|---|---|
| Policy rate & trajectory | Higher / hawkish ⇒ stronger | Highest weight; central-bank divergence drives biggest gaps. |
| Inflation (CPI vs target) | Above target ⇒ weaker | Scored inversely (purchasing-power drag). |
| GDP growth | Higher relative growth ⇒ stronger | Differential vs the panel. |
| Employment | Stronger labour ⇒ stronger | Feeds the policy path. |
| Trade balance / current account | Surplus ⇒ stronger | Structural demand. |
| Policy stance | Hawkish ⇒ stronger | Primary long-term driver. |
| Surprise momentum | Recent beats ⇒ stronger | From calendar's surprise z-scores. |
| Geopolitical / risk | Risk-off ⇒ safe havens (USD/JPY/CHF) stronger | Bounded forward risk delta. |
| Real yield / carry *(EM/exotic)* | Positive real rate ⇒ stronger | Dominant EM driver in calm regimes. |
| External vulnerability *(EM/exotic)* | Deficits / low reserves / USD debt ⇒ weaker | Structural depreciation pressure. |
| Terms of trade *(commodity exporters)* | Rising export prices ⇒ stronger | BRL, ZAR, CLP, NOK, AUD, CAD. |
| Political / institutional risk *(EM/exotic)* | Instability ⇒ weaker | Wider dead-band, capped conviction. |

## Tiered universe (majors + EM + exotics)

Universe là **deployment-configurable** (`App:CurrencyStrength:Universe`) — adding a currency là
config, không phải code. Mỗi currency mang a **tier** (`Major` / `EmergingMarket` / `Exotic`) that tunes
weighting, dead-band width và conviction cap:

- **Majors** — USD, EUR, GBP, JPY, AUD, NZD, CAD, CHF (rate-level led).
- **Emerging markets** — CNH, INR, BRL, MXN, ZAR, KRW, SGD, PLN (+ Scandi NOK/SEK); carry + risk +
  external-vulnerability weighted up, medium confidence.
- **Exotics** — TRY, HUF, CZK, cộng USD-pegged HKD/SAR; low confidence, wider dead-band, capped
  conviction. **Pegged / heavily-managed** currencies (HKD, SAR, CNH) được flagged, their trajectory
  down-weighted, và their pair outlook clamped toward `Neutral` vì vậy một peg không bao giờ được read as a
  free-floating signal.

Vì official EM/exotic stats lower-frequency, revised và sometimes opaque, AI-gathered
figures carry a **per-tier confidence** shown as reliability badge.

## Graceful degradation

| Calendar | AI | Result |
|---|---|---|
| ✅ | ✅ | Full ranking + forward projection + narrative (`CalendarAndAi`). |
| ✅ | ❌ | Calendar-only current ranking, no forward projection (`CalendarOnly`). |
| ❌ | ✅ | AI-gathered current figures + forward, lower confidence (`AiOnly`). |
| ❌ | ❌ | No snapshot — widget hides và page shows empty state. |

App runs unchanged either way. AI gated on AI key; calendar leg respects its own
white-label gate + runtime toggle.

## Using it

- **Enable AI** (Settings → AI) và **turn on widget** từ your own dashboard **Customize** dialog
  ("Currency strength" — opt-in, hidden by default). Widget shows top strong/weak currencies và
  top 3M pair call; nó links to full page.
- **Full page** — `/ai/currency-strength`: horizon selector (1M/3M/6M/12M), tier filter
  (All/Majors/EM/Exotics), current ranking, forward forecast, pair-outlook matrix (bias +
  conviction, pegged/low-confidence flagged), và AI narrative. Press **Refresh now** (owner) to
  regenerate. Background worker (`App:CurrencyStrength:RefreshEnabled`, **default `true`**) refreshes on
  a schedule nên page populated out of the box; deployment hoặc owner turns it off (hoặc disables
  AI / economic-calendar feature, mà refresher honours by degrading to no snapshot).

## Programmatic access

Một shared read model (`ICurrencyStrengthQuery`) reachable three ways:

- **In-app AI** — injected directly (in-process) vào AI features.
- **MCP** — `currency_strength` tool (params `horizon`, `tier`) cho AI clients/agents.
- **cBot REST** — `GET /api/market/v1/currency-strength/{latest,history,pair/{base}/{quote}}`, secured
  by **cùng** `CalendarJwt` machinery như [calendar cBot API](./calendar-cbot-api.md) với added
  **`market:read`** scope. Một cBot registers API client với `market:read`, exchanges its
  id + secret for a short-lived JWT at `POST /api/calendar/v1/token`, và calls endpoints với a
  `Bearer` token. Không second JWT scheme, không second secret — a leaked token is read-only, market-scoped,
  short-lived và revocable.

Xem [calendar cBot API](./calendar-cbot-api.md) cho token flow và copy-paste sample.
