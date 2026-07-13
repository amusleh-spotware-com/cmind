---
description: "Retail prop firms (FTMO-style) продают evaluation accounts: трейдер должен достичь profit target соблюдая risk limits. cMind позволяет создавать custom challenge любой industry shape, привязать к TradingAccount, отслеживать live over cTrader Open API."
---

# Prop-firm challenge simulation

Розничные prop firms (FTMO-style) продают **evaluation accounts**: трейдер должен достичь profit target while
соблюдая risk limits (max daily loss, max total/trailing drawdown, consistency, time limits) перед funded.
cMind позволяет пользователю создать **custom challenge любой industry shape**, привязать к
`TradingAccount`, **запустить как copy-trading operation** — started/stopped, hosted on node,
отслеживать **live over cTrader Open API**. Агрегат детерминированно оценивает каждое правило; на
pass или breach, ends challenge, marks it, alerts user.

## Домен (bounded context: PropFirm)

`PropFirmChallenge` = aggregate root (модуль `Core.PropFirm`), ссылается на свой `TradingAccount` по
strong id только (no cross-aggregate FK). Owns rule evaluation, phase/state machine, node lease.

### Value objects & rule set

- **`Money`** (не-отрицательный), **`MoneyAmount`** (signed), **`Percent`** (0–100], **`TradingDayRequirement`** (0–365).
- **`EquitySnapshot`** `(equity, balance)` — reading fed to aggregate.
- **`ActivitySnapshot`** `(openPositions, openedInNewsWindow, holdingOverWeekend)` — не-equity facts.
- **`DailyLossLimit`** `(percent, basis)` — basis `Equity` (intraday, includes floating P&L) или `Balance`
  (realized only).
- **`DrawdownLimit`** — `Static` (от starting balance), `TrailingPercent` (от peak equity), или
  `TrailingThresholdDollar` (trails equity peak by fixed dollar amount, затем **locks at starting
  balance** once equity reaches threshold — futures-style).
- **`ConsistencyRule`** `(maxSingleDayShareOfProfit)` — блокирует pass пока один день доминирует total profit.
- **`ChallengeRules`** несёт вышеперечисленное плюс `MaxCalendarDays`, `MaxInactivityDays`,
  `MaxOpenPositions`, `AllowWeekendHolding`, `AllowNewsTrading`, `Kind`, `SingleStep`. Rule maths живут на VOs
  (`DrawdownLimit.IsBreached`, `DailyLossLimit.IsBreached`, `ConsistencyRule.IsSatisfied`); aggregate
  оркестрирует.

### Challenge kinds & templates

`ChallengeTemplates.For(kind)` строит валидный пресет для `OnePhase`, `TwoPhase`, `ThreePhase`,
`InstantFunding`, или `Custom` (full control). UI pre-fills template; user может настроить любое поле.

### Фазы & статус

- **Фазы:** `Evaluation → Verification → Funded` (single-step пропускает Verification).
- **Статус:** `Active`, `Passed`, `Failed`, плюс lifecycle `Stopped` (tracking paused) — `Create` запускает
  challenge `Active`; `Stop()`/`Resume()` toggle `Active↔Stopped`.
- **`BreachReason`:** `DailyLoss`, `MaxDrawdown`, `Consistency`, `TimeLimit`, `Inactivity`,
  `WeekendHolding`, `NewsTrading`, `MaxExposure`.

### Rule evaluation

- **`RecordEquity(EquitySnapshot, now)`** — rolls trading day at day boundaries (captures previous
  day's profit for consistency rule), updates peak/daily peaks, затем **fails on first breach**
  (daily loss → drawdown → time limit → inactivity, in order) или advances phase когда profit target,
  minimum-trading-day, consistency requirements все met. Out-of-order snapshots и records on
  terminal challenge throw `DomainException`.
- **`RecordActivity(ActivitySnapshot, now)`** — оценивает behaviour rules (max open positions, weekend
  holding, news trading), stamps activity для inactivity rule.
- Soft **`PropFirmDrawdownWarning`** fires once когда equity usage crosses configurable threshold.

Domain events: `PropFirmChallengeStarted`, `PropFirmChallengeStopped`, `PropFirmPhasePassed`,
`PropFirmChallengePassed`, `PropFirmChallengeBreached`, `PropFirmDrawdownWarning`.

## Live tracking (Execution) — node-hosted, self-healing

Tracking mirrors copy-trading hosting stack точно; prop tracker = **read-only** cousin of
copy engine.

- **`PropFirmTrackingSupervisor`** (`src/Nodes/PropFirm`) — `BackgroundService` на каждом узле, gated on
  `App:PropFirm:Enabled`. Каждый cycle **claims** active challenges on self-healing lease
  (`AssignedNode` + `LeaseExpiresAt`; dead node's challenges reclaimed once lease lapses —
  same atomic `ExecuteUpdate` claim as copy trading, so two nodes never double-track), renews leases,
  pushes rotated tokens in place, stops hosts whose challenge left `Active`.
- **`PropFirmTrackingHost`** (`src/Nodes/PropFirm`) — один per challenge. Opens `IOpenApiTradingSession`
  для счёта и, на `App:PropFirm:EquityPollInterval`, перевычисляет live equity, feed'ит к
  aggregate. Swap'ит access token in place on rotation (no session drop). Exits когда challenge
  больше не `Active`.
- **`PropFirmEquityCalculator`** (`src/CTraderOpenApi/Client`) — cTrader-faithful equity maths.
  Equity **не** delivered by Open API, so derived: `equity = balance + Σ(unrealized P&L)`,
  где each position's P&L = `priceDifference × units × quote→deposit rate + swap + commission`
  (`units = wire volume / 100`; long revalues at bid, short at ask). Balance from
  `ProtoOATrader`; positions from reconcile; live bid/ask from spot subscriptions. Pure и isolated —
  currency-conversion hot spot unit-tested на своём.

## Alerts

`PropFirmAlertNotifier` (`src/Infrastructure/PropFirm`) подписывается на pass/breach/warning domain events
(dispatched after successful `SaveChanges`), уведомляет пользователя через structured alert/audit trail
(`LogMessages`). Live UI отражает то же изменение статуса. Это = cross-context reaction — никогда не
mutates challenge aggregate.

## API (`/api/prop-firm`, feature `PropFirm`, role User+)

| Метод | Маршрут | Назначение |
|--------|-------|---------|
| GET | `/challenges` | список challenges пользователя (kind, phase, status, live equity, lease) |
| GET | `/challenges/{id}` | один challenge |
| GET | `/templates` | industry presets для create dialog |
| POST | `/challenges` | создать из template или полностью custom rule set |
| POST | `/challenges/{id}/start` | resume tracking (Stopped → Active) |
| POST | `/challenges/{id}/stop` | stop tracking (Active → Stopped, release lease) |
| POST | `/challenges/{id}/equity` | записать equity snapshot → re-evaluate (manual/no-live-feed path) |
| DELETE | `/challenges/{id}` | soft-delete (blocked while Active) |

MCP: `Mcp/Tools/PropFirmTools.cs` exposes list/create(from template)/record-equity/start/stop, gated on
`PropFirm` feature.

UI: `/prop-firm` (nav *Prop Firm*, gated by `PropFirm` flag) перечисляет challenges с **Start/Stop/Delete**
row actions, создаёт их через `NewPropFirmChallengeDialog` (template picker + full rule editor).

## Live equity feed — resolved

Ранее "no live account P&L feed" gap closed: когда `App:PropFirm:Enabled` set, nodes track
account live over Open API, feed equity automatically. Без него (по умолчанию), domain и
**manual-equity** path (`POST …/equity`) работают без изменений — без cTrader credentials needed for build/test/E2E.

## Тесты

- **Unit** — `UnitTests/PropFirm/`: `PropFirmChallengeTests` (phase advance, min-days, static/trailing
  drawdown, daily loss, terminal/out-of-order guards); `PropFirmChallengeRulesTests` (balance vs equity
  daily-loss basis, trailing-threshold-dollar trail+lock, consistency block/allow, time-limit, inactivity,
  max-exposure, weekend, news, stop/resume, lease boundary, pass releases lease, drawdown warning);
  `PropFirmValueObjectTests` (VO ranges + rule-VO maths); `PropFirmEquityCalculatorTests` (long/short P&L,
  swap/commission, quote→deposit conversion, missing pricing); `PropFirmTrackingHostTests` (live equity
  drives pass/fail against extended fake session); `PropFirmAlertNotifierTests`. Time explicit /
  `FakeTimeProvider` — никаких wall-clock reads.
- **Integration** — `IntegrationTests/`: `PropFirmChallengePersistenceTests` (round-trip + record-equity +
  soft-delete, enriched-rules + lease round-trip) и `PropFirmTrackingLeaseTests` (claim, contested lease,
  reclaim after lapse across two node identities) на реальном Postgres.
- **E2E** — `E2ETests/PropFirmTests.cs`: create + record-equity to `Passed`; stop→start→breach flow;
  templates endpoint.
- **Stress / DST** — `StressTests/PropFirm/PropFirmChallengeDstTests.cs`: seeded randomized equity/activity
  streams (day rolls, spikes, crashes, duplicate + out-of-order snapshots, exposure/weekend/news) across
  many mixed-rule challenges, asserting sticky exactly-once terminal states, peak-bounds-current invariant,
  reasoned failures.

## Конфигурация (`App:PropFirm`)

`Enabled` (off by default), `ReconcileInterval`, `EquityPollInterval`, `LeaseTtl`,
`DrawdownWarnThresholdPercent`, `NodeName`.
