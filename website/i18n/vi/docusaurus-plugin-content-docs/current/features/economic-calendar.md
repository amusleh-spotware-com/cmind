# Economic calendar

cMind ships its **own** economic calendar — release schedule, actuals, forecasts, revisions và data-driven impact model — sourced từ **primary authorities** (central banks và national statistical agencies), với **zero dependency** trên ForexFactory, FXStreet, Investing.com hoặc bất kỳ aggregator nào. Nó là point-in-time correct, giữ ≥10 năm history, và được wired vào trading, public API, MCP, cBots, AI, alerts và backtests. Nó là decoupled module: nó có thể được disabled với zero effect trên trading core.

> **Status.** The domain core (impact model, country→symbol mapping, news-window policy, point-in-time revision chains, two-tier gating) **and** persistence (the `calendar` Postgres schema, the append-only read/write side, the FRED connector và config-gated ingestion worker) được implemented và tested (unit + Testcontainers integration). JWT REST API, MCP tools và UI land trong subsequent rollout phases được mô tả dưới đây.

## Điều gì làm cho nó khác

Những than phiền tái diễn chống leading calendars trở thành design constraints của chúng tôi:

- **Không silent impact-rating changes.** Impact rating của chúng tôi là **deterministic, versioned và auditable**. Mỗi change là recorded revision với timestamp — không bao giờ silent overwrite. User có thể thấy chính xác *tại sao* event là High.
- **One UTC anchor per event.** Mỗi event được anchored tới single UTC instant từ primary source's official schedule; source's own timezone được stored, và per-user rendering sử dụng explicit IANA timezone với DST được xử lý bởi zone database — không bao giờ manual ±1h toggle.
- **Full revision chains, ở mọi nơi.** Original value và mỗi revision là first-class, exposed identically qua API, MCP và cBot surfaces.
- **≥10 năm history, không wall.** Unrestricted browsing range; không 60-day cap, không registration gate.
- **Point-in-time by construction.** Mỗi fact carries `KnownAt` (khi *chúng tôi* learned it) và `EffectiveAt` (event instant). "As the calendar looked at time T" là first-class query, nên backtested news rule behaves chính xác như live — không look-ahead từ using revised values trong history.

## The impact model

Impact score là pure, deterministic function trong `[0, 100]`, banded tới Low / Medium / High / Critical. Inputs của nó chỉ là data known tại scoring time (không future leak):

- **Series prior** — baseline weight per indicator class (rate decision outweighs CPI, outweighs minor survey).
- **Realized-volatility footprint** — median absolute return của primary affected symbols trong window sau *past* releases của series này: "this release historically moves price this much."
- **Surprise sensitivity** — how strongly absolute surprise (z-score) has historically correlated với post-release move.

Score blends những cái này với fixed weights và stamps `ImpactModelVersion`. Recompute là explicit, logged operation tạo **new revision** — không bao giờ mutate — nên score luôn reproducible từ inputs của nó.

## Country → currency → symbol mapping

Single most-cited algo integration papercut được solved một lần, như pure function: country maps tới currency của nó (mỗi euro-area member fans in tới EUR), và currency maps tới watchlist symbols quoting nó trên either leg. Nên **EURUSD bị affected bởi EU và US events**; XAUUSD là USD-exposed; US500 maps tới USD. Cái này drives news filter, affected-symbols resolution và blackout math.

## News-window policy

`NewsWindowRule` là `{ minImpact, beforeMinutes, afterMinutes, currencies?, series? }`. Single, shared, pure implementation trả lời "is instant T inside blackout cho symbol S?" — used bởi cBot news filter, copy-trade pause và AI risk guard, nên chúng không thể bao giờ diverge. Trên uncertainty blackout answer defaults tới configured conservative value (fail-closed by default) nên data gap không bao giờ silently green-lights trading qua high-impact release.

## Point-in-time & revisions

Actuals, forecasts và impact scores là **append-only**. Mỗi event owns ordered chain của revisions, monotonic trong `KnownAt`:

- `Scheduled` — event được first scheduled (prior impact, không actual).
- `Released` — first printed actual arrived.
- `Revised` — later revised value arrived.
- `Rescheduled` — source moved release instant (auditable, alertable).
- `Rescored` — impact score được recomputed dưới new model version.

Querying `as of` past instant trả về exactly revision known then — guarantee tạo kill look-ahead trong backtested news rules.

## Forecast / consensus

Survey median của economists **không** freely published bởi primary sources — nó là aggregators' proprietary value-add, và chúng tôi không fabricate nó. Event schema carries nullable `Forecast`; deployment có thể wire licensed consensus feed qua optional `IForecastProvider` port (bring-your-own key, off by default). Previous values và revisions luôn come từ official source.

## Data sources

Hai decoupled layers, tất cả primary — không bao giờ aggregator:

- **Schedule / timing:** FRED release calendar; national statistical agencies (BLS, BEA, Census, Eurostat, ONS, Destatis, INSEE, e-Stat, ABS, StatCan); central-bank meeting calendars (Fed, ECB, BoE, BoJ, RBA, BoC, SNB, RBNZ).
- **Actual values:** FRED (với vintage dates cho revisions và point-in-time), cộng với BLS, BEA, Census, ECB SDW, Eurostat và OECD SDMX APIs.

Dead source degrades coverage cho **that source chỉ**; calendar giữ serving mọi thứ khác và surfaces gap như freshness metric.

## Rate limiting & backup plan
