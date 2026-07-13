# Economic calendar

cMind มาพร้อม **economic calendar ของตัวเอง** — release schedule, actuals, forecasts, revisions
และ impact model ที่ขับเคลื่อนด้วยข้อมูล — มาจาก **primary authorities** (central banks และ
national statistical agencies), ด้วย **ไม่พึ่งพา** ForexFactory, FXStreet, Investing.com หรือ
aggregator ใดๆ มัน point-in-time correct, เก็บ ≥10 ปี history และเชื่อมต่อกับ trading,
public API, MCP, cBots, AI, alerts และ backtests มันเป็น decoupled module: สามารถ
disable ได้โดยไม่มีผลกระทบต่อ trading core

> **สถานะ.** Domain core (impact model, country→symbol mapping, news-window policy,
> point-in-time revision chains, two-tier gating) **และ** persistence (calendar Postgres schema,
> append-only read/write side, FRED connector และ config-gated ingestion worker) ถูก implement
> และ test แล้ว (unit + Testcontainers integration) JWT REST API, MCP tools และ UI
> มาถึงใน phases ถัดไปที่อธิบายด้านล่าง

## อะไรทำให้มันแตกต่าง

ข้อร้องเรียนที่เกิดซ้ำเทียบกับ calendars ชั้นนำกลายเป็น design constraints ของเรา:

- **ไม่มี silent impact-rating changes.** Impact rating ของเราเป็น **deterministic, versioned
  และ auditable** ทุก change เป็น recorded revision พร้อม timestamp — ไม่เคย silent overwrite
  user สามารถเห็นได้ว่าทำไม event เป็น High
- **หนึ่ง UTC anchor ต่อ event.** ทุก event ถูก anchor ไปที่ UTC instant เดียวจาก official
  schedule ของ primary source; timezone ของ source ถูกเก็บ และ per-user rendering ใช้ explicit
  IANA timezone พร้อม DST handled โดย zone database — ไม่เคย manual ±1h toggle
- **Full revision chains, ทุกที่.** ค่าเดิมและทุก revision เป็น first-class, expose เหมือนกัน
  ผ่าน API, MCP และ cBot surfaces
- **≥10 ปี history, ไม่มี wall.** ไม่จำกัด browsing range; ไม่มี 60-day cap, ไม่มี registration gate
- **Point-in-time by construction.** ทุก fact มี `KnownAt` (เมื่อ *เรา* เรียนรู้มัน) และ
  `EffectiveAt` (event instant) "อย่างที่ calendar ดู ณ เวลา T" เป็น first-class query
  ดังนั้น backtested news rule ทำตัวเหมือน live — ไม่มี look-ahead จากการใช้ revised values
  ใน history

## Impact model

Impact score เป็น pure, deterministic function ใน `[0, 100]`, banded เป็น Low / Medium / High /
Critical อินพุตของมันเป็นข้อมูลที่รู้ ณ เวลาที่ score (ไม่มี future leak):

- **Series prior** — baseline weight ต่อ indicator class (rate decision outweighs CPI ซึ่ง
  outweighs minor survey)
- **Realized-volatility footprint** — median absolute return ของ primary affected symbols ใน
  window หลัง *past* releases ของ series นี้: "release นี้ historically ขยับราคามากแค่ไหน"
- **Surprise sensitivity** — ความแรงที่ absolute surprise (z-score) historically correlated กับ
  post-release move

score ผสมสิ่งเหล่านี้ด้วย fixed weights และ stamp `ImpactModelVersion` Recompute เป็น
explicit, logged operation ที่สร้าง **new revision** — ไม่เคย mutate — ดังนั้น score
สามารถ reproduce จากอินพุตได้เสมอ

## Country → currency → symbol mapping

algo integration papercut ที่ถูก cite มากที่สุดถูก solve ครั้งเดียว, เป็น pure function:
country maps ไปยัง currency (ทุก euro-area member fans in ไปยัง EUR), และ currency maps
ไปยัง watchlist symbols ที่ quote มันบน either leg ดังนั้น **EURUSD ได้รับผลกระทบจากทั้ง EU
และ US events**; XAUUSD มี USD exposure; US500 maps ไปยัง USD สิ่งนี้ขับเคลื่อน news
filter, affected-symbols resolution และ blackout math

## News-window policy

`NewsWindowRule` คือ `{ minImpact, beforeMinutes, afterMinutes, currencies?, series? }`
implementation เดียว, shared, pure ตอบคำถามว่า "instant T อยู่ใน blackout สำหรับ symbol S
หรือไม่" — ใช้โดย cBot news filter, copy-trade pause และ AI risk guard ดังนั้นพวกเขา
ไม่มีทาง diverge เมื่อ uncertainty ให้ conservative default (fail-closed by default)
ดังนั้น data gap ไม่เคย silently green-lights trading ผ่าน high-impact release

## Point-in-time & revisions

Actuals, forecasts และ impact scores เป็น **append-only** แต่ละ event owns ordered chain ของ
revisions, monotonic ใน `KnownAt`:

- `Scheduled` — event ถูก schedule ครั้งแรก (prior impact, ไม่มี actual)
- `Released` — actual พิมพ์ครั้งแรกมาถึง
- `Revised` — revised value มาถึง
- `Rescheduled` — source ย้าย release instant (auditable, alertable)
- `Rescored` — impact score ถูก recompute ภายใต้ model version ใหม่

Query `as of` instant ในอดีต return revision ที่รู้ตอนนั้นพอดี — guarantee ที่ฆ่า
look-ahead ใน backtested news rules

## Forecast / consensus

survey median ของ economists เป็น **ไม่** freely published โดย primary sources — มันคือ
proprietary value-add ของ aggregators และเราไม่ประดิษฐ์มัน event schema มี nullable
`Forecast`; deployment อาจ wire licensed consensus feed ผ่าน optional `IForecastProvider` port
(bring-your-own key, off by default) ค่าก่อนหน้าและ revisions มาจาก official source เสมอ

## Data sources

สอง decoupled layers, ทั้งหมดเป็น primary — ไม่เคยเป็น aggregator:

- **Schedule / timing:** FRED release calendar; national statistical agencies (BLS, BEA, Census,
  Eurostat, ONS, Destatis, INSEE, e-Stat, ABS, StatCan); central-bank meeting calendars (Fed, ECB,
  BoE, BoJ, RBA, BoC, SNB, RBNZ)
- **Actual values:** FRED (พร้อม vintage dates สำหรับ revisions และ point-in-time), บวก BLS, BEA, Census,
  ECB SDW, Eurostat และ OECD SDMX APIs

dead source degrades coverage สำหรับ **source นั้นเท่านั้น**; calendar ยัง serve ทุกอย่างอื่น
และ surface gap เป็น freshness metric

## Rate limiting & the backup plan

External providers publish rate limits (FRED allows ~120 requests/minute) Calendar สร้างมา
เพื่อมัน **ไม่เคย trip provider's limit** และเพื่อให้ being throttled หรือ cut off ไม่
degrade reads:

- **Proactive throttling.** HTTP client ของทุก source ไปผ่าน shared, thread-safe rate gate ที่
  spaces outbound requests ไปยัง configured budget (`App:Calendar:FredRequestsPerMinute`,
  default 100 — deliberately under provider ceiling) Requests ถูก queue และ pace, ไม่เคย burst
- **Honour `429 Retry-After`.** ถ้า provider return `429 Too Many Requests`, gate ถอย source ทั้งหมด
  off โดย server-requested cooldown (หรือ `App:Calendar:RateLimitBackoff`, default 60s)
  ก่อน call ถัดไป — ไม่มี tight retry loop
- **Standard resilience.** แต่ละ source client ยัง inherit app-wide resilience handler (retry
  พร้อม backoff + jitter, circuit breaker, timeouts) ดังนั้น transient blips ถูก absorbed และ
  persistently failing source ถูก parked (coverage ไป stale) โดยไม่กระทบต่ออื่น
- **The backup plan — durable read-through cache.** Reads **ไม่เคย** serve โดย calling provider
  เมื่อ range ถูก fetch มันถูก persisted append-only ไปยัง Postgres และ serve จากนั้นตลอดไป
  (ดู §"On-demand load") ดังนั้นแม้เมื่อ source ถูก rate-limited หรือ down, calendar ยัง
  answer จาก cached, point-in-time-correct data; missing span simply stays uncovered และ
  retry บน next ingestion cycle Blackout answers additionally fail ไปยัง conservative default
  under uncertainty ดังนั้น data gap ไม่เคย green-lights trading ผ่าน release
- **Cheap polling.** Conditional fetch (ETag / If-Modified-Since / source vintage cursors) และ
  "fetch a span once, never again" cache keep actual request volume far below any limit ใน
  normal operation — rate gate เป็น safety net ไม่ใช่ common path

## Enable / disable

สอง independent tiers, เหมือนกับ features อื่นๆ ของ cMind:

- **Tier 1 — runtime feature toggle** (`Feature.EconomicCalendar`) flip จาก Features admin UI;
  ไม่ต้อง redeploy, มีผล live
- **Tier 2 — white-label hard gate** (`App:Branding:EnableEconomicCalendar`, default `true`)
  reseller ตั้ง `false` เพื่อเอา feature ออกทั้งหมด; operator จากนั้นไม่สามารถ re-enable มันได้

Effective state คือ `Branding.EnableEconomicCalendar && FeatureToggle.EconomicCalendar`
เมื่อ disabled, nav entry ถูกซ่อนและ `/economic-calendar`, `/api/calendar/**` และ MCP
calendar tools return clean feature-disabled `404` — ไม่เคย `500` Persisted history
ถูก retain เมื่อ runtime toggle-off ดังนั้น re-enabling เป็น instant

## Rollout phases

- **P0 — domain core** *(implemented)*: aggregates, value objects, ports, impact model,
  country→symbol mapping, news-window policy, two-tier gating, full unit suite
- **P1 — persistence + one source** *(implemented)*: EF `calendar` schema (ตารางของตัวเอง,
  append-only, hot indexes), read-through `IEconomicCalendar` reader พร้อม point-in-time `asOf`,
  idempotent append-only write service, FRED connector behind resilient typed client และ
  config-gated ingestion worker; Testcontainers integration tests (persistence, PIT,
  idempotency, blackout)
- **P2 — public JWT REST API + Web UI** *(implemented)*: versioned, JWT-secured `/api/calendar/v1`
  API — client issuance, token exchange และ core read endpoints (events, history, series,
  surprises, next, blackout, affected-symbols, health) พร้อม scope enforcement และ two-tier
  gating, integration-tested บวก mobile-first **`/economic-calendar` page** — gated,
  fully-localized (23 languages) agenda ของ upcoming releases เป็น phone-friendly cards
  พร้อม colour-banded impact chips และ MudBlazor **filter dialog** (currencies + minimum
  impact + **From-date** picker เพื่อกระโดดไปยัง **วันที่ในอดีตใดก็ได้**ข้าม full history
  — ไม่มี 60-day cap, ไม่มี wall); nav entry, smoke/mobile/a11y/E2E tested per-indicator
  series history page (`/economic-calendar/series/{code}`, linked จากแต่ละ event) lists
  series' full print history surprise charts + infinite-scroll browser ตามมา
- **P3 — more sources & warm-up** *(started)*: **core-series catalog** (CPI, Core CPI, NFP,
  unemployment, GDP, PCE, Fed funds, retail sales → their FRED ids) seeded automatically on
  startup, และ one-time, idempotent, year-chunked **proactive backfill** pulls ≥10-year
  history ดังนั้น common case warm โดยไม่ต้องรอ user miss **Ingestion is on by default**
  (`App:Calendar:IngestionEnabled`, default `true`): **central-bank schedule source** ต้องการ
  **ไม่มี API key** ดังนั้น FOMC / ECB / BoE decision calendar populate out of the box —
  backfill seeds those meeting dates ข้าม **both recent history และ forward horizon** ดังนั้น
  browsing *last month* (หรือ window ใดก็ได้ในอดีต) แสดง meetings แม้ก่อน FRED/BLS key
  ถูก configure; value series เติมเมื่อ keys ถูก set workers honor calendar's two-tier gate
  — white-label deployment หรือ owner disabling economic-calendar feature stop ingestion และ
  `App:Calendar:IngestionEnabled=false` turn it off explicitly **Per-source freshness** ตอนนี้
  real เช่นกัน: worker record แต่ละ source's last successful poll, consecutive-failure count และ
  tripped-circuit flag (persisted in app settings, cross-process) และ `/health` endpoint +
  `calendar_health` MCP tool report truthful `stale` verdict ต่อ source **BLS** (2nd value
  source) และ **central-bank schedule source** (FOMC / ECB / BoE decision dates, backfilled
  ข้าม history และ synced forward into horizon window โดย worker) อยู่ใน ยังมาถึง: BEA/Census/ECB-SDW/Eurostat/OECD value sources และ reconciliation pass
- **P4 — deep integration**: **MCP tools** *(implemented — full read-API parity:
  `calendar_events`, `calendar_event`, `calendar_history`, `calendar_series`,
  `calendar_surprises`, `calendar_next`, `calendar_blackout`, `calendar_affected_symbols`,
  `calendar_health`, gated on the feature)* และ **alerts `EconomicEvent` trigger**
  *(implemented — `AlertRule` ที่ fire N นาทีก่อน upcoming release ที่/สูงกว่า chosen
  impact, แบบ optional narrowed ไปยัง currencies; evaluated โดย existing alert worker
  ด้วย no AI, de-duplicated ต่อ release; สร้างผ่าน
  `POST /api/alerts/rules/economic-event`)* prop-guard news-blackout gate **และ
  copy-trade blackout pause** อยู่ใน (§5.1 — opt-in `App:Copy:NewsPauseEnabled`, default
  off: source position open ที่ symbol อยู่ใน Critical-impact blackout ถูก skip, hot path
  byte-identical เมื่อ off) **backtest event overlay** อยู่ใน — `GET /api/calendar/v1/for-symbol`
  และ `calendar_events_for_symbol` MCP tool return point-in-time-correct events ที่มีผลต่อ
  symbol ใน window และ **instance/backtest report page** render high-impact releases ที่ตก
  อยู่ใน backtest window ใต้ equity curve (ดังนั้น author เห็นว่า trades ไหนลงบน NFP),
  gated และ localized แผนทั้งหมดตอนนี้ implemented
- **P5 — extras**: surprise analytics, iCal/CSV export, keyword search, pluggable consensus

ดู [cBot & REST API reference](calendar-cbot-api.md) สำหรับ integration surface
