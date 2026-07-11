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

## Per-destination options

Set on the Copy Trading page (or `POST /api/copy/profiles/{id}/destinations`):

- **Sizing** (`MoneyManagementMode` + parameter): fixed lot, lot/notional multiplier,
  proportional balance/equity/free-margin, fixed risk %, fixed leverage, auto-proportional.
  Plus min/max lot bounds and force-min-lot.
- **Direction filter**: both / long-only / short-only. **Reverse**: flip the side (and swap
  SL↔TP) to run a contrarian copy.
- **Symbol map** and **symbol filter** (whitelist / blacklist).
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
  the slave price has moved beyond N pips from the master entry).

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
