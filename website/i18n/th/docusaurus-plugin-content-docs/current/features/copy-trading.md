---
description: "Mirror master cTrader account ไป one+ slave accounts — cross-broker cross-cID — ด้วย per-destination control + money-grade reconciliation"
---

# Copy trading

Mirror **master** cTrader account ไป one+ **slave** accounts — cross-broker cross-cID — ด้วย per-destination control + money-grade reconciliation

## Concepts

- **Copy profile** — one master (`SourceAccountId`) + one+ **destinations** Lifecycle: `Draft → Running → Paused → Stopped` (`Error` on failure) Aggregate root: `CopyProfile` (owns `CopyDestination`)
- **Destination** — one slave account + full rule set สำหรับ how master copied ไปยัง มัน ทั้งหมด config per-destination ดังนั้น one master feeds conservative + aggressive slaves at once
- **Copy engine host** — running worker สำหรับ profile (`CopyEngineHost`) Subscribes master execution stream applies ทุก ๆ event ไปยัง ทุก ๆ destination
- **Supervisor** — `CopyEngineSupervisor` background service บน ทุก ๆ node Hosts assigned profiles self-heals ข้ามบน cluster (ดู [scaling](../deployment/scaling.md))

## สิ่งที่ get mirrored

| Master event | Slave action |
|--------------|--------------|
| Market / market-range position open | Open sized copy (labelled ด้วย source position id) |
| Limit / stop / stop-limit pending order | Place matching pending order |
| Pending order amend | Amend mirrored pending order in place |
| Pending order cancel / expiry | Cancel mirrored pending order |
| Partial close | Close same proportion ของ slave position |
| Scale-in (volume increase) | Open added volume (opt-in) |
| Stop-loss / trailing-stop change | Amend slave position's protection |
| Full close | Close slave copy |

ทุก ๆ copy **labelled ด้วย source position/order id** หลัง reconnect host rebuilds state จาก reconcile: opens copies master holds แต่ slave missing closes slave "orphans" master ไม่ยึด — **โดยไม่ duplicating trades**

## Creating profile

**New Profile** dialog บน Copy Trading page collects ทั้งหมด up front: profile name source (master) account destination (slave) accounts (multi-select ด้วย **Select ทั้งหมด** button; chosen master excluded จาก slave list) + full per-destination option set ด้านล่าง ทั้งหมด inputs **validated ก่อน saving** — missing name/source/destination non-positive sizing param negative/inconsistent lot bounds out-of-range drawdown % no order type enabled empty symbol filter หรือ malformed symbol-map pairs surface เป็น error list + block save บน confirm profile created + ทุก ๆ selected slave added ด้วย chosen settings

Row actions respect lifecycle: **Start** enabled เพียง when ไม่ running **Stop** + **Pause** เพียง when running **Delete** disabled ขณะ running + asks confirmation ก่อน removing profile + destinations

## Per-destination options

Set ใน New Profile dialog บน Copy Trading page's per-destination panel หรือ ผ่าน `POST /api/copy/profiles/{id}/destinations`:

- **Sizing** (`MoneyManagementMode` + parameter): fixed lot lot/notional multiplier proportional balance/equity/free-margin fixed risk % fixed leverage auto-proportional **risk-%-from-stop** (M7) บวก min/max lot bounds + force-min-lot **Risk-from-stop** sizes destination ดังนั้นมัน risks configured percent ของ *its own* balance derived จาก **master's stop-loss distance** (`master risks 2% → slave auto-risks 2%`): `lots = balance×% ÷ (stopDistance × contractSize)` master open **โดยไม่** stop-loss มี no distance เพื่อ size against → uses configured **max-risk fallback lot** (M7) ถ้า set else skipped (`no_stop_loss`) ไม่ guessed proportional-**equity**/**free-margin** size off real account **equity** (`balance + Σ floating P&L` derived per cTrader Open API ซึ่งไม่ deliver equity) ไม่ plain balance — ดังนั้น master sitting บน open profit/loss sizes copies right used margin ไม่ exposed โดย reconcile API ดังนั้น free-margin treated เป็น equity (honest available-funds proxy); other modes read balance + skip extra revaluation round-trip
- **Direction filter**: both / long-only / short-only **Reverse**: flip side (+ swap SL↔TP) สำหรับ contrarian copy
- **Manage-only** (Ignore-New-Trades / Close-Only): mirror closes partial closes + protection changes บน already-copied positions แต่ open **no** new positions/pending orders (skipped `manage_only`) ใช้ เพื่อ wind destination ลง โดยไม่ cutting existing copies
- **Sync-Open-on-start** / **Sync-Closed-on-start** (default on): บน profile's **first** resync whether เพื่อ open copies สำหรับ master's pre-existing positions + whether เพื่อ close copies master closed ขณะ profile stopped ทั้งสอง apply เพียง at start — mid-run reconnect always reconciles fully ดังนั้น desync recovers regardless
- **Symbol map** + **symbol filter** (whitelist / blacklist) ทุก ๆ symbol-map entry carries optional **per-symbol volume multiplier** (cMAM per-symbol override) scaling copy size สำหรับ symbol นั่น บน top ของ destination's sizing (1 = ไม่มี change) whole map imports/exports เป็น **CSV** (`GET …/symbol-map.csv` `PUT …/symbol-map/csv`; columns `Source,Destination,VolumeMultiplier`) — ทุก ๆ row validated ผ่าน domain value objects ดังนั้น malformed file ไม่สามารถ produce invalid map
- **Trading-hours window** (C18) — per-destination daily UTC window (`start`/`end` minutes-of-day end exclusive; `start == end` = all-day) new opens outside window skipped (`trading_hours`); window ด้วย `start > end` wraps past midnight (e.g. 22:00–06:00) existing positions stay managed
- **Source-label filter** (C18 cTrader equivalent ของ MT magic-number filter) — เมื่อ set copy เพียง master trades whose label matches **ตรง** (e.g. one bot's trades หรือ manual-only label); else skipped (`source_label`) empty = copy ทั้งหมด carried บน `ExecutionEvent.SourceLabel` จาก master position/order's `TradeData.Label` honored บน resync ด้วย
- **Account protection** (ZuluGuard / Global Account Protection) — watch destination's **live equity** (`balance + Σ floating P&L` polled ทุก ๆ `CopyDefaults.EquityGuardInterval`) against `StopEquity` floor and/or optional `TakeEquity` ceiling บน breach apply mode: **CloseOnly** (stop new copies keep managing existing) **Frozen** (stop opening) **SellOut** (close **every** copy บน destination immediately) เมื่อ fired destination latched — ไม่มี new opens จนกว่า host restarts — + `CopyAccountProtectionTriggered` alert raised `SellOut` requires `StopEquity`; `TakeEquity` ต้อง sit above `StopEquity` **No-guarantee caveat:** sell-out ใช้ market execution — เหมือน ทุก ๆ competitor's equivalent ไม่สามารถ guarantee fill price ใน fast/gapped market
- **Flatten-All panic button** (C8) — `POST /api/copy/profiles/{id}/flatten` immediately closes **every** copied position บน ทุก ๆ destination + locks against new opens routed cross-process: API sets flag supervisor delivers เป็น running host (reusing token-rotation channel) ซึ่ง flattens in place; flag cleared ดังนั้น fires ตรง once (`CopyFlattenAll` alert) user แล้ว pauses/stops profile
- **Prop-firm rule guard** (C7) — enforcement prop-firm copier users ask สำหรับ per destination **daily-loss cap** (loss จาก day's opening equity) and/or **trailing-drawdown** limit (loss จาก running peak equity) ทั้งสอง ใน deposit currency บน breach destination **auto-flattened** (ทุก ๆ copy closed) + **locked out** rest ของ UTC day (new opens skipped `prop_lockout`); `CopyPropRuleBreached` alert fires lockout clears เมื่อ UTC day rolls over (fresh baseline/peak taken) shares same live-equity poll เป็น account protection
- **Execution jitter** (C11 off โดย default) — random `0..N` ms delay ก่อน placing ทุก ๆ copy เพื่อ de-correlate near-identical order timestamps ข้ามบน user's **own** accounts **Compliance caveat:** aid สำหรับ prop firms ที่ *permit* copying — **ไม่** tool เพื่อ evade firm ที่ forbids มัน; staying within firm's rules ของคุณ คือ responsibility ของคุณ
- **Config lock** (C9) — freeze destination's settings สำหรับ period (`POST …/destinations/{id}/lock` ด้วย minutes) ขณะ locked destination ไม่สามารถ removed (aggregate rejects ด้วย `CopyDestinationConfigLocked`) — deliberate guard ต้านแบบ impulsive changes ระหว่าง drawdown lock expires automatically ที่ timestamp ของมัน
- **Consistency pre-alert** (C10) — warn (once per UTC day) เมื่อ destination's **daily profit** reaches configured percent ของ day's opening equity (`CopyConsistencyThresholdApproaching`) ดังนั้น prop-firm consistency rule respected *ก่อน* มัน trips profit-side independent ของ loss-side lockout; runs off same day baseline เป็น prop-rule guard
- **Order-type filter** — เลือก ตรง which master order types เพื่อ copy: market market-range limit stop stop-limit (`CopyOrderTypes` flags; default ทั้งหมด) cMAM-style selectivity
- **Copy SL / Copy TP** — mirror master's stop-loss / take-profit หรือ manage protection independently
- **Copy trailing stop** **mirror partial close** **mirror scale-in** — ทุก ๆ independently toggleable
- **Copy pending expiry** (default on) — mirror master pending order's Good-Till-Date expiry timestamp
- **Copy master slippage** (default on) — สำหรับ market-range + stop-limit orders place slave order ด้วย master's ตรง slippage-in-points (base price taken จาก slave's live spot)
- **Guards**: max drawdown % daily loss cap max copy delay slippage filter (skip copy ถ้า slave price moved beyond N pips จาก master entry) **Max copy delay** measured ต้านแบบ master event's real server timestamp (`ExecutionEvent.ServerTimestamp`) ผ่าน injected `TimeProvider`: signal older than configured max-lag skipped ดังนั้น stale copy ไม่เคย placed late (previously delay always zero + guard dead)
- **SL/TP precision normalization** (M6) — copied stop-loss/take-profit prices rounded เป็น **destination** symbol's digit precision ก่อน amend ดังนั้น master price ที่ finer precision (หรือ cross-broker digit mismatch) ไม่เคย trips server's `INVALID_STOPLOSS_TAKEPROFIT`
- **Rejection circuit breaker / Follower Guard** (G8) — destination rejecting `CopyDefaults.RejectionBudget` opens in row **tripped**: ไม่มี new opens สำหรับ cooldown window (`CopyDestinationTripped` alert fires) stopping rejection storm จาก hammering (prop-firm) account existing positions still managed + closed ขณะ tripped; breaker auto-resets หลัง cooldown + successful copy clears counter
- **Lot sanity ceiling** (C14) — absolute max copy size and/or multiple-of-master cap computed copy exceeding absolute cap หรือ exceeding `N×` master's own lot size **hard-blocked** (surfaced เป็น `lot_sanity` skip counted บน `cmind.copy.skipped`) ไม่ placed — defends ต้านแบบ catastrophic-oversize class (0.23-lot master turning into 3 lots บน ทุก ๆ receiver ผ่าน runaway multiplier หรือ rounding bug) ทั้งสอง dimensions default `0` (off)

## Reliability & edge cases

engine built สำหรับ reality ที่ anything สามารถ fail anytime:

- **Slave-pending fill-correlation timeout** (C13) — mirrored slave pending whose master pending vanished (ไม่ resting หรือ freshly filled) cancelled หลัง correlation timeout ดังนั้น slave copy ไม่สามารถ fill uncorrelated เป็น unmanaged position (`CopyPendingTimedOut`) resync also cleans order-id-labelled filled-pending orphan
- **Robust close/flatten** (M8) — closing orphan บน resync หรือ flattening บน guard breach tolerates position broker already closed (`POSITION_NOT_FOUND`): ทุก ๆ close runs independently ดังนั้น one stale id ไม่เคย aborts resync หรือ leaves rest ของ account un-flattened
- **Start ด้วย master already ใน trades** — บน start host reconciles + opens copies สำหรับ master's existing positions
- **Connection drops / desync** — บน reconnect host reconciles: opens missing copies closes orphans re-labels pendings ไม่มี duplicate orders
- **Order placement failure** — failure บน one destination logged ไม่เคย blocks other destinations
- **Single valid token per cID** — cTrader invalidates cID's old access token moment new one issued cMind swaps running host's token **in place** (re-auth บน live socket) ดังนั้น copying continues โดยไม่ dropping stream ดู [token lifecycle](token-lifecycle.md)

## Auditability

ทุก ๆ action emits structured source-generated log event (`LogMessages`) ด้วย profile id destination cID order/position ids + values — order placed/skipped (ด้วย reason) partial close protection applied trailing applied pending placed/amended/cancelled expiry mirrored market-range slippage mirrored token swapped resync summary นี่คือ audit trail สำหรับ compliance + dispute resolution

alongside logs engine emits **OpenTelemetry metrics** บน `cMind.Copy` meter (registered ใน shared OTel pipeline exported over OTLP / เป็น Azure Monitor เหมือน rest): `cmind.copy.latency` (master-event → dispatch ms) `cmind.copy.dispatch.duration` (fan-out เป็น ทั้งหมด destinations ms) `cmind.copy.slippage.points` `cmind.copy.placed` (tagged โดย destination) `cmind.copy.skipped` (tagged โดย reason) + `cmind.copy.failed` เหล่านี้ทำให้ latency/slippage regression measurable ไม่ just visible ใน log line — live suite asserts พวกเขา ต้านแบบ budget

## API

- `GET /api/copy/profiles` — list
- `POST /api/copy/profiles` — create (ด้วย optional destination account ids)
- `GET /api/copy/profiles/{id}` — full detail incl. ทุก ๆ destination option
- `POST /api/copy/profiles/{id}/destinations` — เพิ่ม destination ด้วย full option set
- `DELETE /api/copy/profiles/{id}/destinations/{destinationId}` — remove
- `POST /api/copy/profiles/{id}/{start|pause|stop}` — lifecycle

## Tests

- **Unit** (`tests/UnitTests/CopyTrading`) — sizing modes decision filters order-type filter expiry copy market-range/stop-limit slippage SL/TP toggles partial close pending amend/cancel start-with-open disconnect→desync→resync in-place token swap cross-cID invalidation runs ต้านแบบ `FakeTradingSession` cTrader-faithful in-memory simulator
- **Integration** (`tests/IntegrationTests/CopyLive`) — node-affinity/lease claim token-version propagation บน real Postgres
- **E2E** (`tests/E2ETests`) — destination-option round-trip ผ่าน API + UI full lifecycle
- **Stress / DST** (`tests/StressTests`) — deterministic-simulation testing: seeded randomized workloads + fault injection (socket flap order rejection market-range rejection token rotation node death) drive `CopyEngineHost` เป็น quiescence + assert convergence invariants ดู [testing/stress-testing.md](../testing/stress-testing.md) suite นี้ surfaced + fixed real startup race: `OnReconnected` wired ก่อน initial reference-load + resync ดังนั้น socket flap ระหว่าง startup สามารถ run second resync concurrently + corrupt host's non-concurrent state dictionaries — startup load + first resync now run ภายใต้ `_stateGate`
- **Live** — real cTrader demo accounts; ดู [testing/live-copy-trading.md](../testing/live-copy-trading.md)

ดู [dev-credentials.md](../testing/dev-credentials.md) สำหรับ single credentials file live + E2E tiers read
