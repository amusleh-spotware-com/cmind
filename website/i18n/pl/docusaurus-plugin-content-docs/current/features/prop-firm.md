---
description: "Handlu-symulatora firm prop (FTMO-style) — trader musi trafić profit target przy utrzymaniu wewnątrz risk limits (max daily loss, max…"
---

# Symulacja wyzwania Prop-firm

Handlu-symulatora firmy prop (FTMO-style) sprzedają **konta ewaluacyjne**: trader musi trafić profit
target przy utrzymaniu wewnątrz risk limits (max daily loss, max total/trailing drawdown, consistency,
time limits) zanim founded. cMind pozwala użytkownikowi tworzyć **custom wyzwanie każdej industrialnej
kształtu**, bind do `TradingAccount`, **uruchamiać jak copy-trading operacja** — started/stopped, hostowana
na węźle, tracked **live przez cTrader Open API**. Aggregate ocenia każdą regułę deterministycznie;
na pass albo breach, kończy wyzwanie, oznacza go, alertuje użytkownika.

## Domena (bounded context: PropFirm)

`PropFirmChallenge` = aggregate root (moduł `Core.PropFirm`), references jego `TradingAccount` przez
strong id tylko (brak cross-aggregate FK). Owns rule evaluation, phase/state machine, node
lease.

### Value objects & rule set

- **`Money`** (non-negative), **`MoneyAmount`** (signed), **`Percent`** (0–100], **`TradingDayRequirement`** (0–365).
- **`EquitySnapshot`** `(equity, balance)` — reading feed do aggregate.
- **`ActivitySnapshot`** `(openPositions, openedInNewsWindow, holdingOverWeekend)` — non-equity fakty.
- **`DailyLossLimit`** `(percent, basis)` — basis `Equity` (intraday, includes floating P&L) lub `Balance`
  (realized tylko).
- **`DrawdownLimit`** — `Static` (od starting balance), `TrailingPercent` (od peak equity), albo
  `TrailingThresholdDollar` (trails peak equity przez fixed dollar amount, potem **locks na starting
  balance** raz equity dochodzi threshold — futures-style).
- **`ConsistencyRule`** `(maxSingleDayShareOfProfit)` — blocks pass gdy jeden dzień dominuje total profit.
- **`ChallengeRules`** niesie powyżej plus `MaxCalendarDays`, `MaxInactivityDays`, `MaxOpenPositions`,
  `AllowWeekendHolding`, `AllowNewsTrading`, `Kind`, `SingleStep`. Rule maths żyją na VOs
  (`DrawdownLimit.IsBreached`, `DailyLossLimit.IsBreached`, `ConsistencyRule.IsSatisfied`);
  aggregate orchestruje.

### Challenge kinds & templates

`ChallengeTemplates.For(kind)` buduje ważny preset dla `OnePhase`, `TwoPhase`, `ThreePhase`,
`InstantFunding`, albo `Custom` (pełna kontrola). UI pre-fills template; użytkownik może adjust każde pole.

### Phases & status

- **Phases:** `Evaluation → Verification → Funded` (single-step skips Verification).
- **Status:** `Active`, `Passed`, `Failed`, plus lifecycle `Stopped` (tracking paused) — `Create` zaczyna
  wyzwanie `Active`; `Stop()`/`Resume()` toggle `Active↔Stopped`.
- **`BreachReason`:** `DailyLoss`, `MaxDrawdown`, `Consistency`, `TimeLimit`, `Inactivity`,
  `WeekendHolding`, `NewsTrading`, `MaxExposure`.

### Rule evaluation

- **`RecordEquity(EquitySnapshot, now)`** — rolls trading day na granicach dnia (captures poprzedniego
  dnia profit dla consistency rule), updates peak/daily peaks, potem **fails na pierwszy breach**
  (daily loss → drawdown → time limit → inactivity, in order) albo advances phase gdy profit target,
  minimum-trading-day, consistency requirements wszystkie met. Out-of-order snapshots i records na
  terminal challenge throw `DomainException`.
- **`RecordActivity(ActivitySnapshot, now)`** — evaluuje behaviour rules (max open positions,
  weekend holding, news trading), stamps activity dla inactivity rule.
- Soft **`PropFirmDrawdownWarning`** fires raz gdy equity usage crosses configurable threshold.

Domain events: `PropFirmChallengeStarted`, `PropFirmChallengeStopped`, `PropFirmPhasePassed`,
`PropFirmChallengePassed`, `PropFirmChallengeBreached`, `PropFirmDrawdownWarning`.

## Live tracking (Execution) — node-hosted, self-healing

Tracking mirrors copy-trading hosting stack dokładnie; prop tracker = **read-only** cousin z
copy engine.

- **`PropFirmTrackingSupervisor`** (`src/Nodes/PropFirm`) — `BackgroundService` na każdym węźle,
  gated na `App:PropFirm:Enabled`. Każdy cykl **claims** active challenges na self-healing lease
  (`AssignedNode` + `LeaseExpiresAt`; dead node challenges reclaimed raz lease lapses —
  ten sam atomic `ExecuteUpdate` claim jak copy trading, więc dwa węzły nigdy nie double-track),
  renews leases, pushes rotated tokens w miejscu, stops hosts których wyzwanie left `Active`.
- **`PropFirmTrackingHost`** (`src/Nodes/PropFirm`) — jeden per wyzwanie. Otwiera `IOpenApiTradingSession`
  dla konta i, na `App:PropFirm:EquityPollInterval`, recomputes live equity, feeds do
  aggregate. Swaps access token w miejscu na rotation (brak session drop). Exits gdy wyzwanie
  nie jest więcej `Active`.
- **`PropFirmEquityCalculator`** (`src/CTraderOpenApi/Client`) — cTrader-faithful equity maths.
  Equity **nie** delivered przez Open API, więc derived: `equity = balance + Σ(unrealized P&L)`,
  gdzie każdy position P&L to `priceDifference × units × quote→deposit rate + swap + commission`
  (`units = wire volume / 100`; long revalues przy bid, short przy ask). Balance z
  `ProtoOATrader`; positions (entry price, swap, commission) z reconcile; live bid/ask z spot
  subscriptions. Czysta i isolated — currency-conversion hot spot unit-tested na jego własnym.

## Alerts

`PropFirmAlertNotifier` (`src/Infrastructure/PropFirm`) subskrybuje pass/breach/warning domain events
(registered jako `IDomainEventHandler<>`, dispatched po successful `SaveChanges`), notifies użytkownika
przez structured alert/audit trail (`LogMessages`). Live UI reflects ten sam status change. To
= cross-context reaction — nigdy nie mutuje challenge aggregate.

## API (`/api/prop-firm`, feature `PropFirm`, role User+)

| Method | Route | Cel |
|--------|-------|-----|
| GET | `/challenges` | list challenges użytkownika (kind, phase, status, live equity, lease) |
| GET | `/challenges/{id}` | jedno wyzwanie |
| GET | `/templates` | industry presets dla create dialog |
| POST | `/challenges` | create z template **albo** fully custom rule set |
| POST | `/challenges/{id}/start` | resume tracking (Stopped → Active) |
| POST | `/challenges/{id}/stop` | stop tracking (Active → Stopped, release lease) |
| POST | `/challenges/{id}/equity` | record equity snapshot → re-evaluate (manual/no-live-feed path) |
| DELETE | `/challenges/{id}` | soft-delete (blocked gdy Active) |

MCP: `Mcp/Tools/PropFirmTools.cs` exposes list/create(z template)/record-equity/start/stop, gated na
`PropFirm` feature.

UI: `/prop-firm` (nav *Prop Firm*, gated przez `PropFirm` flag) listy challenges z **Start/Stop/Delete**
row actions (Start gdy Stopped, Stop gdy Active, Delete disabled gdy Active), tworzy je przez
`NewPropFirmChallengeDialog` (template picker + full rule editor). Wszystkie create/edit przez MudBlazor
dialog.

## Live equity feed — resolved

Wcześniejszy "brak live account P&L feed" gap zamknięty: gdy `App:PropFirm:Enabled` ustawiony, węzły
track konto live przez Open API, feed equity automatycznie. Bez tego (default), domena i
**manual-equity** path (`POST …/equity`) uruchamiają bez zmian — brak cTrader credentials needed dla
build/test/E2E.

## Tests

- **Unit** — `UnitTests/PropFirm/`: `PropFirmChallengeTests` (phase advance, min-days, static/trailing
  drawdown, daily loss, terminal/out-of-order guards); `PropFirmChallengeRulesTests` (balance vs equity
  daily-loss basis, trailing-threshold-dollar trail+lock, consistency block/allow, time-limit,
  inactivity, max-exposure, weekend, news, stop/resume, lease boundary, pass releases lease,
  drawdown warning); `PropFirmValueObjectTests` (VO ranges + rule-VO maths); `PropFirmEquityCalculatorTests`
  (long/short P&L, swap/commission, quote→deposit conversion, missing pricing); `PropFirmTrackingHostTests`
  (live equity drives pass/fail against extended fake session); `PropFirmAlertNotifierTests`. Time
  explicit / `FakeTimeProvider` — brak wall-clock reads.
- **Integration** — `IntegrationTests/`: `PropFirmChallengePersistenceTests` (round-trip + record-equity +
  soft-delete, enriched-rules + lease round-trip) i `PropFirmTrackingLeaseTests` (claim, contested lease,
  reclaim po lapse across dwa node identities) na realnym Postgres.
- **E2E** — `E2ETests/PropFirmTests.cs`: create + record-equity do `Passed`; stop→start→breach flow;
  templates endpoint.
- **Stress / DST** — `StressTests/PropFirm/PropFirmChallengeDstTests.cs`: seeded randomized equity/activity
  streams (day rolls, spikes, crashes, duplicate + out-of-order snapshots, exposure/weekend/news)
  across wiele mixed-rule challenges, asserting sticky exactly-once terminal states, peak-bounds-current
  invariant, reasoned failures.

## Configuration (`App:PropFirm`)

`Enabled` (off domyślnie), `ReconcileInterval`, `EquityPollInterval`, `LeaseTtl`,
`DrawdownWarnThresholdPercent`, `NodeName`.
