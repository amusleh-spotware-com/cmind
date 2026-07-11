# Copy trading

Mirror a **master** cTrader account onto one or more **slave** accounts — across brokers and
across cTrader IDs (cIDs) — with per-destination control and money-grade reconciliation.

## Concepts

- **Copy profile** — one master (`SourceAccountId`) plus one or more **destinations**. Has a
  lifecycle: `Draft → Running → Paused → Stopped` (`Error` on failure). Aggregate root:
  `CopyProfile` (owns `CopyDestination`).
- **Destination** — one slave account and the full rule set for how the master is copied onto
  it. All configuration is per-destination, so one master can feed conservative and aggressive
  slaves at once.
- **Copy engine host** — the running worker for a profile (`CopyEngineHost`). It subscribes to
  the master's execution stream and applies each event to every destination.
- **Supervisor** — `CopyEngineSupervisor`, a background service on each node that hosts the
  profiles assigned to it and self-heals across a cluster (see
  [scaling](../deployment/scaling.md)).

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

Every copy is **labelled with the source position/order id**, so after any reconnect the host
rebuilds state from a reconcile: it opens copies the master holds but the slave is missing, and
closes slave "orphans" the master no longer holds — **without duplicating trades**.

## Creating a profile

The **New Profile** dialog on the Copy Trading page collects everything up front: profile name, the
source (master) account, the destination (slave) accounts (multi-select with a **Select all** button;
the chosen master is excluded from the slave list), and the full per-destination option set below.
All inputs are **validated before saving** — missing name/source/destination, non-positive sizing
parameter, negative/inconsistent lot bounds, out-of-range drawdown %, no order type enabled, an empty
symbol filter, or malformed symbol-map pairs are surfaced as an error list and block the save. On
confirm the profile is created and every selected slave is added with the chosen settings.

Row actions respect the lifecycle: **Start** is enabled only when the profile is not running, **Stop**
and **Pause** only when it is running, and **Delete** is disabled while running and asks for
confirmation before removing the profile and its destinations.

## Per-destination options

Set in the New Profile dialog, on the Copy Trading page's per-destination panel, or via
`POST /api/copy/profiles/{id}/destinations`:

- **Sizing** (`MoneyManagementMode` + parameter): fixed lot, lot/notional multiplier,
  proportional balance/equity/free-margin, fixed risk %, fixed leverage, auto-proportional,
  **risk-%-from-stop** (M7). Plus min/max lot bounds and force-min-lot. **Risk-from-stop** sizes the
  destination so it risks the configured percent of *its own* balance derived from the **master's
  stop-loss distance** (`master risks 2% → slave auto-risks 2%`): `lots = balance×% ÷ (stopDistance ×
  contractSize)`. A master open **without** a stop-loss has no distance to size against, so it is
  skipped (`no_stop_loss`) rather than guessed — configure a different mode for unstopped masters. Proportional-**equity**/**free-margin** size off real
  account **equity** (`balance + Σ floating P&L`, derived per the cTrader Open API which doesn't deliver
  equity), not plain balance — so a master sitting on open profit/loss sizes copies correctly. Used
  margin isn't exposed by the reconcile API, so free-margin is treated as equity (an honest available-funds
  proxy); the other modes read balance and skip the extra revaluation round-trip.
- **Direction filter**: both / long-only / short-only. **Reverse**: flip the side (and swap
  SL↔TP) to run a contrarian copy.
- **Manage-only** (Ignore-New-Trades / Close-Only): mirror closes, partial closes and protection
  changes on positions already copied, but open **no** new positions or pending orders (skipped as
  `manage_only`). Use to wind a destination down without cutting existing copies.
- **Sync-Open-on-start** / **Sync-Closed-on-start** (default on): on the profile's **first** resync,
  whether to open copies for the master's pre-existing positions, and whether to close copies the master
  closed while the profile was stopped. Both apply only at start — a mid-run reconnect always reconciles
  fully so a desync recovers regardless.
- **Symbol map** and **symbol filter** (whitelist / blacklist).
- **Trading-hours window** (C18) — a per-destination daily UTC window (`start`/`end` minutes-of-day,
  end exclusive; `start == end` = all-day). New opens outside the window are skipped (`trading_hours`);
  a window with `start > end` wraps past midnight (e.g. 22:00–06:00). Existing positions stay managed.
- **Source-label filter** (C18, cTrader equivalent of an MT magic-number filter) — when set, copy only
  master trades whose label matches **exactly** (e.g. copy one bot's trades, or a manual-only label);
  everything else is skipped (`source_label`). Empty = copy all. Carried on `ExecutionEvent.SourceLabel`
  from the master position/order's `TradeData.Label`, and honored on resync too.
- **Account protection** (ZuluGuard / Global Account Protection) — watch the destination's **live
  equity** (`balance + Σ floating P&L`, polled every `CopyDefaults.EquityGuardInterval`) against a
  `StopEquity` floor and/or optional `TakeEquity` ceiling. On breach, apply the mode: **CloseOnly**
  (stop opening new copies, keep managing existing), **Frozen** (stop opening), **SellOut** (close
  **every** copy on the destination immediately). Once fired the destination is latched — no new opens
  until the host restarts — and a `CopyAccountProtectionTriggered` alert is raised. `SellOut` requires a
  `StopEquity`; a `TakeEquity` must sit above the `StopEquity`. **No-guarantee caveat:** sell-out uses
  market execution — like every competitor's equivalent it can't guarantee a fill price in a fast or
  gapped market.
- **Prop-firm rule guard** (C7) — the enforcement prop-firm copier users ask for. Per destination, a
  **daily-loss cap** (loss from the day's opening equity) and/or a **trailing-drawdown** limit (loss from
  the running peak equity), both in the deposit currency. On breach the destination is **auto-flattened**
  (every copy closed) and **locked out** for the rest of the UTC day (new opens skipped as `prop_lockout`);
  a `CopyPropRuleBreached` alert fires. The lockout clears when the UTC day rolls over (a fresh
  baseline/peak is taken). Shares the same live-equity poll as account protection.
- **Config lock** (C9) — freeze a destination's settings for a period (`POST …/destinations/{id}/lock`
  with minutes). While locked, the destination cannot be removed (the aggregate rejects it with
  `CopyDestinationConfigLocked`) — a deliberate guard against impulsive changes during a drawdown. The
  lock expires automatically at its timestamp.
- **Consistency pre-alert** (C10) — warn (once per UTC day) when a destination's **daily profit** reaches
  a configured percent of the day's opening equity (`CopyConsistencyThresholdApproaching`), so a
  prop-firm consistency rule can be respected *before* it trips. Profit-side, independent of the loss-side
  lockout; runs off the same day baseline as the prop-rule guard.
- **Order-type filter** — choose exactly which master order types to copy: market, market-range,
  limit, stop, stop-limit (`CopyOrderTypes` flags; default all). cMAM-style selectivity.
- **Copy SL / Copy TP** — mirror the master's stop-loss / take-profit, or manage protection
  independently.
- **Copy trailing stop**, **mirror partial close**, **mirror scale-in** — each independently
  toggleable.
- **Copy pending expiry** (default on) — mirror the master pending order's Good-Till-Date
  expiry timestamp.
- **Copy master slippage** (default on) — for market-range and stop-limit orders, place the
  slave order with the master's exact slippage-in-points (base price taken from the slave's
  live spot).
- **Guards**: max drawdown %, daily loss cap, max copy delay, slippage filter (skip a copy if
  the slave price has moved beyond N pips from the master entry). **Max copy delay** is measured
  against the master event's real server timestamp (`ExecutionEvent.ServerTimestamp`) via the injected
  `TimeProvider`: a signal older than the configured max-lag is skipped, so a stale copy is never
  placed late (previously the delay was always zero and the guard was dead).
- **SL/TP precision normalization** (M6) — copied stop-loss/take-profit prices are rounded to the
  **destination** symbol's digit precision before the amend, so a master price at finer precision (or a
  cross-broker digit mismatch) never trips the server's `INVALID_STOPLOSS_TAKEPROFIT`.
- **Rejection circuit breaker / Follower Guard** (G8) — a destination that rejects
  `CopyDefaults.RejectionBudget` opens in a row is **tripped**: it receives no new opens for a cooldown
  window (a `CopyDestinationTripped` alert fires), stopping a rejection storm from hammering a
  (prop-firm) account. Existing positions are still managed and closed while tripped; the breaker
  auto-resets after the cooldown and a successful copy clears the counter.
- **Lot sanity ceiling** (C14) — an absolute maximum copy size and/or a multiple-of-master cap. A
  computed copy that exceeds the absolute cap, or exceeds `N×` the master's own lot size, is
  **hard-blocked** (surfaced as a `lot_sanity` skip, counted on `cmind.copy.skipped`) rather than
  placed — defending against the catastrophic-oversize class (a 0.23-lot master turning into 3 lots on
  each receiver through a runaway multiplier or rounding bug). Both dimensions default to `0` (off).

## Reliability & edge cases

The engine is built for the reality that anything can fail at any time:

- **Start with the master already in trades** — on start the host reconciles and opens copies
  for the master's existing positions.
- **Connection drops / desync** — on reconnect the host reconciles: opens missing copies, closes
  orphans, re-labels pendings. No duplicate orders.
- **Order placement failure** — a failure on one destination is logged and never blocks the
  other destinations.
- **Single valid token per cID** — cTrader invalidates a cID's old access token the moment a new
  one is issued. cMind swaps the running host's token **in place** (re-auth on the live socket)
  so copying continues without dropping the stream. See
  [token lifecycle](token-lifecycle.md).

## Auditability

Every action emits a structured, source-generated log event (`LogMessages`) with the profile id,
destination cID, order/position ids, and values — order placed/skipped (with reason), partial
close, protection applied, trailing applied, pending placed/amended/cancelled, expiry mirrored,
market-range slippage mirrored, token swapped, resync summary. This is the audit trail for
compliance and dispute resolution.

Alongside the logs the engine emits **OpenTelemetry metrics** on the `cMind.Copy` meter (registered in
the shared OTel pipeline, exported over OTLP / to Azure Monitor like the rest): `cmind.copy.latency`
(master-event → dispatch, ms), `cmind.copy.dispatch.duration` (fan-out to all destinations, ms),
`cmind.copy.slippage.points`, `cmind.copy.placed` (tagged by destination), `cmind.copy.skipped` (tagged
by reason), and `cmind.copy.failed`. These make a latency/slippage regression measurable, not just
visible in a log line — the live suite asserts them against a budget.

## API

- `GET /api/copy/profiles` — list.
- `POST /api/copy/profiles` — create (with optional destination account ids).
- `GET /api/copy/profiles/{id}` — full detail incl. every destination option.
- `POST /api/copy/profiles/{id}/destinations` — add a destination with the full option set.
- `DELETE /api/copy/profiles/{id}/destinations/{destinationId}` — remove.
- `POST /api/copy/profiles/{id}/{start|pause|stop}` — lifecycle.

## Tests

- **Unit** (`tests/UnitTests/CopyTrading`) — sizing modes, decision filters, order-type filter,
  expiry copy, market-range/stop-limit slippage, SL/TP toggles, partial close, pending
  amend/cancel, start-with-open, disconnect→desync→resync, in-place token swap, cross-cID
  invalidation. Runs against `FakeTradingSession`, a cTrader-faithful in-memory simulator.
- **Integration** (`tests/IntegrationTests/CopyLive`) — node-affinity/lease claim, token-version
  propagation on real Postgres.
- **E2E** (`tests/E2ETests`) — destination-option round-trip through the API + UI, full lifecycle.
- **Stress / DST** (`tests/StressTests`) — deterministic-simulation testing: seeded randomized
  workloads + fault injection (socket flap, order rejection, market-range rejection, token
  rotation, node death) drive `CopyEngineHost` to quiescence and assert convergence invariants.
  See [testing/stress-testing.md](../testing/stress-testing.md). This suite surfaced and fixed a
  real startup race: `OnReconnected` was wired before the initial reference-load + resync, so a
  socket flap during startup could run a second resync concurrently and corrupt the host's
  non-concurrent state dictionaries — the startup load + first resync now run under `_stateGate`.
- **Live** — real cTrader demo accounts; see [testing/live-copy-trading.md](../testing/live-copy-trading.md).

See [dev-credentials.md](../testing/dev-credentials.md) for the single credentials file the live
and E2E tiers read.
