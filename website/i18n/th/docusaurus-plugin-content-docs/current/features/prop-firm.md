---
description: "Retail prop firms (FTMO-style) ขาย evaluation accounts: trader ต้อง hit profit target ขณะ staying ภายใน risk limits (max daily loss max total/trailing drawdown consistency time limits) ก่อน funded cMind let user create **custom challenge ของ any industry shape** bind เป็น `TradingAccount` **run เหมือน copy-trading operation** — started/stopped hosted บน node tracked **live ผ่าน cTrader Open API**"
---

# Prop-firm challenge simulation

Retail prop firms (FTMO-style) ขาย **evaluation accounts**: trader ต้อง hit profit target ขณะ staying ภายใน risk limits (max daily loss max total/trailing drawdown consistency time limits) ก่อน funded cMind let user create **custom challenge ของ any industry shape** bind เป็น `TradingAccount` **run เหมือน copy-trading operation** — started/stopped hosted บน node tracked **live ผ่าน cTrader Open API** aggregate evaluates ทุก ๆ rule deterministically; บน pass หรือ breach ends challenge marks มัน alerts user

## Domain (bounded context: PropFirm)

`PropFirmChallenge` = aggregate root (module `Core.PropFirm`) references its `TradingAccount` โดย strong id เพียง (ไม่มี cross-aggregate FK) owns rule evaluation phase/state machine node lease

### Value objects & rule set

- **`Money`** (non-negative) **`MoneyAmount`** (signed) **`Percent`** (0–100] **`TradingDayRequirement`** (0–365)
- **`EquitySnapshot`** `(equity, balance)` — reading fed เป็น aggregate
- **`ActivitySnapshot`** `(openPositions, openedInNewsWindow, holdingOverWeekend)` — non-equity facts
- **`DailyLossLimit`** `(percent, basis)` — basis `Equity` (intraday includes floating P&L) หรือ `Balance` (realized เพียง)
- **`DrawdownLimit`** — `Static` (จาก starting balance) `TrailingPercent` (จาก peak equity) หรือ `TrailingThresholdDollar` (trails equity peak โดย fixed dollar amount แล้ว **locks ที่ starting balance** เมื่อ equity reaches threshold — futures-style)
- **`ConsistencyRule`** `(maxSingleDayShareOfProfit)` — blocks pass ขณะ one day dominates total profit
- **`ChallengeRules`** carries ด้านบน บวก `MaxCalendarDays` `MaxInactivityDays` `MaxOpenPositions` `AllowWeekendHolding` `AllowNewsTrading` `Kind` `SingleStep` rule maths live บน VOs (`DrawdownLimit.IsBreached` `DailyLossLimit.IsBreached` `ConsistencyRule.IsSatisfied`); aggregate orchestrates

### Challenge kinds & templates

`ChallengeTemplates.For(kind)` builds valid preset สำหรับ `OnePhase` `TwoPhase` `ThreePhase` `InstantFunding` หรือ `Custom` (full control) UI pre-fills template; user อาจ adjust any field

### Phases & status

- **Phases:** `Evaluation → Verification → Funded` (single-step skips verification)
- **Status:** `Active` `Passed` `Failed` บวก lifecycle `Stopped` (tracking paused) — `Create` starts challenge `Active`; `Stop()`/`Resume()` toggle `Active↔Stopped`
- **`BreachReason`:** `DailyLoss` `MaxDrawdown` `Consistency` `TimeLimit` `Inactivity` `WeekendHolding` `NewsTrading` `MaxExposure`

### Rule evaluation

- **`RecordEquity(EquitySnapshot, now)`** — rolls trading day ที่ day boundaries (captures previous day's profit สำหรับ consistency rule) updates peak/daily peaks แล้ว **fails บน first breach** (daily loss → drawdown → time limit → inactivity in order) หรือ advances phase เมื่อ profit target minimum-trading-day consistency requirements ทั้งหมด met out-of-order snapshots และ records บน terminal challenge throw `DomainException`
- **`RecordActivity(ActivitySnapshot, now)`** — evaluates behaviour rules (max open positions weekend holding news trading) stamps activity สำหรับ inactivity rule
- soft **`PropFirmDrawdownWarning`** fires เมื่อ equity usage crosses configurable threshold

domain events: `PropFirmChallengeStarted` `PropFirmChallengeStopped` `PropFirmPhasePassed` `PropFirmChallengePassed` `PropFirmChallengeBreached` `PropFirmDrawdownWarning`

## Live tracking (Execution) — node-hosted self-healing

tracking mirrors copy-trading hosting stack ตรง; prop tracker = **read-only** cousin ของ copy engine

- **`PropFirmTrackingSupervisor`** (`src/Nodes/PropFirm`) — `BackgroundService` บน ทุก ๆ node gated บน `App:PropFirm:Enabled` ทุก ๆ cycle **claims** active challenges บน self-healing lease (`AssignedNode` + `LeaseExpiresAt`; dead node's challenges reclaimed เมื่อ lease lapses — same atomic `ExecuteUpdate` claim เป็น copy trading ดังนั้น สอง nodes ไม่เคย double-track) renews leases pushes rotated tokens in place stops hosts whose challenge left `Active`
- **`PropFirmTrackingHost`** (`src/Nodes/PropFirm`) — one per challenge opens `IOpenApiTradingSession` สำหรับ account และ on `App:PropFirm:EquityPollInterval` recomputes live equity feeds เป็น aggregate swaps access token in place บน rotation (no session drop) exits เมื่อ challenge ไม่มี longer `Active`
- **`PropFirmEquityCalculator`** (`src/CTraderOpenApi/Client`) — cTrader-faithful equity maths equity **ไม่** delivered โดย open API ดังนั้น derived: `equity = balance + Σ(unrealized P&L)` where ทุก ๆ position's P&L เป็น `priceDifference × units × quote→deposit rate + swap + commission` (`units = wire volume / 100`; long revalues ที่ bid short ที่ ask) balance จาก `ProtoOATrader`; positions (entry price swap commission) จาก reconcile; live bid/ask จาก spot subscriptions pure และ isolated — currency-conversion hot spot unit-tested บน ของมันเอง

## Alerts

`PropFirmAlertNotifier` (`src/Infrastructure/PropFirm`) subscribes เป็น pass/breach/warning domain events (registered เป็น `IDomainEventHandler<>` dispatched หลัง successful `SaveChanges`) notifies user ผ่าน structured alert/audit trail (`LogMessages`) live UI reflects same status เปลี่ยน นี่ = cross-context reaction — ไม่เคย mutates challenge aggregate

## API (`/api/prop-firm` feature `PropFirm` role user+)

| Method | Route | Purpose |
|--------|-------|---------|
| GET | `/challenges` | list user's challenges (kind phase status live equity lease) |
| GET | `/challenges/{id}` | one challenge |
| GET | `/templates` | industry presets สำหรับ create dialog |
| POST | `/challenges` | create จาก template **หรือ** fully custom rule set |
| POST | `/challenges/{id}/start` | resume tracking (stopped → active) |
| POST | `/challenges/{id}/stop` | stop tracking (active → stopped release lease) |
| POST | `/challenges/{id}/equity` | record equity snapshot → re-evaluate (manual/no-live-feed path) |
| DELETE | `/challenges/{id}` | soft-delete (blocked ขณะ active) |

MCP: `Mcp/Tools/PropFirmTools.cs` exposes list/create(from template)/record-equity/start/stop gated บน `PropFirm` feature

UI: `/prop-firm` (nav *Prop Firm* gated โดย `PropFirm` flag) lists challenges ด้วย **Start/Stop/Delete** row actions (start เมื่อ stopped stop เมื่อ active delete disabled ขณะ active) creates พวกเขา ผ่าน `NewPropFirmChallengeDialog` (template picker + full rule editor) ทั้งหมด create/edit ผ่าน mudblazor dialog

## Live equity feed — resolved

ก่อน "ไม่มี live account P&L feed" gap closed: เมื่อ `App:PropFirm:Enabled` ตั้ง nodes track account live ผ่าน open API feed equity automatically โดยไม่ต้อง (default) domain และ **manual-equity** path (`POST …/equity`) run unchanged — ไม่มี ctrader credentials ต้อง build/test/E2E

## Tests

- **Unit** — `UnitTests/PropFirm/`: `PropFirmChallengeTests` (phase advance min-days static/trailing drawdown daily loss terminal/out-of-order guards); `PropFirmChallengeRulesTests` (balance vs equity daily-loss basis trailing-threshold-dollar trail+lock consistency block/allow time-limit inactivity max-exposure weekend news stop/resume lease boundary pass releases lease drawdown warning); `PropFirmValueObjectTests` (VO ranges + rule-VO maths); `PropFirmEquityCalculatorTests` (long/short P&L swap/commission quote→deposit conversion missing pricing); `PropFirmTrackingHostTests` (live equity drives pass/fail ต้านแบบ extended fake session); `PropFirmAlertNotifierTests` time explicit / `FakeTimeProvider` — ไม่มี wall-clock reads
- **Integration** — `IntegrationTests/`: `PropFirmChallengePersistenceTests` (round-trip + record-equity + soft-delete enriched-rules + lease round-trip) และ `PropFirmTrackingLeaseTests` (claim contested lease reclaim หลัง lapse ข้ามบน สอง node identities) บน real postgres
- **E2E** — `E2ETests/PropFirmTests.cs`: create + record-equity เป็น `Passed`; stop→start→breach flow; templates endpoint
- **Stress / DST** — `StressTests/PropFirm/PropFirmChallengeDstTests.cs`: seeded randomized equity/activity streams (day rolls spikes crashes duplicate + out-of-order snapshots exposure/weekend/news) ข้ามบน many mixed-rule challenges asserting sticky exactly-once terminal states peak-bounds-current invariant reasoned failures

## Configuration (`App:PropFirm`)

`Enabled` (off โดย default) `ReconcileInterval` `EquityPollInterval` `LeaseTtl` `DrawdownWarnThresholdPercent` `NodeName`
