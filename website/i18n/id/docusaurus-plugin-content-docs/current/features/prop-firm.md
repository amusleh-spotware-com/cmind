---
description: "Prop firm retail (FTMO-style) menjual akun evaluasi: trader harus mencapai profit target sambil tetap dalam batas risiko (max daily loss, max…"
---

# Simulasi tantangan prop-firm

Prop firm retail (FTMO-style) menjual **akun evaluasi**: trader harus mencapai profit target sambil
tetap dalam batas risiko (max daily loss, max total/trailing drawdown, consistency, time limits) sebelum
funded. cMind memungkinkan pengguna membuat **custom challenge dari bentuk industri apa pun**, bind ke
`TradingAccount`, **jalankan seperti copy-trading operation** — started/stopped, hosted pada node,
tracked **live melalui cTrader Open API**. Aggregate mengevaluasi setiap rule secara deterministic; pada
pass atau breach, mengakhiri challenge, menandainya, alert pengguna.

## Domain (bounded context: PropFirm)

`PropFirmChallenge` = aggregate root (modul `Core.PropFirm`), mereferensikan `TradingAccount`-nya oleh
strong id saja (tidak ada cross-aggregate FK). Memiliki rule evaluation, phase/state machine, node
lease.

### Value objects & rule set

- **`Money`** (non-negative), **`MoneyAmount`** (signed), **`Percent`** (0–100], **`TradingDayRequirement`** (0–365).
- **`EquitySnapshot`** `(equity, balance)` — reading fed ke aggregate.
- **`ActivitySnapshot`** `(openPositions, openedInNewsWindow, holdingOverWeekend)` — non-equity facts.
- **`DailyLossLimit`** `(percent, basis)` — basis `Equity` (intraday, includes floating P&L) atau `Balance`
  (realized hanya).
- **`DrawdownLimit`** — `Static` (dari starting balance), `TrailingPercent` (dari peak equity), atau
  `TrailingThresholdDollar` (trails equity peak oleh fixed dollar amount, kemudian **locks pada starting
  balance** sekali equity mencapai threshold — futures-style).
- **`ConsistencyRule`** `(maxSingleDayShareOfProfit)` — blocks pass sementara satu hari mendominasi total profit.
- **`ChallengeRules`** membawa di atas plus `MaxCalendarDays`, `MaxInactivityDays`, `MaxOpenPositions`,
  `AllowWeekendHolding`, `AllowNewsTrading`, `Kind`, `SingleStep`. Rule maths tinggal pada VOs
  (`DrawdownLimit.IsBreached`, `DailyLossLimit.IsBreached`, `ConsistencyRule.IsSatisfied`); aggregate
  orchestrates.

### Jenis challenge & templates

`ChallengeTemplates.For(kind)` builds valid preset untuk `OnePhase`, `TwoPhase`, `ThreePhase`,
`InstantFunding`, atau `Custom` (full control). UI pre-fills template; pengguna dapat menyesuaikan field apa pun.

### Phases & status

- **Phases:** `Evaluation → Verification → Funded` (single-step melewati Verification).
- **Status:** `Active`, `Passed`, `Failed`, plus lifecycle `Stopped` (tracking paused) — `Create` starts
  challenge `Active`; `Stop()`/`Resume()` toggle `Active↔Stopped`.
- **`BreachReason`:** `DailyLoss`, `MaxDrawdown`, `Consistency`, `TimeLimit`, `Inactivity`,
  `WeekendHolding`, `NewsTrading`, `MaxExposure`.

### Rule evaluation

- **`RecordEquity(EquitySnapshot, now)`** — rolls trading day pada day boundaries (captures previous
  day's profit untuk consistency rule), updates peak/daily peaks, kemudian **fails pada first breach**
  (daily loss → drawdown → time limit → inactivity, dalam order) atau advances phase ketika profit target,
  minimum-trading-day, consistency requirements semua terpenuhi. Out-of-order snapshots dan records pada
  terminal challenge throw `DomainException`.
- **`RecordActivity(ActivitySnapshot, now)`** — evaluates behaviour rules (max open positions, weekend
  holding, news trading), stamps activity untuk inactivity rule.
- Soft **`PropFirmDrawdownWarning`** fires once ketika equity usage crosses configurable threshold.

Domain events: `PropFirmChallengeStarted`, `PropFirmChallengeStopped`, `PropFirmPhasePassed`,
`PropFirmChallengePassed`, `PropFirmChallengeBreached`, `PropFirmDrawdownWarning`.

## Live tracking (Execution) — node-hosted, self-healing

Tracking mirrors copy-trading hosting stack exactly; prop tracker = **read-only** cousin dari
copy engine.

- **`PropFirmTrackingSupervisor`** (`src/Nodes/PropFirm`) — `BackgroundService` pada setiap node, gated pada
  `App:PropFirm:Enabled`. Setiap cycle **claims** active challenges pada self-healing lease
  (`AssignedNode` + `LeaseExpiresAt`; dead node's challenges reclaimed sekali lease lapses —
  same atomic `ExecuteUpdate` claim sebagai copy trading, jadi dua node tidak pernah double-track), renews leases,
  pushes rotated tokens in place, stops hosts yang challenge-nya left `Active`.
- **`PropFirmTrackingHost`** (`src/Nodes/PropFirm`) — satu per challenge. Opens `IOpenApiTradingSession`
  untuk account dan, pada `App:PropFirm:EquityPollInterval`, recomputes live equity, feeds ke
  aggregate. Swaps access token in place pada rotation (tidak ada session drop). Exits ketika challenge
  tidak lagi `Active`.
- **`PropFirmEquityCalculator`** (`src/CTraderOpenApi/Client`) — cTrader-faithful equity maths.
  Equity **tidak** delivered oleh Open API, jadi derived: `equity = balance + Σ(unrealized P&L)`,
  di mana setiap position's P&L adalah `priceDifference × units × quote→deposit rate + swap + commission`
  (`units = wire volume / 100`; long revalues pada bid, short pada ask). Balance dari
  `ProtoOATrader`; positions (entry price, swap, commission) dari reconcile; live bid/ask dari spot
  subscriptions. Pure dan isolated — currency-conversion hot spot unit-tested pada dirinya sendiri.

## Alerts

`PropFirmAlertNotifier` (`src/Infrastructure/PropFirm`) subscribes ke pass/breach/warning domain events
(registered sebagai `IDomainEventHandler<>`, dispatched setelah successful `SaveChanges`), notifies pengguna
melalui structured alert/audit trail (`LogMessages`). Live UI reflects same status change. Ini
= cross-context reaction — tidak pernah mutates challenge aggregate.

## API (`/api/prop-firm`, feature `PropFirm`, role User+)

| Method | Route | Purpose |
|--------|-------|---------|
| GET | `/challenges` | list user's challenges (kind, phase, status, live equity, lease) |
| GET | `/challenges/{id}` | satu challenge |
| GET | `/templates` | industry presets untuk create dialog |
| POST | `/challenges` | create dari template **atau** fully custom rule set |
| POST | `/challenges/{id}/start` | resume tracking (Stopped → Active) |
| POST | `/challenges/{id}/stop` | stop tracking (Active → Stopped, release lease) |
| POST | `/challenges/{id}/equity` | record equity snapshot → re-evaluate (manual/no-live-feed path) |
| DELETE | `/challenges/{id}` | soft-delete (blocked saat Active) |

MCP: `Mcp/Tools/PropFirmTools.cs` exposes list/create(dari template)/record-equity/start/stop, gated pada
`PropFirm` feature.

UI: `/prop-firm` (nav *Prop Firm*, gated by `PropFirm` flag) lists challenges dengan **Start/Stop/Delete**
row actions (Start ketika Stopped, Stop ketika Active, Delete disabled saat Active), creates mereka melalui
`NewPropFirmChallengeDialog` (template picker + full rule editor). Semua create/edit via MudBlazor dialog.

## Live equity feed — resolved

Gap "tidak ada live account P&L feed" lebih awal ditutup: ketika `App:PropFirm:Enabled` set, nodes track
account live melalui Open API, feed equity secara otomatis. Tanpanya (default), domain dan
**manual-equity** path (`POST …/equity`) berjalan tanpa perubahan — tidak ada cTrader credentials diperlukan untuk build/test/E2E.

## Tests

- **Unit** — `UnitTests/PropFirm/`: `PropFirmChallengeTests` (phase advance, min-days, static/trailing
  drawdown, daily loss, terminal/out-of-order guards); `PropFirmChallengeRulesTests` (balance vs equity
  daily-loss basis, trailing-threshold-dollar trail+lock, consistency block/allow, time-limit, inactivity,
  max-exposure, weekend, news, stop/resume, lease boundary, pass releases lease, drawdown warning);
  `PropFirmValueObjectTests` (VO ranges + rule-VO maths); `PropFirmEquityCalculatorTests` (long/short P&L,
  swap/commission, quote→deposit conversion, missing pricing); `PropFirmTrackingHostTests` (live equity
  drives pass/fail terhadap extended fake session); `PropFirmAlertNotifierTests`. Time explicit /
  `FakeTimeProvider` — tidak ada wall-clock reads.
- **Integration** — `IntegrationTests/`: `PropFirmChallengePersistenceTests` (round-trip + record-equity +
  soft-delete, enriched-rules + lease round-trip) dan `PropFirmTrackingLeaseTests` (claim, contested lease,
  reclaim setelah lapse lintas dua node identities) pada real Postgres.
- **E2E** — `E2ETests/PropFirmTests.cs`: create + record-equity ke `Passed`; stop→start→breach flow;
  templates endpoint.
- **Stress / DST** — `StressTests/PropFirm/PropFirmChallengeDstTests.cs`: seeded randomized equity/activity
  streams (day rolls, spikes, crashes, duplicate + out-of-order snapshots, exposure/weekend/news) lintas
  banyak mixed-rule challenges, asserting sticky exactly-once terminal states, peak-bounds-current invariant,
  reasoned failures.

## Configuration (`App:PropFirm`)

`Enabled` (off by default), `ReconcileInterval`, `EquityPollInterval`, `LeaseTtl`,
`DrawdownWarnThresholdPercent`, `NodeName`.
