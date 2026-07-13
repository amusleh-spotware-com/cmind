---
description: "Mirror master cTrader account onto one+ slave accounts — cross-broker, cross-cID — with per-destination control + money-grade reconciliation."
---

# Copy trading

Mirror **master** cTrader account onto one+ **slave** accounts — cross-broker, cross-cID — with per-destination control + money-grade reconciliation.

## Concepts

- **Copy profile** — one master (`SourceAccountId`) + one+ **destinations**. Lifecycle: `Draft → Running → Paused → Stopped` (`Error` on failure). Aggregate root: `CopyProfile` (owns `CopyDestination`).
- **Destination** — one slave account + full rule set for how master copied onto it. All config per-destination, so one master feeds conservative + aggressive slaves at once.
- **Copy engine host** — running worker for profile (`CopyEngineHost`). Subscribes master execution stream, applies each event to every destination.
- **Supervisor** — `CopyEngineSupervisor`, background service on each node. Hosts assigned profiles, self-heals across cluster (see [scaling](../deployment/scaling.md)).

## What gets mirrored

| Master event | Slave action |
|--------------|--------------|
| Market / market-range position open | Open a sized copy (labelled with the source position id) |
| Limit / stop / stop-limit pending order | Place the matching pending order |
| Pending order amend | Amend the mirrored pending order in place |
| Pending order cancel / expiry | Cancel the mirrored pending order |
| Partial close | Close the same proportion of the slave position |
| Scale-in (volume increase) | Open the added volume (opt-in) |
| Stop-loss / trailing-stop change | Amend the slave position's protection |
| Full close | Close the slave copy |

Every copy **labelled with source position/order id**. After reconnect host rebuilds state from reconcile: opens copies master holds but slave missing, closes slave "orphans" master no longer holds — **without duplicating trades**.

## Creating a profile

**New Profile** dialog on Copy Trading page collects all up front: profile name, source (master) account, destination (slave) accounts (multi-select with **Select all** button; chosen master excluded from slave list), + full per-destination option set below. All inputs **validated before saving** — missing name/source/destination, non-positive sizing param, negative/inconsistent lot bounds, out-of-range drawdown %, no order type enabled, empty symbol filter, or malformed symbol-map pairs surface as error list + block save. On confirm, profile created + every selected slave added with chosen settings.

Row actions respect lifecycle: **Start** enabled only when not running, **Stop** + **Pause** only when running, **Delete** disabled while running + asks confirmation before removing profile + destinations.

## Per-destination options

Set in New Profile dialog, on Copy Trading page's per-destination panel, or via `POST /api/copy/profiles/{id}/destinations`:

- **Sizing** (`MoneyManagementMode` + parameter): fixed lot, lot/notional multiplier, proportional balance/equity/free-margin, fixed risk %, fixed leverage, auto-proportional, **risk-%-from-stop** (M7). Plus min/max lot bounds + force-min-lot. **Risk-from-stop** sizes destination so it risks configured percent of *its own* balance, derived from **master's stop-loss distance** (`master risks 2% → slave auto-risks 2%`): `lots = balance×% ÷ (stopDistance ×
  contractSize)`. Master open **without** stop-loss has no distance to size against → uses configured **max-risk fallback lot** (M7) if set, else skipped (`no_stop_loss`) not guessed. Proportional-**equity**/**free-margin** size off real account **equity** (`balance + Σ floating P&L`, derived per cTrader Open API which doesn't deliver equity), not plain balance — so master sitting on open profit/loss sizes copies right. Used margin not exposed by reconcile API, so free-margin treated as equity (honest available-funds proxy); other modes read balance + skip extra revaluation round-trip.
- **Direction filter**: both / long-only / short-only. **Reverse**: flip side (+ swap SL↔TP) for contrarian copy.
- **Manage-only** (Ignore-New-Trades / Close-Only): mirror closes, partial closes + protection changes on already-copied positions, but open **no** new positions/pending orders (skipped `manage_only`). Use to wind destination down without cutting existing copies.
- **Sync-Open-on-start** / **Sync-Closed-on-start** (default on): on profile's **first** resync, whether to open copies for master's pre-existing positions, + whether to close copies master closed while profile stopped. Both apply only at start — mid-run reconnect always reconciles fully so desync recovers regardless.
- **Symbol map** + **symbol filter** (whitelist / blacklist). Each symbol-map entry carries optional **per-symbol volume multiplier** (cMAM per-symbol override) scaling copy size for that symbol on top of destination's sizing (1 = no change). Whole map imports/exports as **CSV** (`GET …/symbol-map.csv`, `PUT …/symbol-map/csv`; columns `Source,Destination,VolumeMultiplier`) — each row validated through domain value objects, so malformed file can't produce invalid map.
- **Trading-hours window** (C18) — per-destination daily UTC window (`start`/`end` minutes-of-day, end exclusive; `start == end` = all-day). New opens outside window skipped (`trading_hours`); window with `start > end` wraps past midnight (e.g. 22:00–06:00). Existing positions stay managed.
- **Source-label filter** (C18, cTrader equivalent of MT magic-number filter) — when set, copy only master trades whose label matches **exactly** (e.g. one bot's trades, or manual-only label); else skipped (`source_label`). Empty = copy all. Carried on `ExecutionEvent.SourceLabel` from master position/order's `TradeData.Label`, honored on resync too.
- **Account protection** (ZuluGuard / Global Account Protection) — watch destination's **live equity** (`balance + Σ floating P&L`, polled every `CopyDefaults.EquityGuardInterval`) against `StopEquity` floor and/or optional `TakeEquity` ceiling. On breach, apply mode: **CloseOnly** (stop new copies, keep managing existing), **Frozen** (stop opening), **SellOut** (close **every** copy on destination immediately). Once fired, destination latched — no new opens until host restarts — + `CopyAccountProtectionTriggered` alert raised. `SellOut` requires `StopEquity`; `TakeEquity` must sit above `StopEquity`. **No-guarantee caveat:** sell-out uses market execution — like every competitor's equivalent, can't guarantee fill price in fast/gapped market.
- **Flatten-All panic button** (C8) — `POST /api/copy/profiles/{id}/flatten` immediately closes **every** copied position on every destination + locks against new opens. Routed cross-process: API sets flag, supervisor delivers to running host (reusing token-rotation channel), which flattens in place; flag cleared so fires exactly once (`CopyFlattenAll` alert). User then pauses/stops profile.
- **Prop-firm rule guard** (C7) — enforcement prop-firm copier users ask for. Per destination, **daily-loss cap** (loss from day's opening equity) and/or **trailing-drawdown** limit (loss from running peak equity), both in deposit currency. On breach destination **auto-flattened** (every copy closed) + **locked out** rest of UTC day (new opens skipped `prop_lockout`); `CopyPropRuleBreached` alert fires. Lockout clears when UTC day rolls over (fresh baseline/peak taken). Shares same live-equity poll as account protection.
- **Execution jitter** (C11, off by default) — random `0..N` ms delay before placing each copy, to de-correlate near-identical order timestamps across user's **own** accounts. **Compliance caveat:** aid for prop firms that *permit* copying — **not** tool to evade firm that forbids it; staying within your firm's rules is your responsibility.
- **Config lock** (C9) — freeze destination's settings for period (`POST …/destinations/{id}/lock` with minutes). While locked, destination can't be removed (aggregate rejects with `CopyDestinationConfigLocked`) — deliberate guard against impulsive changes during drawdown. Lock expires automatically at its timestamp.
- **Consistency pre-alert** (C10) — warn (once per UTC day) when destination's **daily profit** reaches configured percent of day's opening equity (`CopyConsistencyThresholdApproaching`), so prop-firm consistency rule respected *before* it trips. Profit-side, independent of loss-side lockout; runs off same day baseline as prop-rule guard.
- **Order-type filter** — choose exactly which master order types to copy: market, market-range, limit, stop, stop-limit (`CopyOrderTypes` flags; default all). cMAM-style selectivity.
- **Copy SL / Copy TP** — mirror master's stop-loss / take-profit, or manage protection independently.
- **Copy trailing stop**, **mirror partial close**, **mirror scale-in** — each independently toggleable.
- **Copy pending expiry** (default on) — mirror master pending order's Good-Till-Date expiry timestamp.
- **Copy master slippage** (default on) — for market-range + stop-limit orders, place slave order with master's exact slippage-in-points (base price taken from slave's live spot).
- **Guards**: max drawdown %, daily loss cap, max copy delay, slippage filter (skip copy if slave price moved beyond N pips from master entry). **Max copy delay** measured against master event's real server timestamp (`ExecutionEvent.ServerTimestamp`) via injected `TimeProvider`: signal older than configured max-lag skipped, so stale copy never placed late (previously delay always zero + guard dead).
- **SL/TP precision normalization** (M6) — copied stop-loss/take-profit prices rounded to **destination** symbol's digit precision before amend, so master price at finer precision (or cross-broker digit mismatch) never trips server's `INVALID_STOPLOSS_TAKEPROFIT`.
- **Rejection circuit breaker / Follower Guard** (G8) — destination rejecting `CopyDefaults.RejectionBudget` opens in a row is **tripped**: no new opens for cooldown window (`CopyDestinationTripped` alert fires), stopping rejection storm from hammering (prop-firm) account. Existing positions still managed + closed while tripped; breaker auto-resets after cooldown + successful copy clears counter.
- **Lot sanity ceiling** (C14) — absolute max copy size and/or multiple-of-master cap. Computed copy exceeding absolute cap, or exceeding `N×` master's own lot size, **hard-blocked** (surfaced as `lot_sanity` skip, counted on `cmind.copy.skipped`) not placed — defends against catastrophic-oversize class (0.23-lot master turning into 3 lots on each receiver via runaway multiplier or rounding bug). Both dimensions default `0` (off).

## Reliability & edge cases

Engine built for reality that anything can fail anytime:

- **Slave-pending fill-correlation timeout** (C13) — mirrored slave pending whose master pending vanished (neither resting nor freshly filled) cancelled after correlation timeout, so slave copy can't fill uncorrelated into unmanaged position (`CopyPendingTimedOut`). Resync also cleans order-id-labelled filled-pending orphan.
- **Robust close/flatten** (M8) — closing orphan on resync, or flattening on guard breach, tolerates position broker already closed (`POSITION_NOT_FOUND`): each close runs independently, so one stale id never aborts resync or leaves rest of account un-flattened.

- **Start with master already in trades** — on start host reconciles + opens copies for master's existing positions.
- **Connection drops / desync** — on reconnect host reconciles: opens missing copies, closes orphans, re-labels pendings. No duplicate orders.
- **Order placement failure** — failure on one destination logged, never blocks other destinations.
- **Single valid token per cID** — cTrader invalidates cID's old access token moment new one issued. cMind swaps running host's token **in place** (re-auth on live socket) so copying continues without dropping stream. See [token lifecycle](token-lifecycle.md).

## Auditability

Every action emits structured, source-generated log event (`LogMessages`) with profile id, destination cID, order/position ids, + values — order placed/skipped (with reason), partial close, protection applied, trailing applied, pending placed/amended/cancelled, expiry mirrored, market-range slippage mirrored, token swapped, resync summary. This is the audit trail for compliance + dispute resolution.

Alongside logs, engine emits **OpenTelemetry metrics** on `cMind.Copy` meter (registered in shared OTel pipeline, exported over OTLP / to Azure Monitor like rest): `cmind.copy.latency` (master-event → dispatch, ms), `cmind.copy.dispatch.duration` (fan-out to all destinations, ms), `cmind.copy.slippage.points`, `cmind.copy.placed` (tagged by destination), `cmind.copy.skipped` (tagged by reason), + `cmind.copy.failed`. These make latency/slippage regression measurable, not just visible in log line — live suite asserts them against budget.

## API

- `GET /api/copy/profiles` — list.
- `POST /api/copy/profiles` — create (with optional destination account ids).
- `GET /api/copy/profiles/{id}` — full detail incl. every destination option.
- `POST /api/copy/profiles/{id}/destinations` — add a destination with the full option set.
- `DELETE /api/copy/profiles/{id}/destinations/{destinationId}` — remove.
- `POST /api/copy/profiles/{id}/{start|pause|stop}` — lifecycle.

## Tests

- **Unit** (`tests/UnitTests/CopyTrading`) — sizing modes, decision filters, order-type filter, expiry copy, market-range/stop-limit slippage, SL/TP toggles, partial close, pending amend/cancel, start-with-open, disconnect→desync→resync, in-place token swap, cross-cID invalidation. Runs against `FakeTradingSession`, cTrader-faithful in-memory simulator.
- **Integration** (`tests/IntegrationTests/CopyLive`) — node-affinity/lease claim, token-version propagation on real Postgres.
- **E2E** (`tests/E2ETests`) — destination-option round-trip through API + UI, full lifecycle.
- **Stress / DST** (`tests/StressTests`) — deterministic-simulation testing: seeded randomized workloads + fault injection (socket flap, order rejection, market-range rejection, token rotation, node death) drive `CopyEngineHost` to quiescence + assert convergence invariants. See [testing/stress-testing.md](../testing/stress-testing.md). This suite surfaced + fixed real startup race: `OnReconnected` wired before initial reference-load + resync, so socket flap during startup could run second resync concurrently + corrupt host's non-concurrent state dictionaries — startup load + first resync now run under `_stateGate`.
- **Live** — real cTrader demo accounts; see [testing/live-copy-trading.md](../testing/live-copy-trading.md).

See [dev-credentials.md](../testing/dev-credentials.md) for single credentials file live + E2E tiers read.
<!-- [ZH-HANS] Translation needed -->
