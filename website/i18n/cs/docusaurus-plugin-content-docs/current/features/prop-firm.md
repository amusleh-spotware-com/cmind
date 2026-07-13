---
description: "Retail prop firms (FTMO-style) prodávají evaluation účty: trader musí zasáhnout profit target while staying inside risk limits (max denní ztráta, max celková/trailing drawdown, konzistence, časové limity) před funded. cMind umožňuje uživateli vytvořit custom challenge jakéhokoliv průmyslového tvaru, navázat na TradingAccount, run like copy-trading operation — started/stopped, hosted on node, tracked live over cTrader Open API."
---

# Prop-firm challenge simulace

Retail prop firms (FTMO-style) prodávají **evaluation účty**: trader musí zasáhnout profit target while
staying inside risk limits (max denní ztráta, max celková/trailing drawdown, konzistence, časové limity) before
funded. cMind umožňuje uživateli vytvořit **custom challenge jakéhokoliv průmyslového tvaru**, navázat na
`TradingAccount`, **run like copy-trading operation** — started/stopped, hosted on node,
tracked **live over cTrader Open API**. Aggregate vyhodnocuje každé pravidlo deterministicky; on
pass nebo breach, ends challenge, marks it, alerts user.

## Domain (bounded context: PropFirm)

`PropFirmChallenge` = aggregate root (modul `Core.PropFirm`), references its `TradingAccount` by
strong id only (no cross-aggregate FK). Owns rule evaluation, phase/state machine, node
lease.

### Value objects & rule set

- **`Money`** (non-negative), **`MoneyAmount`** (signed), **`Percent`** (0–100], **`TradingDayRequirement`** (0–365).
- **`EquitySnapshot`** `(equity, balance)` — reading fed to aggregate.
- **`ActivitySnapshot`** `(openPositions, openedInNewsWindow, holdingOverWeekend)` — non-equity facts.
- **`DailyLossLimit`** `(percent, basis)` — basis `Equity` (intraday, includes floating P&L) nebo `Balance`
  (realized only).
- **`DrawdownLimit`** — `Static` (from starting balance), `TrailingPercent` (from peak equity), nebo
  `TrailingThresholdDollar` (trails equity peak by fixed dollar amount, then **locks at starting
  balance** once equity reaches threshold — futures-style).
- **`ConsistencyRule`** `(maxSingleDayShareOfProfit)` — blocks pass while one day dominates total profit.
- **`ChallengeRules`** nese výše uvedené plus `MaxCalendarDays`, `MaxInactivityDays`, `MaxOpenPositions`,
  `AllowWeekendHolding`, `AllowNewsTrading`, `Kind`, `SingleStep`. Rule maths žijí na VOs
  (`DrawdownLimit.IsBreached`, `DailyLossLimit.IsBreached`, `ConsistencyRule.IsSatisfied`); aggregate
  orchestrates.

### Challenge kinds & templates

`ChallengeTemplates.For(kind)` builds valid preset for `OnePhase`, `TwoPhase`, `ThreePhase`,
`InstantFunding`, nebo `Custom` (full control). UI pre-fills template; user may adjust any field.

### Fáze & status

- **Fáze:** `Evaluation → Verification → Funded` (single-step skips Verification).
- **Status:** `Active`, `Passed`, `Failed`, plus lifecycle `Stopped` (tracking paused) — `Create` starts
  challenge `Active`; `Stop()`/`Resume()` toggle `Active↔Stopped`.
- **`BreachReason`:** `DailyLoss`, `MaxDrawdown`, `Consistency`, `TimeLimit`, `Inactivity`,
  `WeekendHolding`, `NewsTrading`, `MaxExposure`.

### Rule evaluation

- **`RecordEquity(EquitySnapshot, now)`** — rolls trading day at day boundaries (captures previous
  day's profit for consistency rule), updates peak/daily peaks, then **fails on first breach**
  (denní ztráta → drawdown → časový limit → neaktivita, in order) or advances phase when profit target,
  minimum-trading-day, consistency requirements all met. Out-of-order snapshots and records on
  terminal challenge throw `DomainException`.
- **`RecordActivity(ActivitySnapshot, now)`** — evaluates behaviour rules (max open positions, weekend
  holding, news trading), stamps activity for inactivity rule.
- Soft **`PropFirmDrawdownWarning`** fires once when equity usage crosses configurable threshold.

Domain events: `PropFirmChallengeStarted`, `PropFirmChallengeStopped`, `PropFirmPhasePassed`,
`PropFirmChallengePassed`, `PropFirmChallengeBreached`, `PropFirmDrawdownWarning`.

## Live tracking (Execution) — node-hosted, samo-hojící

Tracking zrcadlí copy-trading hosting stack exactly; prop tracker = **read-only** cousin of
copy engine.

- **`PropFirmTrackingSupervisor`** (`src/Nodes/PropFirm`) — `BackgroundService` na každém nodu, gated on
  `App:PropFirm:Enabled`. Each cycle **claims** active challenges on samo-hojící lease
  (`AssignedNode` + `LeaseExpiresAt`; dead node's challenges reclaimed once lease lapses —
  same atomic `ExecuteUpdate` claim as copy trading, takže two nodes never double-track), renews leases,
  pushes rotated tokens in place, stops hosts whose challenge left `Active`.
- **`PropFirmTrackingHost`** (`src/Nodes/PropFirm`) — one per challenge. Otevře `IOpenApiTradingSession`
  for account and, on `App:PropFirm:EquityPollInterval`, recomputes live equity, feeds to
  aggregate. Swaps access token in place on rotation (no session drop). Exits when challenge
  no longer `Active`.
- **`PropFirmEquityCalculator`** (`src/CTraderOpenApi/Client`) — cTrader-faithful equity maths.
  Equity **not** delivered by Open API, takže derived: `equity = balance + Σ(unrealized P&L)`,
  where each position's P&L is `priceDifference × units × quote→deposit rate + swap + commission`
  (`units = wire volume / 100`; long revalues at bid, short at ask). Balance from
  `ProtoOATrader`; positions (entry price, swap, commission) from reconcile; live bid/ask from spot
  subscriptions. Pure and isolated — currency-conversion hot spot unit-tested on its own.

## Alerts

`PropFirmAlertNotifier` (`src/Infrastructure/PropFirm`) subscribes to pass/breach/warning domain events
(registered as `IDomainEventHandler<>`, dispatched after successful `SaveChanges`), notifies user
through structured alert/audit trail (`LogMessages`). Live UI reflects same status change. This
= cross-context reaction — never mutates challenge aggregate.

## API (`/api/prop-firm`, feature `PropFirm`, role User+)

| Method | Route | Účel |
|--------|-------|---------|
| GET | `/challenges` | list user's challenges (kind, phase, status, live equity, lease) |
| GET | `/challenges/{id}` | one challenge |
| GET | `/templates` | industry presets for create dialog |
| POST | `/challenges` | create from template **or** fully custom rule set |
| POST | `/challenges/{id}/start` | resume tracking (Stopped → Active) |
| POST | `/challenges/{id}/stop` | stop tracking (Active → Stopped, release lease) |
| POST | `/challenges/{id}/equity` | record equity snapshot → re-evaluate (manual/no-live-feed path) |
| DELETE | `/challenges/{id}` | soft-delete (blocked while Active) |

MCP: `Mcp/Tools/PropFirmTools.cs` exposes list/create(from template)/record-equity/start/stop, gated on
`PropFirm` feature.

UI: `/prop-firm` (nav *Prop Firm*, gated by `PropFirm` flag) lists challenges with **Start/Stop/Delete**
row actions (Start when Stopped, Stop when Active, Delete disabled while Active), creates them through
`NewPropFirmChallengeDialog` (template picker + full rule editor). Všechny create/edit přes MudBlazor dialog.

## Live equity feed — vyřešeno

Dřívější "no live account P&L feed" gap uzavřen: když `App:PropFirm:Enabled` set, nodes track
účet live over Open API, feed equity automatically. Bez něj (default), domain and
**manual-equity** path (`POST …/equity`) run unchanged — žádné cTrader credentials needed for build/test/E2E.

## Testy

- **Unit** — `UnitTests/PropFirm/`: `PropFirmChallengeTests` (phase advance, min-days, static/trailing
  drawdown, daily loss, terminal/out-of-order guards); `PropFirmChallengeRulesTests` (balance vs equity
  daily-loss basis, trailing-threshold-dollar trail+lock, consistency block/allow, time-limit, inactivity,
  max-exposure, weekend, news, stop/resume, lease boundary, pass releases lease, drawdown warning);
  `PropFirmValueObjectTests` (VO ranges + rule-VO maths); `PropFirmEquityCalculatorTests` (long/short P&L,
  swap/commission, quote→deposit conversion, missing pricing); `PropFirmTrackingHostTests` (live equity
  drives pass/fail against extended fake session); `PropFirmAlertNotifierTests`. Time explicit /
  `FakeTimeProvider` — žádné wall-clock reads.
- **Integration** — `IntegrationTests/`: `PropFirmChallengePersistenceTests` (round-trip + record-equity +
  soft-delete, enriched-rules + lease round-trip) and `PropFirmTrackingLeaseTests` (claim, contested lease,
  reclaim after lapse across two node identities) on real Postgres.
- **E2E** — `E2ETests/PropFirmTests.cs`: create + record-equity to `Passed`; stop→start→breach flow;
  templates endpoint.
- **Stress / DST** — `StressTests/PropFirm/PropFirmChallengeDstTests.cs`: seeded randomized equity/activity
  streams (day rolls, spikes, crashes, duplicate + out-of-order snapshots, exposure/weekend/news) across
  many mixed-rule challenges, asserting sticky exactly-once terminal states, peak-bounds-current invariant,
  reasoned failures.

## Konfigurace (`App:PropFirm`)

`Enabled` (off by default), `ReconcileInterval`, `EquityPollInterval`, `LeaseTtl`,
`DrawdownWarnThresholdPercent`, `NodeName`.
