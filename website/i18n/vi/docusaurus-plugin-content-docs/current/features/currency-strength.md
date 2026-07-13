# AI macro currency strength & forward outlook

cMind ships một **AI-assisted, math-deterministic** macro currency-strength engine. Nó ranks configurable universe của currencies — 8 majors cộng emerging-market và exotic currencies — bởi **current** fundamental strength, và projects **forward directional outlook** cho mỗi pair qua chosen horizon (1M / 3M / 6M / 12M). Mỗi rank, mỗi pair bias và mỗi number được computed bởi pure deterministic math trong domain core; LLM chỉ *gathers* forward-looking inputs data không thể publish và *explains* result trong plain English. Nó không bao giờ invents rank, direction hoặc number.

> **Honest limitation.** Fundamentals predict medium-to-long-term value tốt và short-term value kém. Treat cái này như positioning / confluence filter, **không** short-term timing signal. Readings gần high-impact releases (NFP/CPI/central-bank) noisy. Không financial advice.

## Cách nó hoạt động

1. **Current fundamentals come từ Economic Calendar, không phải LLM.** Hard numbers — policy rates, CPI vs target, GDP, employment, trade balance — và **surprise z-scores** của chúng sourced **point-in-time** từ [economic calendar](./economic-calendar.md) module (FRED/BLS/BEA/ECB và central-bank schedules). Historical snapshot không bao giờ leaks look-ahead.
2. **LLM gathers chỉ những gì calendar không thể publish** — per currency: **forward** trajectory (expected policy-rate path trong bp, inflation-trend-vs-target, growth momentum) và **geopolitical** outlook (risk-on/off, tariffs, fiscal/debt, elections), cộng bất kỳ EM/exotic current figures calendar lacks. Strict JSON, tier-aware validation, web search on.
3. **Domain computes ranking và forward matrix deterministically.** Mỗi driver được scored như **within-tier z-score** (nên 50%-inflation exotic không bao giờ distorts majors), winsorized, weight-summed vào composite, và ranked strongest→weakest với stable ISO tie-break. Forward layer carries mỗi composite dọc trajectory của nó — `projected = current + horizonScale · Σ trajectoryDriver·weight` — và maps mỗi pair's projected differential tới **directional bias** (▲ appreciate / ▬ neutral / ▼ depreciate) với conviction.
4. **LLM explains** ranking và top pair calls trong plain language.

## The drivers

| Driver | Effect trên strength | Notes |
|---|---|---|
| Policy rate & trajectory | Higher / hawkish ⇒ stronger | Highest weight; central-bank divergence drives biggest gaps. |
| Inflation (CPI vs target) | Above target ⇒ weaker | Scored inversely (purchasing-power drag). |
| GDP growth | Higher relative growth ⇒ stronger | Differential vs panel. |
| Employment | Stronger labour ⇒ stronger | Feeds policy path. |
| Trade balance / current account | Surplus ⇒ stronger | Structural demand. |
| Policy stance | Hawkish ⇒ stronger | Primary long-term driver. |
| Surprise momentum | Recent beats ⇒ stronger | Từ calendar's surprise z-scores. |
| Geopolitical / risk | Risk-off ⇒ safe havens (USD/JPY/CHF) stronger | Bounded forward risk delta. |
| Real yield / carry *(EM/exotic)* | Positive real rate ⇒ stronger | Dominant EM driver trong calm regimes. |
| External vulnerability *(EM/exotic)* | Deficits / low reserves / USD debt ⇒ weaker | Structural depreciation pressure. |
| Terms of trade *(commodity exporters)* | Rising export prices ⇒ stronger | BRL, ZAR, CLP, NOK, AUD, CAD. |
| Political / institutional risk *(EM/exotic)* | Instability ⇒ weaker | Wider dead-band, capped conviction. |

## Tiered universe (majors + EM + exotics)

Universe **deployment-configurable** (`App:CurrencyStrength:Universe`) — thêm currency là config, không code. Mỗi currency carries **tier** (`Major` / `EmergingMarket` / `Exotic`) tunes weighting, dead-band width và conviction cap:

- **Majors** — USD, EUR, GBP, JPY, AUD, NZD, CAD, CHF (rate-level led).
- **Emerging markets** — CNH, INR, BRL, MXN, ZAR, KRW, SGD, PLN (+ Scandi NOK/SEK); carry + risk + external-vulnerability weighted up, medium confidence.
- **Exotics** — TRY, HUF, CZK, cộng USD-pegged HKD/SAR; low confidence, wider dead-band, capped conviction. **Pegged / heavily-managed** currencies (HKD, SAR, CNH) flagged, trajectory của chúng down-weighted, và pair outlook của chúng clamped tới `Neutral` nên peg không bao giờ được đọc như free-floating signal.

Vì official EM/exotic stats là lower-frequency, revised và sometimes opaque, AI-gathered figures carry **per-tier confidence** được hiển thị như reliability badge.

## Graceful degradation

| Calendar | AI | Result |
|---|---|---|
| ✅ | ✅ | Full ranking + forward projection + narrative (`CalendarAndAi`). |
| ✅ | ❌ | Calendar-only current ranking, không forward projection (`CalendarOnly`). |
| ❌ | ✅ | AI-gathered current figures + forward, lower confidence (`AiOnly`). |
| ❌ | ❌ | Không snapshot — widget hides và page hiển thị empty state. |

App chạy unchanged either way. AI gated trên AI key; calendar leg respects own white-label gate + runtime toggle.

## Sử dụng nó

- **Enable AI** (Settings → AI) và **turn on widget** từ own dashboard **Customize** dialog ("Currency strength" — opt-in, hidden by default). Widget hiển thị top strong/weak currencies và top 3M pair call; nó links tới full page.
- **Full page** — `/ai/currency-strength`: horizon selector (1M/3M/6M/12M), tier filter (All/Majors/EM/Exotics), current ranking, forward forecast, pair-outlook matrix (bias + conviction, pegged/low-confidence flagged), và AI narrative. Press **Refresh now** (owner) để regenerate. Background worker (`App:CurrencyStrength:RefreshEnabled`, **default `true`**) refreshes trên schedule nên page populated out of the box; deployment hoặc owner turns off (hoặc disables AI / economic-calendar feature, mà refresher honours bằng degrading tới không snapshot).

## Programmatic access

One shared read model (`ICurrencyStrengthQuery`) reachable ba ways:

- **In-app AI** — injected trực tiếp (in-process) vào AI features.
- **MCP** — `currency_strength` tool (params `horizon`, `tier`) cho AI clients/agents.
- **cBot REST** — `GET /api/market/v1/currency-strength/{latest,history,pair/{base}/{quote}}`, secured bởi **same** `CalendarJwt` machinery như [calendar cBot API](./calendar-cbot-api.md) với thêm **`market:read`** scope. cBot registers API client với `market:read`, exchanges id + secret cho short-lived JWT tại `POST /api/calendar/v1/token`, và calls endpoints với `Bearer` token. Không second JWT scheme, không second secret — leaked token là read-only, market-scoped, short-lived và revocable.

Xem [calendar cBot API](./calendar-cbot-api.md) cho token flow và copy-paste sample.
