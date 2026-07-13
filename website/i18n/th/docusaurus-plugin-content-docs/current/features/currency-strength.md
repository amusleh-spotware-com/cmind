# AI macro currency strength & forward outlook

cMind ships an **AI-assisted, math-deterministic** macro currency-strength engine มันจัดอันดับ
configurable universe ของ currencies — 8 majors บวก emerging-market และ exotic currencies — โดย
**current** fundamental strength และ projects **forward directional outlook** สำหรับ ทุก pair over
chosen horizon (1M / 3M / 6M / 12M) ทุก rank ทุก pair bias และ ทุกตัวเลข computed โดย pure
deterministic math ใน domain core; LLM เพียง *gathers* forward-looking inputs data ไม่สามารถ
publish และ *explains* result ใน plain English มันไม่เคย invents rank direction หรือ
ตัวเลข

> **Honest limitation** Fundamentals predict medium-to-long-term value ดี และ short-term value
> ไม่ดี Treat this เป็น positioning / confluence filter **ไม่ใช่** short-term timing signal Readings
> near high-impact releases (NFP/CPI/central-bank) noisy ไม่ใช่ financial advice

## How it works

1. **Current fundamentals มาจาก Economic Calendar ไม่ใช่ LLM** hard numbers — policy
   rates CPI vs target GDP employment trade balance — และ **surprise z-scores** sourced
   **point-in-time** จาก [economic calendar](./economic-calendar.md) module (FRED/BLS/BEA/ECB และ
   central-bank schedules) historical snapshot ไม่เคยขั้ว look-ahead
2. **LLM gathers เฉพาะสิ่งที่ calendar ไม่สามารถ publish** — per currency: **forward** trajectory
   (expected policy-rate path ใน bp inflation-trend-vs-target growth momentum) และ **geopolitical**
   outlook (risk-on/off tariffs fiscal/debt elections) บวก any EM/exotic current figures calendar
   lacks Strict JSON tier-aware validation web search on
3. **domain computes ranking และ forward matrix deterministically** แต่ละ driver scored
   เป็น **within-tier z-score** (ดังนั้น 50%-inflation exotic ไม่เคย distorts majors) winsorized
   weight-summed ไป composite และ ranked strongest→weakest ด้วย stable ISO tie-break forward layer
   carries composite ตัวแต่ละตัว along trajectory ของมัน —
   `projected = current + horizonScale · Σ trajectoryDriver·weight` — และ maps ทุก pair ของ projected
   differential ไป **directional bias** (▲ appreciate / ▬ neutral / ▼ depreciate) ด้วย conviction
4. **LLM explains** ranking และ top pair calls ใน plain language

## The drivers

| Driver | Effect on strength | Notes |
|---|---|---|
| Policy rate & trajectory | Higher / hawkish ⇒ stronger | Highest weight; central-bank divergence drives biggest gaps |
| Inflation (CPI vs target) | Above target ⇒ weaker | Scored inversely (purchasing-power drag) |
| GDP growth | Higher relative growth ⇒ stronger | Differential vs panel |
| Employment | Stronger labour ⇒ stronger | Feeds policy path |
| Trade balance / current account | Surplus ⇒ stronger | Structural demand |
| Policy stance | Hawkish ⇒ stronger | primary long-term driver |
| Surprise momentum | Recent beats ⇒ stronger | จาก calendar's surprise z-scores |
| Geopolitical / risk | Risk-off ⇒ safe havens (USD/JPY/CHF) stronger | Bounded forward risk delta |
| Real yield / carry *(EM/exotic)* | Positive real rate ⇒ stronger | Dominant EM driver ใน calm regimes |
| External vulnerability *(EM/exotic)* | Deficits / low reserves / USD debt ⇒ weaker | Structural depreciation pressure |
| Terms ของ trade *(commodity exporters)* | Rising export prices ⇒ stronger | BRL ZAR CLP NOK AUD CAD |
| Political / institutional risk *(EM/exotic)* | Instability ⇒ weaker | Wider dead-band capped conviction |

## Tiered universe (majors + EM + exotics)

universe เป็น **deployment-configurable** (`App:CurrencyStrength:Universe`) — adding currency คือ
config ไม่ใช่ code แต่ละ currency carries **tier** (`Major` / `EmergingMarket` / `Exotic`) ที่ tunes
weighting dead-band width และ conviction cap:

- **Majors** — USD EUR GBP JPY AUD NZD CAD CHF (rate-level led)
- **Emerging markets** — CNH INR BRL MXN ZAR KRW SGD PLN (+ Scandi NOK/SEK); carry + risk +
  external-vulnerability weighted up medium confidence
- **Exotics** — TRY HUF CZK บวก USD-pegged HKD/SAR; low confidence wider dead-band capped
  conviction **Pegged / heavily-managed** currencies (HKD SAR CNH) flagged trajectory ของพวกเขา
  down-weighted และ pair outlook ของพวกเขา clamped ไป `Neutral` ดังนั้น peg ไม่เคยอ่าน as
  free-floating signal

เพราะ official EM/exotic stats lower-frequency revised และ บางครั้ง opaque AI-gathered
figures carry **per-tier confidence** shown เป็น reliability badge

## Graceful degradation

| Calendar | AI | Result |
|---|---|---|
| ✅ | ✅ | Full ranking + forward projection + narrative (`CalendarAndAi`) |
| ✅ | ❌ | Calendar-only current ranking ไม่มี forward projection (`CalendarOnly`) |
| ❌ | ✅ | AI-gathered current figures + forward lower confidence (`AiOnly`) |
| ❌ | ❌ | No snapshot — widget hides และ page shows empty state |

app runs ไม่เปลี่ยนแปลง either way AI gated on AI key; calendar leg respects own
white-label gate + runtime toggle

## Using it

- **Enable AI** (Settings → AI) และ **turn on widget** จาก own dashboard **Customize** dialog
  ("Currency strength" — opt-in hidden by default) widget shows top strong/weak currencies และ
  top 3M pair call; มันlinkถึง full page
- **Full page** — `/ai/currency-strength`: horizon selector (1M/3M/6M/12M) tier filter
  (All/Majors/EM/Exotics) current ranking forward forecast pair-outlook matrix (bias +
  conviction pegged/low-confidence flagged) และ AI narrative Press **Refresh now** (owner) ไป
  regenerate background worker (`App:CurrencyStrength:RefreshEnabled` **default `true`**) refreshes
  on schedule ดังนั้น page populated out ของ box; deployment หรือ owner turns มัน off (หรือ
  disables AI / economic-calendar feature ซึ่ง refresher honours โดย degrading ไป no snapshot)

## Programmatic access

One shared read model (`ICurrencyStrengthQuery`) reachable three ways:

- **In-app AI** — injected directly (in-process) ไป AI features
- **MCP** — `currency_strength` tool (params `horizon` `tier`) สำหรับ AI clients/agents
- **cBot REST** — `GET /api/market/v1/currency-strength/{latest,history,pair/{base}/{quote}}` secured
  โดย **same** `CalendarJwt` machinery เช่น [calendar cBot API](./calendar-cbot-api.md) ด้วย
  added **`market:read`** scope cBot registers API client ด้วย `market:read` exchanges มัน
  id + secret สำหรับ short-lived JWT ที่ `POST /api/calendar/v1/token` และ calls endpoints ด้วย
  `Bearer` token ไม่มี second JWT scheme ไม่มี second secret — leaked token read-only market-scoped
  short-lived และ revocable

ดู [calendar cBot API](./calendar-cbot-api.md) สำหรับ token flow และ copy-paste sample
