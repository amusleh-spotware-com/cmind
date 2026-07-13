---
description: "Retail prop firms (FTMO-style) ขาย evaluation accounts: trader ต้องตี profit target ขณะอยู่ใน risk limits (max daily loss, max total/trailing drawdown, consistency, time limits)"
---

# Prop-firm challenge simulation

Retail prop firms (FTMO-style) ขาย **evaluation accounts**: trader ต้องตี profit target ขณะ
อยู่ใน risk limits (max daily loss, max total/trailing drawdown, consistency, time limits) ก่อน
funded cMind ให้ user สร้าง **custom challenge ในรูปแบบอุตสาหกรรมใดก็ได้**, bind ไปยัง
`TradingAccount`, **รันเหมือน copy-trading operation** — started/stopped, hosted on node,
tracked **live over cTrader Open API** Aggregate ประเมินทุก rule แบบ deterministic; บน
pass หรือ breach, จบ challenge, mark มัน, alert user

## Domain (bounded context: PropFirm)

`PropFirmChallenge` = aggregate root (module `Core.PropFirm`), references `TradingAccount`
ของมันด้วย strong id เท่านั้น (ไม่มี cross-aggregate FK) Owns rule evaluation,
phase/state machine, node lease

### Value objects & rule set

- **`Money`** (non-negative), **`MoneyAmount`** (signed), **`Percent`** (0–100],
  **`TradingDayRequirement`** (0–365)
- **`EquitySnapshot`** `(equity, balance)` — reading ที่ feed ไปยัง aggregate
- **`ActivitySnapshot`** `(openPositions, openedInNewsWindow, holdingOverWeekend)` — non-equity facts
- **`DailyLossLimit`** `(percent, basis)` — basis `Equity` (intraday, รวม floating P&L) หรือ
  `Balance` (realized เท่านั้น)
- **`DrawdownLimit`** — `Static` (จาก starting balance), `TrailingPercent` (จาก peak equity),
  หรือ `TrailingThresholdDollar` (trails equity peak โดย fixed dollar amount แล้ว **locks at
  starting balance** เมื่อ equity ถึง threshold — futures-style)
- **`ConsistencyRule`** `(maxSingleDayShareOfProfit)` — blocks pass ขณะที่ day หนึ่ง dominate
  total profit
- **`ChallengeRules`** ถือข้างบนบวก `MaxCalendarDays`, `MaxInactivityDays`, `MaxOpenPositions`,
  `AllowWeekendHolding`, `AllowNewsTrading`, `Kind`, `SingleStep` Rule maths อยู่บน VOs
  (`DrawdownLimit.IsBreached`, `DailyLossLimit.IsBreached`,
  `ConsistencyRule.IsSatisfied`); aggregate orchestrates

### Challenge kinds & templates

`ChallengeTemplates.For(kind)` สร้าง valid preset สำหรับ `OnePhase`, `TwoPhase`,
`ThreePhase`, `InstantFunding`, หรือ `Custom` (full control) UI pre-fills template;
user อาจปรับ field ใดก็ได้

### Phases & status

- **Phases:** `Evaluation → Verification → Funded` (single-step ข้าม Verification)
- **Status:** `Active`, `Passed`, `Failed`, บวก lifecycle `Stopped` (tracking paused) —
  `Create` starts challenge `Active`; `Stop()`/`Resume()` toggle `Active↔Stopped`
- **`BreachReason`:** `DailyLoss`, `MaxDrawdown`, `Consistency`, `TimeLimit`, `Inactivity`,
  `WeekendHolding`, `NewsTrading`, `MaxExposure`

### Rule evaluation

- **`RecordEquity(EquitySnapshot, now)`** — rolls trading day ที่ day boundaries (captures
  previous day's profit สำหรับ consistency rule), updates peak/daily peaks, แล้ว **fails on
  first breach** (daily loss → drawdown → time limit → inactivity, ตามลำดับ) หรือ advances
  phase เมื่อ profit target, minimum-trading-day, consistency requirements ทั้งหมด meet
  Out-of-order snapshots และ records บน terminal challenge throw `DomainException`
- **`RecordActivity(ActivitySnapshot, now)`** — evaluates behaviour rules (max open positions,
  weekend holding, news trading), stamps activity สำหรับ inactivity rule
- Soft **`PropFirmDrawdownWarning`** fire ครั้งเดียวเมื่อ equity usage crosses configurable threshold

Domain events: `PropFirmChallengeStarted`, `PropFirmChallengeStopped`,
`PropFirmPhasePassed`, `PropFirmChallengePassed`, `PropFirmChallengeBreached`,
`PropFirmDrawdownWarning`

## Live tracking (Execution) — node-hosted, self-healing

Tracking mirrors copy-trading hosting stack อย่างแน่นอน; prop tracker = **read-only** cousin
ของ copy engine

- **`PropFirmTrackingSupervisor`** (`src/Nodes/PropFirm`) — `BackgroundService` บนแต่ละ node,
  gated บน `App:PropFirm:Enabled` แต่ละ cycle **claims** active challenges บน self-healing
  lease (`AssignedNode` + `LeaseExpiresAt`; dead node's challenges ถูก reclaim เมื่อ lease
  lapses — same atomic `ExecuteUpdate` claim เหมือน copy trading ดังนั้นสอง nodes ไม่เคย
  double-track), renews leases, pushes rotated tokens in place, stops hosts ที่ challenge
  ออกจาก `Active`
- **`PropFirmTrackingHost`** (`src/Nodes/PropFirm`) — หนึ่งต่อ challenge เปิด
  `IOpenApiTradingSession` สำหรับ account และเมื่อ `App:PropFirm:EquityPollInterval`,
  recomputes live equity, feed ไปยัง aggregate เปลี่ยน access token in place on rotation
  (ไม่มี session drop) exits เมื่อ challenge ไม่ `Active` แล้ว
- **`PropFirmEquityCalculator`** (`src/CTraderOpenApi/Client`) — cTrader-faithful equity maths
  Equity **ไม่ได้** delivered โดย Open API, ดังนั้น derive: `equity = balance + Σ(unrealized
  P&L)`, โดยแต่ละ position's P&L คือ `priceDifference × units × quote→deposit rate + swap +
  commission` (`units = wire volume / 100`; long revalues ที่ bid, short ที่ ask) Balance จาก
  `ProtoOATrader`; positions (entry price, swap, commission) จาก reconcile; live bid/ask จาก
  spot subscriptions Pure และ isolated — currency-conversion hot spot unit-tested บนตัวเอง

## Alerts

`PropFirmAlertNotifier` (`src/Infrastructure/PropFirm`) subscribe ไปยัง pass/breach/warning
domain events (registered as `IDomainEventHandler<>`, dispatched after successful
`SaveChanges`), notifies user ผ่าน structured alert/audit trail (`LogMessages`)
Live UI สะท้อน status change เดียวกัน นี่ = cross-context reaction — ไม่เคย mutate
challenge aggregate

## API (`/api/prop-firm`, feature `PropFirm`, role User+)

| Method | Route | Purpose |
|--------|-------|---------|
| GET | `/challenges` | list user's challenges (kind, phase, status, live equity, lease) |
| GET | `/challenges/{id}` | one challenge |
| GET | `/templates` | industry presets สำหรับ create dialog |
| POST | `/challenges` | create จาก template **หรือ** fully custom rule set |
| POST | `/challenges/{id}/start` | resume tracking (Stopped → Active) |
| POST | `/challenges/{id}/stop` | stop tracking (Active → Stopped, release lease) |
| POST | `/challenges/{id}/equity` | record equity snapshot → re-evaluate (manual/no-live-feed path) |
| DELETE | `/challenges/{id}` | soft-delete (blocked ขณะ Active) |

MCP: `Mcp/Tools/PropFirmTools.cs` expose list/create(from template)/record-equity/start/stop,
gated บน `PropFirm` feature

UI: `/prop-firm` (nav *Prop Firm*, gated บน `PropFirm` flag) lists challenges พร้อม
**Start/Stop/Delete** row actions (Start เมื่อ Stopped, Stop เมื่อ Active, Delete disabled
ขณะ Active), สร้างผ่าน `NewPropFirmChallengeDialog` (template picker + full rule editor)
ทุก create/edit ผ่าน MudBlazor dialog

## Live equity feed — resolved

ช่องว่าง "no live account P&L feed" ก่อนหน้าปิดแล้ว: เมื่อ `App:PropFirm:Enabled` ถูกตั้ง,
nodes track account live over Open API, feed equity โดยอัตโนมัติ โดยไม่มีมัน (default),
domain และ **manual-equity** path (`POST …/equity`) ทำงานเหมือนเดิม — ไม่ต้องมี
cTrader credentials สำหรับ build/test/E2E

## Tests

- **Unit** — `UnitTests/PropFirm/`: `PropFirmChallengeTests` (phase advance, min-days,
  static/trailing drawdown, daily loss, terminal/out-of-order guards);
  `PropFirmChallengeRulesTests` (balance vs equity daily-loss basis, trailing-threshold-dollar
  trail+lock, consistency block/allow, time-limit, inactivity, max-exposure, weekend, news,
  stop/resume, lease boundary, pass releases lease, drawdown warning);
  `PropFirmValueObjectTests` (VO ranges + rule-VO maths); `PropFirmEquityCalculatorTests`
  (long/short P&L, swap/commission, quote→deposit conversion, missing pricing);
  `PropFirmTrackingHostTests` (live equity drives pass/fail ต่อ extended fake session);
  `PropFirmAlertNotifierTests` Time explicit / `FakeTimeProvider` — ไม่มี wall-clock reads
- **Integration** — `IntegrationTests/`: `PropFirmChallengePersistenceTests` (round-trip +
  record-equity + soft-delete, enriched-rules + lease round-trip) และ
  `PropFirmTrackingLeaseTests` (claim, contested lease, reclaim after lapse ข้าม two node
  identities) บน real Postgres
- **E2E** — `E2ETests/PropFirmTests.cs`: create + record-equity ไปยัง `Passed`;
  stop→start→breach flow; templates endpoint
- **Stress / DST** — `StressTests/PropFirm/PropFirmChallengeDstTests.cs`: seeded randomized
  equity/activity streams (day rolls, spikes, crashes, duplicate + out-of-order snapshots,
  exposure/weekend/news) ข้าม many mixed-rule challenges, asserting sticky exactly-once terminal
  states, peak-bounds-current invariant, reasoned failures

## Configuration (`App:PropFirm`)

`Enabled` (off by default), `ReconcileInterval`, `EquityPollInterval`, `LeaseTtl`,
`DrawdownWarnThresholdPercent`, `NodeName`
