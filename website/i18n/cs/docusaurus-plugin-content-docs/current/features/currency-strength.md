# AI makro síla měn & forward outlook

cMind dodává **AI-assisted, math-deterministic** makro currency-strength engine. Rankuje a
konfigurovatelné universe měn — 8 majorů plus emerging-market a exotické měny — podle
**aktuální** fundamentální síly, a projektuje **forward directional outlook** pro každý pár přes a
chosen horizon (1M / 3M / 6M / 12M). Každý rank, každý pair bias a každé číslo je spočítáno čistou
deterministickou matematikou v doménovém core; LLM pouze *sbírá* forward-looking inputs které data
nemohou publikovat a *vysvětluje* výsledek v plain English. Nikdy nevynález rank, direction nebo
číslo.

> **Upřímné omezení.** Fundamenty předpovídají střední až dlouhodobou hodnotu dobře a krátkodobou špatně.
> Treat this as a positioning / confluence filter, **not** a short-term timing signal. Readings
> near high-impact releases (NFP/CPI/central-bank) are noisy. Ne finanční poradenství.

## Jak to funguje

1. **Current fundamentals come from the Economic Calendar, not the LLM.** Hard numbers — policy
   rates, CPI vs target, GDP, employment, trade balance — and their **surprise z-scores** are sourced
   **point-in-time** z [economic calendar](./economic-calendar.md) modulu (FRED/BLS/BEA/ECB and
   central-bank schedules). Historical snapshot never leaks look-ahead.
2. **LLM gathers only what the calendar can't publish** — per currency: the **forward** trajectory
   (expected policy-rate path in bp, inflation-trend-vs-target, growth momentum) and a **geopolitical**
   outlook (risk-on/off, tariffs, fiscal/debt, elections), plus any EM/exotic current figures the
   calendar lacks. Strict JSON, tier-aware validation, web search on.
3. **Doména počítá ranking a forward matrix deterministicky.** Each driver is scored
   as a **within-tier z-score** (so a 50%-inflation exotic never distorts the majors), winsorized,
   weight-summed into a composite, and ranked strongest→weakest with stable ISO tie-break. The
   forward layer carries each composite along its trajectory —
   `projected = current + horizonScale · Σ trajectoryDriver·weight` — and maps each pair's projected
   differential to a **directional bias** (▲ appreciate / ▬ neutral / ▼ depreciate) with conviction.
4. **LLM explains** the ranking and the top pair calls in plain language.

## Drivery

| Driver | Efekt na sílu | Poznámky |
|---|---|---|
| Policy rate & trajectory | Vyšší / hawkish ⇒ silnější | Nejvyšší váha; divergence centrální banky pohání největší mezery. |
| Inflation (CPI vs target) | Nad cíl ⇒ slabší | Scored inversely (purchasing-power drag). |
| GDP growth | Vyšší relativní růst ⇒ silnější | Diferenciál vs panel. |
| Employment | Silnější práce ⇒ silnější | Živí policy path. |
| Trade balance / current account | Přebytek ⇒ silnější | Strukturní poptávka. |
| Policy stance | Hawkish ⇒ silnější | Primární dlouhodobý driver. |
| Surprise momentum | Nedávné beaty ⇒ silnější | Z calendar's surprise z-scores. |
| Geopolitical / risk | Risk-off ⇒ bezpečné přístavy (USD/JPY/CHF) silnější | Bounded forward risk delta. |
| Real yield / carry *(EM/exotic)* | Pozitivní reálná sazba ⇒ silnější | Dominantní EM driver v klidných režimech. |
| External vulnerability *(EM/exotic)* | Deficify / low reserves / USD debt ⇒ slabší | Strukturní depreciační tlak. |
| Terms of trade *(commodity exporters)* | Růst exportních cen ⇒ silnější | BRL, ZAR, CLP, NOK, AUD, CAD. |
| Political / institutional risk *(EM/exotic)* | Nestabilita ⇒ slabší | Širší dead-band, capped conviction. |

## Tiered universe (majors + EM + exotics)

Universe je **deployment-configurable** (`App:CurrencyStrength:Universe`) — přidání měny je
config, ne code. Každá měna nese **tier** (`Major` / `EmergingMarket` / `Exotic`) který ladí
weighting, dead-band width a conviction cap:

- **Majors** — USD, EUR, GBP, JPY, AUD, NZD, CAD, CHF (rate-level led).
- **Emerging markets** — CNH, INR, BRL, MXN, ZAR, KRW, SGD, PLN (+ Scandi NOK/SEK); carry + risk +
  external-vulnerability weighted up, medium confidence.
- **Exotics** — TRY, HUF, CZK, plus USD-pegged HKD/SAR; low confidence, wider dead-band, capped
  conviction. **Pegged / heavily-managed** měny (HKD, SAR, CNH) jsou označeny, their trajectory is
  down-weighted, and their pair outlook is clamped toward `Neutral` so a peg is never read as a
  free-floating signal.

Protože official EM/exotic stats jsou lower-frequency, revised and sometimes opaque, AI-gathered
figures carry **per-tier confidence** shown as reliability badge.

## Graceful degradation

| Calendar | AI | Výsledek |
|---|---|---|
| ✅ | ✅ | Full ranking + forward projection + narrative (`CalendarAndAi`). |
| ✅ | ❌ | Calendar-only current ranking, no forward projection (`CalendarOnly`). |
| ❌ | ✅ | AI-gathered current figures + forward, lower confidence (`AiOnly`). |
| ❌ | ❌ | Žádný snapshot — widget se skryje a stránka ukáže empty state. |

Aplikace běží nezměněně ať tak či onak. AI je gated na AI key; calendar leg respects its own
white-label gate + runtime toggle.

## Použití

- **Enable AI** (Settings → AI) a **turn on the widget** ze svého vlastního dashboard **Customize** dialogu
  ("Currency strength" — opt-in, hidden by default). Widget ukazuje top strong/weak currencies a
  top 3M pair call; odkazuje na full page.
- **Full page** — `/ai/currency-strength`: horizon selector (1M/3M/6M/12M), tier filter
  (All/Majors/EM/Exotics), current ranking, forward forecast, pair-outlook matrix (bias +
  conviction, pegged/low-confidence flagged), a AI narrative. Stiskněte **Refresh now** (owner) pro
  regeneraci. A background worker (`App:CurrencyStrength:RefreshEnabled`, **default `true`**) refreshes on
  schedule takže page je populated out of the box; deployment nebo owner ho vypne (nebo zakáže
  AI / economic-calendar feature, který refresher ctí degradací na žádný snapshot).

## Programmatický přístup

One shared read model (`ICurrencyStrengthQuery`) je dosažitelný třemi způsoby:

- **In-app AI** — injected directly (in-process) do AI funkcí.
- **MCP** — `currency_strength` tool (params `horizon`, `tier`) pro AI klienty/agenty.
- **cBot REST** — `GET /api/market/v1/currency-strength/{latest,history,pair/{base}/{quote}}`, secured
  by **same** `CalendarJwt` machinery jako [calendar cBot API](./calendar-cbot-api.md) with an
  added **`market:read`** scope. A cBot registers an API client with `market:read`, exchanges its
  id + secret for a short-lived JWT at `POST /api/calendar/v1/token`, and calls the endpoints with a
  `Bearer` token. Žádný druhý JWT scheme, žádné druhé tajemství — a leaked token is read-only, market-scoped,
  short-lived and revocable.

Viz [calendar cBot API](./calendar-cbot-api.md) pro token flow a copy-paste sample.
