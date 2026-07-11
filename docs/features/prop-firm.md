# Prop-firm challenge simulation

Retail prop firms (FTMO-style) sell **evaluation accounts**: a trader must hit a profit target while
staying inside risk limits (max daily loss, max total/trailing drawdown, consistency, time limits) before
being funded. cMind lets a user create a **custom challenge of any industry shape**, bind it to a
`TradingAccount`, and **run it like a copy-trading operation** — started/stopped, hosted on a node, and
tracked **live over the cTrader Open API**. The aggregate evaluates every rule deterministically and, on a
pass or a breach, ends the challenge, marks it, and alerts the user.

## Domain (bounded context: PropFirm)

`PropFirmChallenge` is the aggregate root (module `Core.PropFirm`), referencing its `TradingAccount` by
strong id only (no cross-aggregate FK). It owns rule evaluation, the phase/state machine, and the node
lease.

### Value objects & rule set

- **`Money`** (non-negative), **`MoneyAmount`** (signed), **`Percent`** (0–100], **`TradingDayRequirement`** (0–365).
- **`EquitySnapshot`** `(equity, balance)` — the reading fed to the aggregate.
- **`ActivitySnapshot`** `(openPositions, openedInNewsWindow, holdingOverWeekend)` — non-equity facts.
- **`DailyLossLimit`** `(percent, basis)` — basis `Equity` (intraday, includes floating P&L) or `Balance`
  (realized only).
- **`DrawdownLimit`** — `Static` (from starting balance), `TrailingPercent` (from peak equity), or
  `TrailingThresholdDollar` (trails the equity peak by a fixed dollar amount, then **locks at the starting
  balance** once equity reaches a threshold — futures-style).
- **`ConsistencyRule`** `(maxSingleDayShareOfProfit)` — blocks a pass while one day dominates total profit.
- **`ChallengeRules`** carries the above plus `MaxCalendarDays`, `MaxInactivityDays`, `MaxOpenPositions`,
  `AllowWeekendHolding`, `AllowNewsTrading`, `Kind`, and `SingleStep`. Rule maths live on the VOs
  (`DrawdownLimit.IsBreached`, `DailyLossLimit.IsBreached`, `ConsistencyRule.IsSatisfied`); the aggregate
  orchestrates.

### Challenge kinds & templates

`ChallengeTemplates.For(kind)` builds a valid preset for `OnePhase`, `TwoPhase`, `ThreePhase`,
`InstantFunding`, or `Custom` (full control). The UI pre-fills a template and the user may adjust any field.

### Phases & status

- **Phases:** `Evaluation → Verification → Funded` (single-step skips Verification).
- **Status:** `Active`, `Passed`, `Failed`, plus lifecycle `Stopped` (tracking paused) — `Create` starts a
  challenge `Active`; `Stop()`/`Resume()` toggle `Active↔Stopped`.
- **`BreachReason`:** `DailyLoss`, `MaxDrawdown`, `Consistency`, `TimeLimit`, `Inactivity`,
  `WeekendHolding`, `NewsTrading`, `MaxExposure`.

### Rule evaluation

- **`RecordEquity(EquitySnapshot, now)`** — rolls the trading day at day boundaries (capturing the previous
  day's profit for the consistency rule), updates peak/daily peaks, then **fails on the first breach**
  (daily loss → drawdown → time limit → inactivity, in order) or advances the phase when the profit target,
  minimum-trading-day, and consistency requirements are all met. Out-of-order snapshots and records on a
  terminal challenge throw a `DomainException`.
- **`RecordActivity(ActivitySnapshot, now)`** — evaluates the behaviour rules (max open positions, weekend
  holding, news trading) and stamps activity for the inactivity rule.
- A soft **`PropFirmDrawdownWarning`** fires once when equity usage crosses a configurable threshold.

Domain events: `PropFirmChallengeStarted`, `PropFirmChallengeStopped`, `PropFirmPhasePassed`,
`PropFirmChallengePassed`, `PropFirmChallengeBreached`, `PropFirmDrawdownWarning`.

## Live tracking (Execution) — node-hosted, self-healing

Tracking mirrors the copy-trading hosting stack exactly; a prop tracker is a **read-only** cousin of the
copy engine.

- **`PropFirmTrackingSupervisor`** (`src/Nodes/PropFirm`) — a `BackgroundService` on each node, gated on
  `App:PropFirm:Enabled`. Each cycle it **claims** active challenges on a self-healing lease
  (`AssignedNode` + `LeaseExpiresAt`; a dead node's challenges are reclaimed once the lease lapses — the
  same atomic `ExecuteUpdate` claim as copy trading, so two nodes never double-track), renews leases,
  pushes rotated tokens in place, and stops hosts whose challenge left `Active`.
- **`PropFirmTrackingHost`** (`src/Nodes/PropFirm`) — one per challenge. Opens an `IOpenApiTradingSession`
  for the account and, on `App:PropFirm:EquityPollInterval`, recomputes live equity and feeds it to the
  aggregate. Swaps the access token in place on rotation (no session drop). Exits when the challenge is
  no longer `Active`.
- **`PropFirmEquityCalculator`** (`src/CTraderOpenApi/Client`) — the cTrader-faithful equity maths.
  Equity is **not** delivered by the Open API, so it is derived: `equity = balance + Σ(unrealized P&L)`,
  where each position's P&L is `priceDifference × units × quote→deposit rate + swap + commission`
  (`units = wire volume / 100`; a long revalues at the bid, a short at the ask). Balance comes from
  `ProtoOATrader`; positions (entry price, swap, commission) from reconcile; live bid/ask from spot
  subscriptions. Pure and isolated — the currency-conversion hot spot is unit-tested on its own.

## Alerts

`PropFirmAlertNotifier` (`src/Infrastructure/PropFirm`) subscribes to the pass/breach/warning domain events
(registered as `IDomainEventHandler<>`, dispatched after a successful `SaveChanges`) and notifies the user
through the structured alert/audit trail (`LogMessages`). The live UI reflects the same status change. This
is a cross-context reaction — it never mutates the challenge aggregate.

## API (`/api/prop-firm`, feature `PropFirm`, role User+)

| Method | Route | Purpose |
|--------|-------|---------|
| GET | `/challenges` | list the user's challenges (kind, phase, status, live equity, lease) |
| GET | `/challenges/{id}` | one challenge |
| GET | `/templates` | industry presets for the create dialog |
| POST | `/challenges` | create from a template **or** a fully custom rule set |
| POST | `/challenges/{id}/start` | resume tracking (Stopped → Active) |
| POST | `/challenges/{id}/stop` | stop tracking (Active → Stopped, release lease) |
| POST | `/challenges/{id}/equity` | record an equity snapshot → re-evaluate (manual/no-live-feed path) |
| DELETE | `/challenges/{id}` | soft-delete (blocked while Active) |

MCP: `Mcp/Tools/PropFirmTools.cs` exposes list/create(from template)/record-equity/start/stop, gated on the
`PropFirm` feature.

UI: `/prop-firm` (nav *Prop Firm*, gated by the `PropFirm` flag) lists challenges with **Start/Stop/Delete**
row actions (Start when Stopped, Stop when Active, Delete disabled while Active) and creates them through the
`NewPropFirmChallengeDialog` (template picker + full rule editor). All create/edit via a MudBlazor dialog.

## Live equity feed — resolved

The earlier "no live account P&L feed" gap is closed: when `App:PropFirm:Enabled` is set, nodes track the
account live over the Open API and feed equity automatically. Without it (default), the domain and the
**manual-equity** path (`POST …/equity`) run unchanged — no cTrader credentials needed for build/test/E2E.

## Tests

- **Unit** — `UnitTests/PropFirm/`: `PropFirmChallengeTests` (phase advance, min-days, static/trailing
  drawdown, daily loss, terminal/out-of-order guards); `PropFirmChallengeRulesTests` (balance vs equity
  daily-loss basis, trailing-threshold-dollar trail+lock, consistency block/allow, time-limit, inactivity,
  max-exposure, weekend, news, stop/resume, lease boundary, pass releases lease, drawdown warning);
  `PropFirmValueObjectTests` (VO ranges + rule-VO maths); `PropFirmEquityCalculatorTests` (long/short P&L,
  swap/commission, quote→deposit conversion, missing pricing); `PropFirmTrackingHostTests` (live equity
  drives pass/fail against the extended fake session); `PropFirmAlertNotifierTests`. Time is explicit /
  `FakeTimeProvider` — no wall-clock reads.
- **Integration** — `IntegrationTests/`: `PropFirmChallengePersistenceTests` (round-trip + record-equity +
  soft-delete, enriched-rules + lease round-trip) and `PropFirmTrackingLeaseTests` (claim, contested lease,
  reclaim after lapse across two node identities) on real Postgres.
- **E2E** — `E2ETests/PropFirmTests.cs`: create + record-equity to `Passed`; stop→start→breach flow;
  templates endpoint.
- **Stress / DST** — `StressTests/PropFirm/PropFirmChallengeDstTests.cs`: seeded randomized equity/activity
  streams (day rolls, spikes, crashes, duplicate + out-of-order snapshots, exposure/weekend/news) across
  many mixed-rule challenges, asserting sticky exactly-once terminal states, peak-bounds-current invariant,
  and reasoned failures.

## Configuration (`App:PropFirm`)

`Enabled` (off by default), `ReconcileInterval`, `EquityPollInterval`, `LeaseTtl`,
`DrawdownWarnThresholdPercent`, `NodeName`.
