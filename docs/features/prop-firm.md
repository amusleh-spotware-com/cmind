# Prop-firm challenges

Retail prop firms (FTMO-style) sell **evaluation accounts**: a trader must hit a profit target while
staying inside risk limits (max daily loss, max total/trailing drawdown) over a minimum number of trading
days, across one or two evaluation phases, before being funded. This feature models that challenge and
evaluates the rules deterministically.

## Domain (bounded context: PropFirm)

`PropFirmChallenge` is the aggregate root (module `Core.PropFirm`), referencing its `TradingAccount` by
strong id only (no cross-aggregate FK). It owns the rule evaluation and the phase/state machine.

- **Value objects:** `Money` (non-negative), `Percent` (0–100], `TradingDayRequirement` (0–365),
  `ChallengeRules` (profit target, max daily loss, max total drawdown, drawdown mode, min trading days,
  single-step flag). All self-validating; invalid input throws a `DomainException`.
- **Phases:** `Evaluation → Verification → Funded` (single-step challenges skip Verification).
- **Status:** `Active`, `Passed` (funded), `Failed` (breached). `BreachReason`: `DailyLoss`, `MaxDrawdown`.
- **Drawdown modes:** `Static` (measured from the starting balance) or `Trailing` (measured from peak equity).

### Rule evaluation — `RecordEquity(Money equity, DateTimeOffset now)`

The aggregate is fed equity snapshots and decides every transition (callers never encode the rules):

1. **Trading-day roll** — on a new UTC date, the daily baseline resets and the trading-day counter increments.
2. **Peak update** — `PeakEquity` tracks the high-water mark (for trailing drawdown).
3. **Breach checks (fail first):**
   - *Daily loss* — `dailyStartEquity − equity ≥ dailyStartEquity × maxDailyLoss%`.
   - *Total drawdown* — `reference − equity ≥ reference × maxDrawdown%`, where `reference` is the starting
     balance (static) or the peak equity (trailing).
4. **Pass check** — when `equity − startingBalance ≥ startingBalance × profitTarget%` **and**
   `tradingDays ≥ minTradingDays`: an Evaluation phase advances to Verification (baseline resets); a
   Verification pass (or a single-step Evaluation pass) advances to Funded and the challenge is `Passed`.

Domain events: `PropFirmChallengeStarted`, `PropFirmPhasePassed`, `PropFirmChallengePassed`,
`PropFirmChallengeBreached`.

## API (`/api/prop-firm`, feature `PropFirm`, role User+)

| Method | Route | Purpose |
|--------|-------|---------|
| GET | `/challenges` | list the user's challenges |
| GET | `/challenges/{id}` | one challenge |
| POST | `/challenges` | create (name, account, starting balance, rules) |
| POST | `/challenges/{id}/equity` | record an equity snapshot → re-evaluates rules |
| DELETE | `/challenges/{id}` | soft-delete |

UI: `/prop-firm` (nav *Prop Firm*, gated by the `PropFirm` feature flag) lists challenges and creates them
through the `NewPropFirmChallengeDialog`.

## ⚠ Known gap — no live equity feed

The app currently has **no live account P&L feed** (see the copy-trading and backtest scope): equity is only
available from backtests and tracked copy-trading state. The rules are modelled and enforced **in full**, but
they only advance when equity is supplied via `RecordEquity` / `POST …/equity`. When a live equity feed is
added, a background evaluator should poll the source and call `RecordEquity` per account — the domain needs no
change. Until then, equity is recorded manually or by whatever process feeds it.

## Tests

- **Unit** — `UnitTests/PropFirm/PropFirmChallengeTests.cs` (phase advance, single/two-step, min-days gating,
  daily-loss breach, static + trailing drawdown breach, terminal-state guard) and
  `PropFirmValueObjectTests.cs` (VO ranges). Timestamps are explicit — no wall-clock reads.
- **Integration** — `IntegrationTests/PropFirmChallengePersistenceTests.cs`: round-trip + record-equity +
  soft-delete against real Postgres.
- **E2E** — `E2ETests/PropFirmTests.cs`: create a challenge, see it in the page, record equity to `Passed`.
