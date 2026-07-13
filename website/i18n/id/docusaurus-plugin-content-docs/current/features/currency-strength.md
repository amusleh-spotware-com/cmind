# AI macro currency strength & forward outlook

cMind mengirim **AI-assisted, math-deterministic** macro currency-strength engine. Ini merangking configurable
universe dari currencies — 8 majors ditambah emerging-market dan exotic currencies — oleh **current**
fundamental strength, dan projects **forward directional outlook** untuk setiap pair di atas chosen
horizon (1M / 3M / 6M / 12M). Setiap rank, setiap pair bias dan setiap number dihitung oleh pure
deterministic math dalam domain core; LLM hanya *gathers* forward-looking inputs yang data
tidak dapat publish dan *explains* result dalam plain English. Ini tidak pernah invents rank, direction atau
number.

> **Honest limitation.** Fundamentals memprediksi medium-to-long-term value well dan short-term value
> poorly. Treat ini sebagai positioning / confluence filter, **bukan** short-term timing signal. Readings
> dekat high-impact releases (NFP/CPI/central-bank) adalah noisy. Bukan financial advice.

## Bagaimana cara kerjanya

1. **Current fundamentals datang dari Economic Calendar, bukan LLM.** Hard numbers — policy
   rates, CPI vs target, GDP, employment, trade balance — dan their **surprise z-scores** disumber
   **point-in-time** dari [economic calendar](./economic-calendar.md) module (FRED/BLS/BEA/ECB dan
   central-bank schedules). Historical snapshot tidak pernah leak look-ahead.
2. **LLM hanya gathers apa yang calendar tidak dapat publish** — per currency: **forward** trajectory
   (expected policy-rate path dalam bp, inflation-trend-vs-target, growth momentum) dan **geopolitical**
   outlook (risk-on/off, tariffs, fiscal/debt, elections), ditambah any EM/exotic current figures yang
   calendar lacks. Strict JSON, tier-aware validation, web search on.
3. **Domain computes ranking dan forward matrix secara deterministic.** Setiap driver diskor
   sebagai **within-tier z-score** (jadi 50%-inflation exotic tidak pernah distort majors), winsorized,
   weight-summed ke composite, dan ranked strongest→weakest dengan stable ISO tie-break. Forward layer
   carries setiap composite sepanjang its trajectory —
   `projected = current + horizonScale · Σ trajectoryDriver·weight` — dan maps setiap pair's projected
   differential ke **directional bias** (▲ appreciate / ▬ neutral / ▼ depreciate) dengan conviction.
4. **LLM explains** ranking dan top pair calls dalam plain language.

## Drivers

| Driver | Effect pada strength | Notes |
|---|---|---|
| Policy rate & trajectory | Higher / hawkish ⇒ stronger | Highest weight; central-bank divergence drives biggest gaps. |
| Inflation (CPI vs target) | Atas target ⇒ weaker | Scored inversely (purchasing-power drag). |
| GDP growth | Higher relative growth ⇒ stronger | Differential vs panel. |
| Employment | Stronger labour ⇒ stronger | Feeds policy path. |
| Trade balance / current account | Surplus ⇒ stronger | Structural demand. |
| Policy stance | Hawkish ⇒ stronger | Primary long-term driver. |
| Surprise momentum | Recent beats ⇒ stronger | Dari calendar's surprise z-scores. |
| Geopolitical / risk | Risk-off ⇒ safe havens (USD/JPY/CHF) stronger | Bounded forward risk delta. |
| Real yield / carry *(EM/exotic)* | Positive real rate ⇒ stronger | Dominant EM driver dalam calm regimes. |
| External vulnerability *(EM/exotic)* | Deficits / low reserves / USD debt ⇒ weaker | Structural depreciation pressure. |
| Terms of trade *(commodity exporters)* | Rising export prices ⇒ stronger | BRL, ZAR, CLP, NOK, AUD, CAD. |
| Political / institutional risk *(EM/exotic)* | Instability ⇒ weaker | Wider dead-band, capped conviction. |

## Tiered universe (majors + EM + exotics)

Universe adalah **deployment-configurable** (`App:CurrencyStrength:Universe`) — menambahkan currency adalah
config, tidak code. Setiap currency membawa **tier** (`Major` / `EmergingMarket` / `Exotic`) yang tunes
weighting, dead-band width dan conviction cap:

- **Majors** — USD, EUR, GBP, JPY, AUD, NZD, CAD, CHF (rate-level led).
- **Emerging markets** — CNH, INR, BRL, MXN, ZAR, KRW, SGD, PLN (+ Scandi NOK/SEK); carry + risk +
  external-vulnerability weighted up, medium confidence.
- **Exotics** — TRY, HUF, CZK, ditambah USD-pegged HKD/SAR; low confidence, wider dead-band, capped
  conviction. **Pegged / heavily-managed** currencies (HKD, SAR, CNH) adalah flagged, their trajectory adalah
  down-weighted, dan their pair outlook adalah clamped menuju `Neutral` jadi peg tidak pernah dibaca sebagai
  free-floating signal.

Karena official EM/exotic stats adalah lower-frequency, revised dan kadang opaque, AI-gathered
figures membawa **per-tier confidence** ditampilkan sebagai reliability badge.

## Graceful degradation

| Calendar | AI | Result |
|---|---|---|
| ✅ | ✅ | Full ranking + forward projection + narrative (`CalendarAndAi`). |
| ✅ | ❌ | Calendar-only current ranking, tidak ada forward projection (`CalendarOnly`). |
| ❌ | ✅ | AI-gathered current figures + forward, lower confidence (`AiOnly`). |
| ❌ | ❌ | Tidak ada snapshot — widget hides dan page menunjukkan empty state. |

App berjalan unchanged baik caranya. AI adalah gated pada AI key; calendar leg menghormati its own
white-label gate + runtime toggle.

## Menggunakannya

- **Enable AI** (Settings → AI) dan **turn on widget** dari your own dashboard **Customize** dialog
  ("Currency strength" — opt-in, hidden by default). Widget menampilkan top strong/weak currencies dan
  top 3M pair call; links ke full page.
- **Full page** — `/ai/currency-strength`: horizon selector (1M/3M/6M/12M), tier filter
  (All/Majors/EM/Exotics), current ranking, forward forecast, pair-outlook matrix (bias +
  conviction, pegged/low-confidence flagged), dan AI narrative. Press **Refresh now** (owner) untuk
  regenerate. Background worker (`App:CurrencyStrength:RefreshEnabled`, **default `true`**) refreshes pada
  schedule jadi page adalah populated out of the box; deployment atau owner turns itu off (atau disables
  AI / economic-calendar feature, yang refresher honors oleh degrading ke tidak ada snapshot).

## Programmatic access

Satu shared read model (`ICurrencyStrengthQuery`) adalah reachable tiga ways:

- **In-app AI** — injected directly (in-process) ke AI features.
- **MCP** — `currency_strength` tool (params `horizon`, `tier`) untuk AI clients/agents.
- **cBot REST** — `GET /api/market/v1/currency-strength/{latest,history,pair/{base}/{quote}}`, secured
  oleh **same** `CalendarJwt` machinery sebagai [calendar cBot API](./calendar-cbot-api.md) dengan added
  **`market:read`** scope. CBot registers API client dengan `market:read`, exchanges its
  id + secret untuk short-lived JWT di `POST /api/calendar/v1/token`, dan calls endpoints dengan
  `Bearer` token. Tidak ada second JWT scheme, tidak ada second secret — leaked token adalah read-only,
  market-scoped, short-lived dan revocable.

Lihat [calendar cBot API](./calendar-cbot-api.md) untuk token flow dan copy-paste sample.
