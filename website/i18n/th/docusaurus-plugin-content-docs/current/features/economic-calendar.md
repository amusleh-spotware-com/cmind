# Economic calendar

cMind ships own economic calendar — release schedule actuals forecasts revisions และ data-driven impact model — sourced จาก **primary authorities** (central banks และ national
statistical agencies) ด้วย **zero dependency** on ForexFactory FXStreet Investing.com หรือ aggregator any มันคือ point-in-time correct เก็บ ≥10 years ของ history และ wired ไป trading public API MCP cBots AI alerts และ backtests มันเป็น decoupled module: มันสามารถ disabled ด้วย zero effect on trading core

> **Status** domain core (impact model country→symbol mapping news-window policy point-in-time
> revision chains two-tier gating) **และ** persistence (the `calendar` Postgres schema append-only
> read/write side FRED connector และ config-gated ingestion worker) implemented และ tested
> (unit + Testcontainers integration) JWT REST API MCP tools และ UI land ใน subsequent
> rollout phases described ด้านล่าง

## What makes มันแตกต่าง

recurring complaints against leading calendars became design constraints ของเรา:

- **No silent impact-rating changes** impact rating ของเรา **deterministic versioned และ
  auditable** ทุก change เป็น recorded revision ด้วย timestamp — ไม่เคยsilent overwrite user
  สามารถดู exactly *why* event high
- **One UTC anchor per event** ทุก event anchored ไป single UTC instant จาก primary
  source's official schedule; source ของเรา own timezone stored และ per-user rendering uses
  explicit IANA timezone ด้วย DST handled โดย zone database — ไม่เคยmanual ±1h toggle
- **Full revision chains everywhere** original value และ ทุก revision first-class exposed
  identically ผ่าน API MCP และ cBot surfaces
- **≥10 years ของ history ไม่มี wall** unrestricted browsing range; ไม่มี 60-day cap ไม่มี
  registration gate
- **Point-in-time by construction** ทุก fact carries `KnownAt` (when *we* learned มัน) และ
  `EffectiveAt` (the event instant) "ขณะที่ calendar ดู at time T" first-class query ดังนั้น
  backtested news rule behaves exactly like live — ไม่มี look-ahead from using revised values ใน
  history

## The impact model

impact score เป็น pure deterministic function ใน `[0, 100]` banded ไป Low / Medium / High /
Critical inputs ของมัน เป็นเพียงdata known ที่ scoring time (ไม่มี future leak):

- **Series prior** — baseline weight per indicator class (rate decision outweighs CPI ซึ่ง
  outweighs minor survey)
- **Realized-volatility footprint** — median absolute return ของ primary affected symbols ใน
  window หลัง this series' *past* releases: "this release historically moves price this much"
- **Surprise sensitivity** — how strongly absolute surprise (z-score) historically
  correlated ด้วย post-release move

score blends these ด้วย fixed weights และ stamps `ImpactModelVersion` Recompute explicit
logged operation ที่ produces **new revision** — ไม่เคยmutate — ดังนั้น score เสมอ
reproducible จาก inputs ของมัน

## Country → currency → symbol mapping

single most-cited algo integration papercut solved once เป็น pure function: country maps ไป
currency ของมัน (ทุก euro-area member fans ไป EUR) และ currency maps ไป watchlist symbols
quoting มัน on either leg ดังนั้น **EURUSD affected โดย both EU และ US events**; XAUUSD USD-exposed;
US500 maps ไป USD this drives news filter affected-symbols resolution และ blackout math

## News-window policy

`NewsWindowRule` คือ `{ minImpact, beforeMinutes, afterMinutes, currencies?, series? }` single
shared pure implementation answers "is instant T inside blackout สำหรับ symbol S?" — used โดย
cBot news filter copy-trade pause และ AI risk guard ดังนั้น พวกเขา ไม่เคย diverge on uncertainty
blackout answer defaults ไป configured conservative value (fail-closed by default) ดังนั้น data gap
ไม่เคยsilent green-lights trading ผ่าน high-impact release

## Point-in-time & revisions

actuals forecasts และ impact scores **append-only** แต่ละ event เป็นเจ้าของ ordered chain ของ
revisions monotonic ใน `KnownAt`:

- `Scheduled` — event first scheduled (prior impact no actual)
- `Released` — first printed actual arrived
- `Revised` — later revised value arrived
- `Rescheduled` — source moved release instant (auditable alertable)
- `Rescored` — impact score recomputed ภายใต้ new model version

Querying `as of` past instant returns exactly revision known then — guarantee ที่ kills
look-ahead ใน backtested news rules

## Forecast / consensus

survey median ของ economists **ไม่** freely published โดย primary sources — มันเป็น
aggregators' proprietary value-add และ เรา ไม่ fabricate มัน event schema carries nullable
`Forecast`; deployment อาจ wire licensed consensus feed ผ่าน optional `IForecastProvider`
port (bring-your-own key off by default) previous values และ revisions เสมอ come จาก official
source

## Data sources

two decoupled layers ทั้งหมด primary — ไม่เคย aggregator:

- **Schedule / timing:** FRED release calendar; national statistical agencies (BLS BEA Census
  Eurostat ONS Destatis INSEE e-Stat ABS StatCan); central-bank meeting calendars (Fed ECB
  BoE BoJ RBA BoC SNB RBNZ)
- **Actual values:** FRED (ด้วย vintage dates สำหรับ revisions และ point-in-time) บวก BLS BEA
  Census ECB SDW Eurostat และ OECD SDMX APIs

dead source degrades coverage สำหรับ **that source เพียงเท่านั้น**; calendar keeps serving everything
else และ surfaces gap เป็น freshness metric

## Rate limiting & the backup plan

External FRED/BLS calls are **rate-limited** (`App:Calendar:*Quota`) so a dead upstream gracefully degrades.
Calendar keeps serving cached data; `LastRefreshStatus` warns on a stale read. Intentional quotas (no keys,
sandbox limits) are **OK** — the reader gets the last-known snapshot. True network/auth failures inject
`CalendarIntegrationFailure` events that background workers retry (`BackfillWorker`, `RefreshWorker`).

On a persistent upstream death that spans many days, coverage drops for *just that data source* — a FRED
outage doesn't bleed into BLS. Fallback sourcing (e.g. BLS for US data if FRED is dead) and secondary
providers may be wired later; the architecture isolates source-specific faults.
