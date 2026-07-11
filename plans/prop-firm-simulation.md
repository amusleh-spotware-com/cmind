# Plan — Full Prop-Firm Challenge Simulation

Status: **PLAN ONLY — not implemented.** Author target: cMind main.
Feature flag: `PropFirmSimulation` (extends existing `PropFirm` flag; see §10).

---

## 0. One-paragraph summary

Turn the existing *manual-equity* `PropFirmChallenge` aggregate into a **live, node-hosted
prop-firm simulation**. A user creates a custom challenge (any industry rule shape), binds it to a
`TradingAccount`, and **starts** it like a copy-trading profile. A node then claims the challenge on a
self-healing lease, opens a read-only cTrader Open API session for the account, **computes live equity**
(`balance + Σ unrealized P&L`) from execution + spot streams, and feeds equity snapshots into the
aggregate. The aggregate owns every rule decision and raises domain events on breach / phase pass / final
pass. Those events fan out to the **Alerts** context (notify the user) and end + mark the challenge
`Failed` / `Passed`. Everything is covered by unit + integration + E2E + deterministic-simulation stress
tests, and the cTrader fake is extended to drive equity faithfully.

**Reuse, don't reinvent:** the live host, node lease, supervisor, token-swap, and reconnect/resync
machinery already exist for copy trading (`CopyEngineHost`, `CopyEngineSupervisor`, `CopyProfile`
lease methods). The prop-firm tracker is the **same shape** with a different payload (read-only equity
tracking instead of order mirroring).

---

## 1. Research findings

### 1.1 Retail FX/CFD prop-firm rule taxonomy (2026)

Challenge **structures**:
- **One-phase / single evaluation** — pass one target → funded. Tighter drawdown.
- **Two-phase** — classic Phase 1 (Evaluation) + Phase 2 (Verification), then Funded.
- **Three-phase** — progressively smaller targets (~6%/phase); more relaxed drawdown.
- **Instant funding** — no evaluation; funded immediately, strictest drawdown.

**Profit target** — % of starting balance, typically 5–10%, can differ per phase (Phase 2 target often
lower or 0). Funded phase has **no** profit target (payout instead).

**Maximum drawdown** (peak-to-trough loss) — three real-world structures:
- **Static** — fixed floor from starting balance, never moves.
- **Trailing (percent)** — floor trails peak **equity** by a % — shrinks buffer on winning days.
- **Trailing threshold (dollar)** — futures-style: floor trails equity peak by a **fixed dollar amount**
  until equity reaches a defined profit threshold, then **locks at the initial balance**. (Apex/Topstep.)

**Daily drawdown / daily loss limit** — resets at a fixed server time (default 00:00 UTC), two methods:
- **Balance-based** — measured from balance at day start (realized only). Beginner-friendly.
- **Equity-based (intraday)** — measured from equity incl. floating P&L in real time. Brutal; ~71% of
  Phase-1 failures. Some firms track an **intraday high-water** floor (daily floor rises with intraday peak).

**Other common rules** (must be modellable):
- **Consistency rule** — a single day's profit must not exceed X% (e.g. 30–40%) of total profit.
- **Minimum trading days** — N distinct days with activity before a pass counts.
- **Maximum challenge duration** — calendar-day time limit (e.g. 30/60 days); unlimited is now common.
- **News-trading lockout** — no positions opened/closed within ±N min of high-impact news.
- **Weekend/overnight holding ban** — positions must be flat by session/weekend close.
- **Inactivity** — challenge voided after N days with no trade.
- **Max lot / max positions** — per-symbol or account exposure caps.
- **Funded scaling & profit split** — after funded, balance scales on milestones; trader keeps 80–90%.

Breach semantics: **hard, immediate, no warning** — first breached rule ends the challenge; funded
breach revokes access. We model breach the same way (first breach wins, terminal).

Sources:
- [The5ers — Drawdown rules (daily/max/trailing) 2026](https://the5ers.com/prop-firm-drawdown-rules-explained-daily-max-and-trailing-limits-in-2026/)
- [KenMacro — Prop-firm drawdown rules 2026](https://kenmacro.com/prop-firm-drawdown-rules-explained-2026/)
- [TradeClaris — How challenges work: phases, rules](https://www.tradeclaris.com/blogs/how-prop-firm-challenges-work-rules-phases-and-why-80-fail)
- [FundedNext — basic prop-firm rules](https://fundednext.com/blog/prop-firm-trading-rules)
- [FXIFY — 30% consistency rule](https://tradingfinder.com/props/fxify/rules/)
- [LuxAlgo — CFD prop-firm challenges compared](https://www.luxalgo.com/prop-firms/cfds/challenges/)
- [Industry Spread — regulation 2026 (simulated-account model)](https://theindustryspread.com/retail-prop-trading-regulation-2026-my-forex-funds-cftc/)

### 1.2 cTrader Open API — how to compute live account stats

- **Balance** is given directly: `ProtoOATrader.balance` (via `ProtoOATraderReq/Res`), scaled by `moneyDigits`.
- **Equity is NOT given** — compute it: `Equity = Balance + Σ(unrealized P&L of open positions)`.
- **Unrealized P&L** per position from entry price + live bid/ask; must convert quote→deposit currency and
  include `commission`, `mirroringCommission`, and `swap`.
- **Live pricing**: subscribe `ProtoOASubscribeSpotsReq` → `ProtoOASpotEvent` per held symbol for bid/ask.
- **Positions**: reconcile via `ProtoOAReconcileReq` + keep current with `ProtoOAExecutionEvent`.
- **Margin / free margin**: each `ProtoOAPosition.usedMargin` (deposit ccy) → `FreeMargin = Equity − Σ usedMargin`,
  `MarginLevel = Equity / Σ usedMargin`. Stop-out is margin-based, independent of our rules.

The app **already** has: `LoadBalanceAsync`, `ReconcileAsync` (open positions), spot subscription,
`LoadSpotPriceAsync`, `LoadSymbolDetailsAsync`, token swap, reconnect callback
(`IOpenApiTradingSession`). We only add a **read-only equity computation** on top (§4.3).

Sources:
- [cTrader Open API — Calculating Profit/Loss](https://help.ctrader.com/open-api/profit-loss-calculation/)
- [cTrader Open API — Model messages](https://help.ctrader.com/open-api/model-messages/)
- [cTrader forum — how to get account equity](https://community.ctrader.com/forum/connect-api-support/23513/)
- [spotware/openapi-proto-messages](https://github.com/spotware/openapi-proto-messages/blob/main/OpenApiModelMessages.proto)

---

## 2. Ubiquitous language (additions to the PropFirm context)

| Term | Meaning |
|------|---------|
| **Challenge** | A prop-firm evaluation program bound to one `TradingAccount` (aggregate root, existing). |
| **Challenge template / kind** | Named rule preset: `OnePhase`, `TwoPhase`, `ThreePhase`, `InstantFunding`. |
| **Phase** | A stage with its own target/rules. Ordered list, last = `Funded`. |
| **Rule** | A single evaluable constraint (profit target, daily loss, max drawdown, consistency, …). |
| **Drawdown structure** | `Static`, `TrailingPercent`, `TrailingThresholdDollar`. |
| **Daily-loss basis** | `Balance` or `Equity` (intraday). |
| **Equity snapshot** | `(equity, balance, floatingPnL, now)` fed to the aggregate. |
| **Tracker / tracking engine** | Node-hosted worker that streams the account and feeds equity (execution context). |
| **Breach** | First rule violation → terminal `Failed` with a `BreachReason`. |
| **Objective** | A must-satisfy pass condition (target reached, min days met, consistency ok). |

Not "job/config/server". A challenge is never a "backtest"; the tracker is never a "bot".

---

## 3. Domain model — `Core.PropFirm` (Execution-adjacent, but pure domain)

The current aggregate is anemic-ish and single-rule-shaped. **We touch it, so we encapsulate + enrich it**
(brownfield rule). Redesign into a phase/rule model while keeping the existing simple path working.

### 3.1 New / changed value objects (`PropFirmValueObjects.cs`)

- Keep `Money`, `Percent`, `TradingDayRequirement`.
- **`MoneyAmount`** (signed) — for dollar trailing thresholds (distinct from non-negative `Money`).
- **`ProfitTarget`** — `Percent?` (null = no target, e.g. funded/verification-zero).
- **`DrawdownLimit`** — discriminated: `Static(Percent)`, `TrailingPercent(Percent)`,
  `TrailingThresholdDollar(MoneyAmount trailAmount, Money lockThreshold)`. Self-validating.
- **`DailyLossLimit`** — `(Percent limit, DailyLossBasis basis, TimeOnly resetTime)`.
- **`ConsistencyRule`** — `Percent maxSingleDayShareOfProfit` (e.g. 40%). Optional.
- **`TradingWindow`** — `MinTradingDays`, `MaxCalendarDays?` (duration limit), `MaxInactivityDays?`.
- **`BehaviourRules`** — `bool AllowWeekendHolding`, `bool AllowNewsTrading`, `NewsBufferMinutes?`,
  `int? MaxOpenPositions`, `Lot? MaxLot`. (Reuse `Symbol`/`Lot` VOs where they exist.)
- **`PhaseRules`** — aggregates the above for one phase:
  `(ProfitTarget target, DailyLossLimit dailyLoss, DrawdownLimit drawdown, TradingWindow window,
    ConsistencyRule? consistency, BehaviourRules behaviour)`.
- **`ChallengeBlueprint`** — ordered `IReadOnlyList<PhaseRules>` + `ChallengeKind` + `ProfitSplitPercent`
  (funded). Factory `ChallengeBlueprint.OnePhase(...)`, `.TwoPhase(...)`, `.ThreePhase(...)`,
  `.InstantFunding(...)` build valid presets; a fully-custom ctor validates invariants (≥1 phase, last
  phase = funded semantics, targets/limits in range). All throw `DomainException` on bad input.

All VOs immutable, equality by value, validate in ctor — matches `Core/StrongIds.cs` style. **No new
primitive-obsessed signatures** crossing the boundary.

### 3.2 Enums (`PropFirmEnums.cs`)

- `ChallengeKind { OnePhase, TwoPhase, ThreePhase, InstantFunding, Custom }`
- Extend `DrawdownMode` → replaced by `DrawdownLimit` VO (keep enum `DrawdownStructure { Static, TrailingPercent, TrailingThresholdDollar }` for persistence discriminator).
- `DailyLossBasis { Balance, Equity }`
- Extend `BreachReason { None, DailyLoss, MaxDrawdown, Consistency, TimeLimit, Inactivity, WeekendHolding, NewsTrading, MaxExposure }`
- `ChallengeStatus { Draft, Active, Passed, Failed, Stopped }` (add `Draft`/`Stopped` for lifecycle, §3.4).
- Keep `ChallengePhase` but generalize: store **phase index** + a computed `IsFunded`.
- `ChallengeRunState { Idle, Assigned, Running, Reconnecting, Ended }` (host/lease state, mirrors copy).

### 3.3 Aggregate `PropFirmChallenge` — rich, phase-aware

Owns: identity (`UserId`, `TradingAccountId`), `Name`, `StartingBalance`, `ChallengeBlueprint`,
current `PhaseIndex`, `Status`, `Breach`, live tracking state (`CurrentEquity`, `CurrentBalance`,
`PeakEquity`, `DailyStartEquity`/`DailyStartBalance`, `DailyPeakEquity`, `CurrentDay`, `TradingDaysCount`,
`StartedAt`, per-day realized-profit history for consistency, `LastEquityAt`), and lease fields
(`AssignedNode`, `LeaseExpiresAt`, `RunState`) mirroring `CopyProfile`.

Intention-revealing methods (no public setters):
- `Create(userId, tradingAccountId, name, Money startingBalance, ChallengeBlueprint)` → `Draft`.
- `Start()` / `Pause()`(optional) / `Stop()` — lifecycle; guards on state; raise events.
  - `Start` requires `Draft`/`Stopped`; sets `Active`; raises `PropFirmChallengeStarted`.
  - `Stop` → `Stopped`, releases lease; raises `PropFirmChallengeStopped`.
- Lease (copy-trading parity): `ClaimBy(node, leaseUntil)`, `RenewLease`, `IsLeaseHeldBy(node, now)`,
  `ReleaseAssignment()`, `AssignToNode`.
- **`RecordEquity(EquitySnapshot snapshot, DateTimeOffset now)`** — the heart. Only when `Active`.
  1. Reject out-of-order (`now < LastEquityAt`).
  2. **Day roll** at the phase's `resetTime`: on new day, snapshot yesterday's realized profit into
     history, reset `DailyStartEquity`/`DailyStartBalance`/`DailyPeakEquity`, increment
     `TradingDaysCount` if the day had activity.
  3. Update `PeakEquity`, `DailyPeakEquity`.
  4. **Breach checks (fail-fast, ordered)** delegated to each rule VO:
     - daily loss (basis-aware; equity basis uses intraday floor),
     - max drawdown (static / trailing-percent / trailing-threshold-dollar with lock),
     - consistency (only enforced at pass evaluation, not mid-day),
     - time limit (`now − StartedAt > MaxCalendarDays`),
     - inactivity, weekend-holding, news, exposure (from `TrackingSignals`, §4.4).
     First breach → `Fail(reason)`.
  5. **Objective / pass check** — target reached AND min-days met AND consistency ok:
     advance phase; if funded → `Passed`.
- `RecordTradingActivity(TradingSignal signal, now)` — feeds non-equity facts the tracker observes
  (a trade opened during news window, a position held over weekend, exposure). Kept separate so the pure
  equity path stays clean; rules read the latest signals. (Alternative: fold into `EquitySnapshot`.)
- Guard: any method throws `DomainException` when terminal (`Passed`/`Failed`).

**Rule evaluation lives on the VOs** (`DrawdownLimit.IsBreached(state)`,
`DailyLossLimit.IsBreached(state)`, `ConsistencyRule.IsSatisfied(profitHistory)`), so the aggregate
orchestrates and each rule owns its math — no rule `if` leaks into endpoints/host.

### 3.4 Domain events (extend existing)

`PropFirmChallengeStarted`, `PropFirmChallengeStopped`, `PropFirmPhasePassed(phaseIndex)`,
`PropFirmChallengePassed`, `PropFirmChallengeBreached(reason)`, plus **`PropFirmDrawdownWarning(percentUsed)`**
(soft alert at e.g. 80% of a limit — nice-to-have). Dispatched after `SaveChanges` via the existing
domain-event dispatcher; **Alerts** subscribes (§5).

### 3.5 `EquitySnapshot` VO

`record EquitySnapshot(Money Equity, Money Balance, MoneyAmount FloatingPnL, Money UsedMargin)`.
Constructed by the tracker from Open API data; the aggregate never reads a socket.

---

## 4. Live tracking — node-hosted engine (Execution context)

Mirror the copy-trading hosting stack **exactly**; a prop tracker is a read-only cousin of `CopyEngineHost`.

### 4.1 `PropFirmTrackingSupervisor` (`src/Nodes/PropFirm/`)

`BackgroundService` on every node (like `CopyEngineSupervisor`). Loop:
1. Query challenges with `Status == Active` whose lease is free or stale (`LeaseExpiresAt <= now`).
2. Claim via `challenge.ClaimBy(node, now + leaseTtl)` → single-aggregate `SaveChanges`.
3. Spin up a `PropFirmTrackingHost` for each claimed challenge; renew leases on a timer; stop hosts whose
   challenge left `Active` (passed/failed/stopped) or whose lease was lost.
Node death → lease lapses → another node reclaims. Same self-heal guarantees, same tests.

### 4.2 `PropFirmTrackingHost` (`src/PropFirmEngine/` — new project mirroring `src/CopyEngine/`)

Owns one `IOpenApiTradingSession` (read-only usage), the account token (with in-place
`SwapAccessTokenAsync` on rotation, reusing token-lifecycle machinery), and the reconnect/resync guard
(`_stateGate`, learned from the copy-trading startup-race fix). Responsibilities:
1. On start: attach account, `LoadBalanceAsync`, `ReconcileAsync` open positions, `LoadSymbolDetailsAsync`,
   subscribe spots for held symbols.
2. On every `ExecutionEvent`: update the in-memory position book + realized balance; (re)subscribe spots for
   new symbols; detect news-window / weekend / exposure signals → `RecordTradingActivity`.
3. On every `ProtoOASpotEvent` (throttled, e.g. ≤ 1 snapshot/sec via `TimeProvider`): recompute equity and
   call `challenge.RecordEquity(snapshot, timeProvider.GetUtcNow())` → persist single aggregate → dispatch
   events. Throttle avoids write storms while staying breach-accurate (breach also checked on every tick
   in-memory; persist on change or interval).
4. On reconnect: re-run load+reconcile+resubscribe under `_stateGate` (no concurrent resync).
5. On terminal status: unsubscribe, dispose session, release lease.

**No domain logic in the host** — it only produces `EquitySnapshot`/`TradingSignal` and calls the aggregate.

### 4.3 ACL extension — equity computation (`src/CTraderOpenApi`)

Add to `IOpenApiTradingSession` (Infrastructure/ACL, not Core):
- `Task<TraderSnapshot> LoadTraderSnapshotAsync(ctid, ct)` → balance + moneyDigits + Σ usedMargin.
- Reuse existing spot subscription; add `IAsyncEnumerable<SpotTick> SourceSpotsAsync(ctid, ct)` exposing
  `(symbolId, bid, ask)` so the host can revalue.
- **`PropFirmEquityCalculator`** (pure, in `src/PropFirmEngine` or `CTraderOpenApi.Client`): given
  position book + latest spots + symbol details + deposit-ccy conversion rates, returns
  `(equity, floatingPnL, usedMargin)`. Includes commission/swap fields. **Unit-testable in isolation** —
  this is where currency-conversion bugs would hide, so it gets its own thorough test class.

### 4.4 Signals for behaviour rules

`TradingSignal` (`opened/closed positionId, symbol, at, volume`) + a `NewsCalendar` abstraction
(`INewsCalendar.IsHighImpactWindow(symbol, at, buffer)`) — start with a static/configurable calendar
(Infrastructure impl), interface in Core so rules stay pure. Weekend/holding detection from position
open-times vs session close (config-driven). Exposure from the position book.

---

## 5. Alerts integration

On `PropFirmChallengePassed` / `PropFirmChallengeBreached` / `PropFirmDrawdownWarning`, a handler in the
Alerts context raises an `AlertEvent` on the user's `AlertRule` (or a dedicated system rule), reusing the
existing alert delivery (in-app/SignalR + whatever channels exist). **Cross-aggregate reaction = domain
event → second use-case**, never a fat transaction. Wire via the existing `SavingChanges`/dispatcher path.
No new alert transport invented; if none exists for a channel, surface in-app + log (`LogMessages`).

---

## 6. Application layer — endpoints & DTOs (`src/Web/Endpoints/PropFirmEndpoints.cs`)

Extend the existing group (`/api/prop-firm`, feature `PropFirm`, role User+):

| Method | Route | Purpose |
|--------|-------|---------|
| GET | `/challenges` | list user's challenges (+ live status, phase, equity, %-to-target, %-drawdown-used) |
| GET | `/challenges/{id}` | full detail incl. blueprint + live tracking state |
| GET | `/templates` | the built-in `ChallengeKind` presets (for the create dialog) |
| POST | `/challenges` | create from a template **or** fully custom blueprint (+ account, starting balance) |
| POST | `/challenges/{id}/start` | start tracking (Draft/Stopped → Active) |
| POST | `/challenges/{id}/stop` | stop tracking (→ Stopped, release lease) |
| POST | `/challenges/{id}/equity` | **manual** equity snapshot (kept — for demo/tests/no-live-feed) |
| DELETE | `/challenges/{id}` | soft-delete (blocked while Active) |

DTOs are edge-only (don't rename domain concepts). List projection is a **read model** (query EF directly,
CQRS-lite) — never reshape the aggregate for the screen. Endpoints orchestrate: load aggregate → call method
→ save; **no rule `if`** in the endpoint.

Also expose the same via **MCP** (`Mcp/Tools/PropFirmTools.cs`): `create_challenge`, `list_challenges`,
`start`/`stop`, `challenge_status` — parity with copy/instance tools.

---

## 7. UI (Blazor) — `src/Web/Components/Pages`

- **`PropFirm.razor`** (existing `/prop-firm`, nav under a sensible group, gated by flag): challenge list
  with live columns (phase, status chip, equity, % to target, daily-loss used, max-DD used), and **row
  actions Start/Stop/Delete** exactly like the Copy Trading page (start enabled when not Active, stop when
  Active, delete disabled while Active + confirm dialog).
- **`NewPropFirmChallengeDialog.razor`** (extend existing): step 1 pick a **template** (`OnePhase`/`TwoPhase`/
  `ThreePhase`/`InstantFunding`/`Custom`); step 2 pick **trading account** + starting balance; step 3 the
  **rule editor** — per-phase profit target, drawdown structure (static/trailing-%/trailing-$), daily-loss
  (limit + basis + reset time), min/max days, consistency %, behaviour toggles (weekend/news/max lot/max
  positions). Full client + server validation; returns a `NewPropFirmChallengeResult` record; page does the
  POST then reloads. **Dialog-based create — never an inline page form** (UI mandate).
- **`PropFirmChallengeDetail.razor`** — one challenge: live equity chart, phase progress, each rule with a
  progress bar (used vs limit), breach/pass banner, event timeline. Reuse InstanceDetail chart patterns.
- Live updates via SignalR (reuse the dashboard/logs hub pattern) so status/equity update without refresh.

All add/edit through MudBlazor dialogs (`IDialogService.ShowAsync`), per the UI guide.

---

## 8. Persistence (`src/Infrastructure/Persistence`)

- Map the enriched aggregate: blueprint stored as owned/JSON column (phase list + rules) or child rows.
  Prefer **owned JSON** for the blueprint (immutable, read whole) + scalar columns for live tracking state
  and lease. Per-day realized-profit history → owned collection or JSON.
- Discriminators: `ChallengeKind`, `DrawdownStructure`, `DailyLossBasis`, `BreachReason` as `int`/string.
- Respect soft-delete filter + `AuditedEntity`. **One aggregate per `SaveChanges`.**
- **EF gotchas to honour** (from CLAUDE.md): no cross-aggregate nav to `TradingAccount` (strong id only);
  don't project nav cycles; materialize before filtering on computed props; TPH `OfType` caveat (n/a here,
  single class). Store lease exactly like `CopyProfile` (`AssignedNode` + `LeaseExpiresAt`).
- **Migration**: `dotnet ef migrations add PropFirmSimulation -p src/Infrastructure -s src/Infrastructure -o Persistence/Migrations`.
- `TimeProvider` everywhere; no `DateTime.UtcNow`. Day-roll uses injected `now`.

---

## 9. Config / options (`Core/Options/AppOptions.cs`)

Add `PropFirmOptions` under `App`: `LeaseTtl`, `LeaseRenewInterval`, `EquitySnapshotThrottle`,
`DrawdownWarningThreshold`, `NewsBufferMinutesDefault`, `Enabled` (or reuse `PropFirm` toggle).
Register the supervisor as a hosted service on nodes (like `CopyEngineSupervisor`), gated by the flag.
`INewsCalendar` + `PropFirmEquityCalculator` DI wiring in `Infrastructure/DependencyInjection.cs` and
node composition.

---

## 10. Feature flag

Reuse/extend the existing `PropFirm` feature toggle; add sub-capability `PropFirmSimulation` if we want to
ship the domain model before the live engine. API + nav + supervisor all gate on it. Unset → feature
invisible, no tracker runs, app unchanged (parity with AI-key-unset behaviour).

---

## 11. Test plan — unit + integration + E2E + stress (MANDATORY, all tiers)

### 11.1 Unit (`tests/UnitTests/PropFirm`)

Aggregate invariants & transitions (not getters):
- Blueprint factories build valid `OnePhase/TwoPhase/ThreePhase/InstantFunding`; custom validation throws.
- Phase advance: one/two/three-phase, verification-zero-target, funded transition, min-days gating.
- **Breach matrix** (each isolated + fail-first ordering):
  daily-loss **balance** basis, daily-loss **equity/intraday** basis, static DD, trailing-% DD,
  **trailing-threshold-$** (trails then locks at initial balance), consistency, time-limit, inactivity,
  weekend-holding, news-trading, max-exposure.
- Day-roll at custom reset time; trading-day counting; peak/daily-peak updates.
- Terminal-state guards (record after Passed/Failed throws); out-of-order equity throws.
- Lifecycle: Draft→Active→Stopped→Active; Start/Stop guards; events raised with right payloads.
- Lease: claim / renew / `IsLeaseHeldBy` boundary at expiry (`<= now`) via `FakeTimeProvider`.
- VO ranges (`PropFirmValueObjectTests`): `Percent`, `MoneyAmount`, `DrawdownLimit`, `DailyLossLimit`,
  `ConsistencyRule`, `TradingWindow`, `ChallengeBlueprint` invariants.
- **`PropFirmEquityCalculator`** unit class: long/short P&L, quote≠deposit conversion, commission/swap,
  used-margin, multi-position equity — the currency-math hot spot.
- `PropFirmTrackingHost` unit tests against the **extended fake session** (§11.4): start→snapshot→breach,
  reconnect→resync-under-gate (no double resync), token swap mid-run, throttle behaviour, terminal cleanup.
  All time via `FakeTimeProvider`; explicit timestamps only.

### 11.2 Integration (`tests/IntegrationTests/PropFirm`, real Postgres/Testcontainers)

- Blueprint round-trip (all kinds) incl. owned JSON / child rows survive save+load.
- `RecordEquity` persistence: equity/peak/day/history/lease columns persist and reload correctly.
- Lease claim + stale-reclaim across two simulated node identities on real Postgres (mirror
  `CopyLive` node-affinity test).
- Soft-delete + blocked-delete-while-Active.
- Domain-event → Alerts handler writes an `AlertEvent` (integration of the dispatcher path).
- Token-version propagation for the tracker session (reuse copy token tests shape).

### 11.3 E2E (`tests/E2ETests/PropFirmTests.cs`, drive API + UI)

- Create each template via the dialog; see it listed.
- Full custom challenge via dialog → persisted with correct rules.
- Start → Active; Stop → Stopped (lifecycle buttons + state).
- **Manual-equity happy path**: record snapshots to `Passed` (two-phase: through Verification→Funded).
- **Manual-equity breach path**: record snapshots that breach each headline rule → `Failed` + reason shown +
  **alert raised** (assert alert surface).
- With a **fake/stub live session** wired in the E2E host: start a challenge, feed the stub's execution +
  spot stream, assert equity tracks and the challenge reaches Passed/Failed **without manual equity** — this
  is the end-to-end proof of the live path (real cTrader creds not required in CI; live tier optional).
- Detail page: live chart + per-rule progress + pass/fail banner render.

### 11.4 Fake simulator extension (`tests/UnitTests/CopyTrading/FakeTradingSession.cs`)

Extend the cTrader-faithful fake (do **not** weaken it) to also serve the tracker:
- Expose **balance**, **open-position book with entry price**, and a **spot feed** the host subscribes to, so
  it can compute equity faithfully (incl. swap/commission fields, quote≠deposit symbols).
- Drive floating P&L by scripting spot ticks; support partial close, SL/TP hit, disconnect→desync→resync,
  token swap — reused from the existing copy scenarios so equity reacts to the same faithful events.
- Add a helper to script an **equity trajectory** (ticks that push equity to a target/breach) for host tests.

### 11.5 Stress / DST (`tests/StressTests`)

Extend the deterministic-simulation suite:
- New workload: N challenges (mixed kinds/rules) tracked concurrently across a simulated multi-node cluster,
  seeded randomized spot trajectories + fault injection (socket flap, spot gap, token rotation, **node death
  + lease reclaim**, out-of-order/duplicate ticks).
- Drive to quiescence; assert invariants: exactly-once terminal transition per challenge, no challenge stuck
  `Active` after its trajectory guarantees a breach/pass, no double-tracking (single lease holder), no
  concurrent resync corruption (the copy-trading race lesson), equity monotonic-consistency vs scripted P&L.
- Reuse the DST harness that already found the `CopyEngineHost` startup-resync race.

**Definition of done:** `dotnet test` green (all tiers incl. pre-existing), 0 new warnings, Rider
`get_file_problems` clean on every touched `.cs`/`.razor`, `caveman:cavecrew-reviewer` pass on the diff.

---

## 12. Docs

- Rewrite `docs/features/prop-firm.md` to cover: rule taxonomy, live tracking engine, node hosting/lease,
  alerting, lifecycle, all challenge kinds, the equity-computation ACL, and the removed "no live feed" gap.
- Update `docs/features/README.md` index and any nav/ops doc if the supervisor changes deployment topology
  (node now also runs the prop tracker — note in `docs/deployment/scaling.md`).
- Keep doc + code in the **same commit** (docs mandate).

---

## 13. Phased delivery (each phase ships all applicable test tiers)

1. **Domain enrichment** — blueprint/rules/VOs/enums, enriched `RecordEquity`, events, lifecycle. Unit +
   integration (persistence). *No behaviour change to live yet; manual-equity path still works.*
2. **Equity ACL + calculator** — session snapshot/spot-source extensions + `PropFirmEquityCalculator`. Unit
   (calculator) + fake-session extension.
3. **Tracking host + supervisor** — node hosting, lease, reconnect/resync, token swap. Unit (host vs fake) +
   integration (lease reclaim).
4. **Alerts wiring** — event handlers → `AlertEvent`. Integration.
5. **API + MCP + UI** — endpoints, dialog, list/detail pages, SignalR live updates. E2E.
6. **Stress extension** — DST workload + fault injection. Stress.
7. **Docs + final review** — doc rewrite, reviewer subagent, migration, `get_file_problems` clean.

---

## 14. Risks / open questions (decide before/at implementation)

- **Currency conversion** for P&L across deposit currencies — needs conversion-rate source per symbol
  (available via Open API symbol data). Isolated in `PropFirmEquityCalculator` + heavily unit-tested.
- **News calendar source** — start with a configurable static calendar (`INewsCalendar`); a real feed is a
  follow-up. News/weekend rules optional per challenge so absence doesn't block.
- **Write frequency** — spot ticks are high-rate; throttle persistence (breach still evaluated every tick
  in-memory) to avoid DB pressure. Tune via `PropFirmOptions.EquitySnapshotThrottle`.
- **Funded phase** — model as terminal `Passed` with `ProfitSplitPercent` recorded; ongoing funded-account
  payout/scaling tracking is a **possible follow-up** (out of scope for "complete the challenge"), flag it.
- **Blueprint persistence shape** — owned JSON vs child tables: JSON simpler + immutable-read; confirm with
  EF review. Migration must be additive/back-compatible with existing `PropFirmChallenge` rows (provide
  defaults mapping old columns → a `TwoPhase`/single-step blueprint).
- **Live E2E in CI** — real cTrader demo creds gated to the optional live tier (as copy trading already is);
  CI proves the path with the fake/stub session.

---

## 15. DDD definition-of-done checklist (must all hold)

- [ ] New rules live on aggregate/VOs/domain services — not endpoints/host/supervisor.
- [ ] No new public setters; state via intention methods guarding invariants.
- [ ] `TradingAccount` referenced by strong id only; no new cross-aggregate nav.
- [ ] Each `SaveChanges` mutates one aggregate; Alerts reaction via domain event → second use-case.
- [ ] No primitive-obsessed domain signatures; new concepts are VOs.
- [ ] Invariant violations throw Core `DomainException`.
- [ ] Ubiquitous-language names; Core compiles with zero infra deps.
- [ ] Unit tests assert invariants/transitions; the touched anemic parts left more encapsulated.
- [ ] `TimeProvider` only; no `DateTime.UtcNow`.
- [ ] All tiers green; docs in the same commit.
