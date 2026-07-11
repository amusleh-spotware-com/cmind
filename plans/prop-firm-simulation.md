# Plan — Full Prop-Firm Challenge Simulation

Status: **PLAN ONLY — not implemented.** Author target: cMind main.
Feature flag: `PropFirmSimulation` (extends existing `PropFirm` flag; see §10).

---

## 0. One-paragraph summary

Turn existing *manual-equity* `PropFirmChallenge` aggregate into **live, node-hosted prop-firm
simulation**. User creates custom challenge (any industry rule shape), binds to `TradingAccount`,
**starts** it like copy-trading profile. Node claims challenge on self-healing lease, opens read-only
cTrader Open API session for account, **computes live equity** (`balance + Σ unrealized P&L`) from
execution + spot streams, feeds equity snapshots into aggregate. Aggregate owns every rule decision,
raises domain events on breach / phase pass / final pass. Events fan out to **Alerts** context (notify
user), end + mark challenge `Failed` / `Passed`. All covered by unit + integration + E2E +
deterministic-simulation stress tests; cTrader fake extended to drive equity faithfully.

**Reuse, don't reinvent:** live host, node lease, supervisor, token-swap, reconnect/resync machinery
already exist for copy trading (`CopyEngineHost`, `CopyEngineSupervisor`, `CopyProfile` lease methods).
Prop-firm tracker = **same shape**, different payload (read-only equity tracking not order mirroring).

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
- **Trailing (percent)** — floor trails peak **equity** by %  — shrinks buffer on winning days.
- **Trailing threshold (dollar)** — futures-style: floor trails equity peak by **fixed dollar amount**
  until equity reaches defined profit threshold, then **locks at initial balance**. (Apex/Topstep.)

**Daily drawdown / daily loss limit** — resets at fixed server time (default 00:00 UTC), two methods:
- **Balance-based** — measured from balance at day start (realized only). Beginner-friendly.
- **Equity-based (intraday)** — measured from equity incl. floating P&L real time. Brutal; ~71% of
  Phase-1 failures. Some firms track **intraday high-water** floor (daily floor rises with intraday peak).

**Other common rules** (must be modellable):
- **Consistency rule** — single day's profit must not exceed X% (e.g. 30–40%) of total profit.
- **Minimum trading days** — N distinct days with activity before pass counts.
- **Maximum challenge duration** — calendar-day time limit (e.g. 30/60 days); unlimited now common.
- **News-trading lockout** — no positions opened/closed within ±N min of high-impact news.
- **Weekend/overnight holding ban** — positions must be flat by session/weekend close.
- **Inactivity** — challenge voided after N days no trade.
- **Max lot / max positions** — per-symbol or account exposure caps.
- **Funded scaling & profit split** — after funded, balance scales on milestones; trader keeps 80–90%.

Breach semantics: **hard, immediate, no warning** — first breached rule ends challenge; funded breach
revokes access. We model breach same way (first breach wins, terminal).

Sources:
- [The5ers — Drawdown rules (daily/max/trailing) 2026](https://the5ers.com/prop-firm-drawdown-rules-explained-daily-max-and-trailing-limits-in-2026/)
- [KenMacro — Prop-firm drawdown rules 2026](https://kenmacro.com/prop-firm-drawdown-rules-explained-2026/)
- [TradeClaris — How challenges work: phases, rules](https://www.tradeclaris.com/blogs/how-prop-firm-challenges-work-rules-phases-and-why-80-fail)
- [FundedNext — basic prop-firm rules](https://fundednext.com/blog/prop-firm-trading-rules)
- [FXIFY — 30% consistency rule](https://tradingfinder.com/props/fxify/rules/)
- [LuxAlgo — CFD prop-firm challenges compared](https://www.luxalgo.com/prop-firms/cfds/challenges/)
- [Industry Spread — regulation 2026 (simulated-account model)](https://theindustryspread.com/retail-prop-trading-regulation-2026-my-forex-funds-cftc/)

### 1.2 cTrader Open API — how to compute live account stats

- **Balance** given directly: `ProtoOATrader.balance` (via `ProtoOATraderReq/Res`), scaled by `moneyDigits`.
- **Equity NOT given** — compute: `Equity = Balance + Σ(unrealized P&L of open positions)`.
- **Unrealized P&L** per position from entry price + live bid/ask; must convert quote→deposit currency,
  include `commission`, `mirroringCommission`, `swap`.
- **Live pricing**: subscribe `ProtoOASubscribeSpotsReq` → `ProtoOASpotEvent` per held symbol for bid/ask.
- **Positions**: reconcile via `ProtoOAReconcileReq` + keep current with `ProtoOAExecutionEvent`.
- **Margin / free margin**: each `ProtoOAPosition.usedMargin` (deposit ccy) → `FreeMargin = Equity − Σ usedMargin`,
  `MarginLevel = Equity / Σ usedMargin`. Stop-out margin-based, independent of our rules.

App **already** has: `LoadBalanceAsync`, `ReconcileAsync` (open positions), spot subscription,
`LoadSpotPriceAsync`, `LoadSymbolDetailsAsync`, token swap, reconnect callback
(`IOpenApiTradingSession`). We only add **read-only equity computation** on top (§4.3).

Sources:
- [cTrader Open API — Calculating Profit/Loss](https://help.ctrader.com/open-api/profit-loss-calculation/)
- [cTrader Open API — Model messages](https://help.ctrader.com/open-api/model-messages/)
- [cTrader forum — how to get account equity](https://community.ctrader.com/forum/connect-api-support/23513/)
- [spotware/openapi-proto-messages](https://github.com/spotware/openapi-proto-messages/blob/main/OpenApiModelMessages.proto)

---

## 2. Ubiquitous language (additions to the PropFirm context)

| Term | Meaning |
|------|---------|
| **Challenge** | Prop-firm evaluation program bound to one `TradingAccount` (aggregate root, existing). |
| **Challenge template / kind** | Named rule preset: `OnePhase`, `TwoPhase`, `ThreePhase`, `InstantFunding`. |
| **Phase** | Stage with own target/rules. Ordered list, last = `Funded`. |
| **Rule** | Single evaluable constraint (profit target, daily loss, max drawdown, consistency, …). |
| **Drawdown structure** | `Static`, `TrailingPercent`, `TrailingThresholdDollar`. |
| **Daily-loss basis** | `Balance` or `Equity` (intraday). |
| **Equity snapshot** | `(equity, balance, floatingPnL, now)` fed to aggregate. |
| **Tracker / tracking engine** | Node-hosted worker streaming account, feeding equity (execution context). |
| **Breach** | First rule violation → terminal `Failed` with `BreachReason`. |
| **Objective** | Must-satisfy pass condition (target reached, min days met, consistency ok). |

Not "job/config/server". Challenge never a "backtest"; tracker never a "bot".

---

## 3. Domain model — `Core.PropFirm` (Execution-adjacent, but pure domain)

Current aggregate anemic-ish, single-rule-shaped. **We touch it, so encapsulate + enrich it**
(brownfield rule). Redesign into phase/rule model, keep existing simple path working.

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
  `int? MaxOpenPositions`, `Lot? MaxLot`. (Reuse `Symbol`/`Lot` VOs where exist.)
- **`PhaseRules`** — aggregates above for one phase:
  `(ProfitTarget target, DailyLossLimit dailyLoss, DrawdownLimit drawdown, TradingWindow window,
    ConsistencyRule? consistency, BehaviourRules behaviour)`.
- **`ChallengeBlueprint`** — ordered `IReadOnlyList<PhaseRules>` + `ChallengeKind` + `ProfitSplitPercent`
  (funded). Factory `ChallengeBlueprint.OnePhase(...)`, `.TwoPhase(...)`, `.ThreePhase(...)`,
  `.InstantFunding(...)` build valid presets; fully-custom ctor validates invariants (≥1 phase, last
  phase = funded semantics, targets/limits in range). All throw `DomainException` on bad input.

All VOs immutable, equality by value, validate in ctor — matches `Core/StrongIds.cs` style. **No new
primitive-obsessed signatures** crossing boundary.

### 3.2 Enums (`PropFirmEnums.cs`)

- `ChallengeKind { OnePhase, TwoPhase, ThreePhase, InstantFunding, Custom }`
- Extend `DrawdownMode` → replaced by `DrawdownLimit` VO (keep enum `DrawdownStructure { Static, TrailingPercent, TrailingThresholdDollar }` for persistence discriminator).
- `DailyLossBasis { Balance, Equity }`
- Extend `BreachReason { None, DailyLoss, MaxDrawdown, Consistency, TimeLimit, Inactivity, WeekendHolding, NewsTrading, MaxExposure }`
- `ChallengeStatus { Draft, Active, Passed, Failed, Stopped }` (add `Draft`/`Stopped` for lifecycle, §3.4).
- Keep `ChallengePhase` but generalize: store **phase index** + computed `IsFunded`.
- `ChallengeRunState { Idle, Assigned, Running, Reconnecting, Ended }` (host/lease state, mirrors copy).

### 3.3 Aggregate `PropFirmChallenge` — rich, phase-aware

Owns: identity (`UserId`, `TradingAccountId`), `Name`, `StartingBalance`, `ChallengeBlueprint`,
current `PhaseIndex`, `Status`, `Breach`, live tracking state (`CurrentEquity`, `CurrentBalance`,
`PeakEquity`, `DailyStartEquity`/`DailyStartBalance`, `DailyPeakEquity`, `CurrentDay`, `TradingDaysCount`,
`StartedAt`, per-day realized-profit history for consistency, `LastEquityAt`), lease fields
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
  2. **Day roll** at phase's `resetTime`: on new day, snapshot yesterday's realized profit into
     history, reset `DailyStartEquity`/`DailyStartBalance`/`DailyPeakEquity`, increment
     `TradingDaysCount` if day had activity.
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
- `RecordTradingActivity(TradingSignal signal, now)` — feeds non-equity facts tracker observes
  (trade opened during news window, position held over weekend, exposure). Kept separate so pure
  equity path stays clean; rules read latest signals. (Alternative: fold into `EquitySnapshot`.)
- Guard: any method throws `DomainException` when terminal (`Passed`/`Failed`).

**Rule evaluation lives on VOs** (`DrawdownLimit.IsBreached(state)`,
`DailyLossLimit.IsBreached(state)`, `ConsistencyRule.IsSatisfied(profitHistory)`), so aggregate
orchestrates, each rule owns its math — no rule `if` leaks into endpoints/host.

### 3.4 Domain events (extend existing)

`PropFirmChallengeStarted`, `PropFirmChallengeStopped`, `PropFirmPhasePassed(phaseIndex)`,
`PropFirmChallengePassed`, `PropFirmChallengeBreached(reason)`, plus **`PropFirmDrawdownWarning(percentUsed)`**
(soft alert at e.g. 80% of limit — nice-to-have). Dispatched after `SaveChanges` via existing
domain-event dispatcher; **Alerts** subscribes (§5).

### 3.5 `EquitySnapshot` VO

`record EquitySnapshot(Money Equity, Money Balance, MoneyAmount FloatingPnL, Money UsedMargin)`.
Constructed by tracker from Open API data; aggregate never reads socket.

---

## 4. Live tracking — node-hosted engine (Execution context)

Mirror copy-trading hosting stack **exactly**; prop tracker = read-only cousin of `CopyEngineHost`.

### 4.1 `PropFirmTrackingSupervisor` (`src/Nodes/PropFirm/`)

`BackgroundService` on every node (like `CopyEngineSupervisor`). Loop:
1. Query challenges with `Status == Active` whose lease free or stale (`LeaseExpiresAt <= now`).
2. Claim via `challenge.ClaimBy(node, now + leaseTtl)` → single-aggregate `SaveChanges`.
3. Spin up `PropFirmTrackingHost` per claimed challenge; renew leases on timer; stop hosts whose
   challenge left `Active` (passed/failed/stopped) or whose lease lost.
Node death → lease lapses → another node reclaims. Same self-heal guarantees, same tests.

### 4.2 `PropFirmTrackingHost` (`src/PropFirmEngine/` — new project mirroring `src/CopyEngine/`)

Owns one `IOpenApiTradingSession` (read-only usage), account token (with in-place
`SwapAccessTokenAsync` on rotation, reusing token-lifecycle machinery), reconnect/resync guard
(`_stateGate`, learned from copy-trading startup-race fix). Responsibilities:
1. On start: attach account, `LoadBalanceAsync`, `ReconcileAsync` open positions, `LoadSymbolDetailsAsync`,
   subscribe spots for held symbols.
2. On every `ExecutionEvent`: update in-memory position book + realized balance; (re)subscribe spots for
   new symbols; detect news-window / weekend / exposure signals → `RecordTradingActivity`.
3. On every `ProtoOASpotEvent` (throttled, e.g. ≤ 1 snapshot/sec via `TimeProvider`): recompute equity,
   call `challenge.RecordEquity(snapshot, timeProvider.GetUtcNow())` → persist single aggregate → dispatch
   events. Throttle avoids write storms while staying breach-accurate (breach also checked every tick
   in-memory; persist on change or interval).
4. On reconnect: re-run load+reconcile+resubscribe under `_stateGate` (no concurrent resync).
5. On terminal status: unsubscribe, dispose session, release lease.

**No domain logic in host** — only produces `EquitySnapshot`/`TradingSignal`, calls aggregate.

### 4.3 ACL extension — equity computation (`src/CTraderOpenApi`)

Add to `IOpenApiTradingSession` (Infrastructure/ACL, not Core):
- `Task<TraderSnapshot> LoadTraderSnapshotAsync(ctid, ct)` → balance + moneyDigits + Σ usedMargin.
- Reuse existing spot subscription; add `IAsyncEnumerable<SpotTick> SourceSpotsAsync(ctid, ct)` exposing
  `(symbolId, bid, ask)` so host can revalue.
- **`PropFirmEquityCalculator`** (pure, in `src/PropFirmEngine` or `CTraderOpenApi.Client`): given
  position book + latest spots + symbol details + deposit-ccy conversion rates, returns
  `(equity, floatingPnL, usedMargin)`. Includes commission/swap fields. **Unit-testable in isolation** —
  where currency-conversion bugs hide, so gets own thorough test class.

### 4.4 Signals for behaviour rules

`TradingSignal` (`opened/closed positionId, symbol, at, volume`) + `NewsCalendar` abstraction
(`INewsCalendar.IsHighImpactWindow(symbol, at, buffer)`) — start with static/configurable calendar
(Infrastructure impl), interface in Core so rules stay pure. Weekend/holding detection from position
open-times vs session close (config-driven). Exposure from position book.

---

## 5. Alerts integration

On `PropFirmChallengePassed` / `PropFirmChallengeBreached` / `PropFirmDrawdownWarning`, handler in
Alerts context raises `AlertEvent` on user's `AlertRule` (or dedicated system rule), reusing existing
alert delivery (in-app/SignalR + whatever channels exist). **Cross-aggregate reaction = domain
event → second use-case**, never fat transaction. Wire via existing `SavingChanges`/dispatcher path.
No new alert transport invented; if none exists for channel, surface in-app + log (`LogMessages`).

---

## 6. Application layer — endpoints & DTOs (`src/Web/Endpoints/PropFirmEndpoints.cs`)

Extend existing group (`/api/prop-firm`, feature `PropFirm`, role User+):

| Method | Route | Purpose |
|--------|-------|---------|
| GET | `/challenges` | list user's challenges (+ live status, phase, equity, %-to-target, %-drawdown-used) |
| GET | `/challenges/{id}` | full detail incl. blueprint + live tracking state |
| GET | `/templates` | built-in `ChallengeKind` presets (for create dialog) |
| POST | `/challenges` | create from template **or** fully custom blueprint (+ account, starting balance) |
| POST | `/challenges/{id}/start` | start tracking (Draft/Stopped → Active) |
| POST | `/challenges/{id}/stop` | stop tracking (→ Stopped, release lease) |
| POST | `/challenges/{id}/equity` | **manual** equity snapshot (kept — for demo/tests/no-live-feed) |
| DELETE | `/challenges/{id}` | soft-delete (blocked while Active) |

DTOs edge-only (don't rename domain concepts). List projection = **read model** (query EF directly,
CQRS-lite) — never reshape aggregate for screen. Endpoints orchestrate: load aggregate → call method
→ save; **no rule `if`** in endpoint.

Also expose same via **MCP** (`Mcp/Tools/PropFirmTools.cs`): `create_challenge`, `list_challenges`,
`start`/`stop`, `challenge_status` — parity with copy/instance tools.

---

## 7. UI (Blazor) — `src/Web/Components/Pages`

- **`PropFirm.razor`** (existing `/prop-firm`, nav under sensible group, gated by flag): challenge list
  with live columns (phase, status chip, equity, % to target, daily-loss used, max-DD used), **row
  actions Start/Stop/Delete** exactly like Copy Trading page (start enabled when not Active, stop when
  Active, delete disabled while Active + confirm dialog).
- **`NewPropFirmChallengeDialog.razor`** (extend existing): step 1 pick **template** (`OnePhase`/`TwoPhase`/
  `ThreePhase`/`InstantFunding`/`Custom`); step 2 pick **trading account** + starting balance; step 3
  **rule editor** — per-phase profit target, drawdown structure (static/trailing-%/trailing-$), daily-loss
  (limit + basis + reset time), min/max days, consistency %, behaviour toggles (weekend/news/max lot/max
  positions). Full client + server validation; returns `NewPropFirmChallengeResult` record; page does
  POST then reloads. **Dialog-based create — never inline page form** (UI mandate).
- **`PropFirmChallengeDetail.razor`** — one challenge: live equity chart, phase progress, each rule with
  progress bar (used vs limit), breach/pass banner, event timeline. Reuse InstanceDetail chart patterns.
- Live updates via SignalR (reuse dashboard/logs hub pattern) so status/equity update without refresh.

All add/edit through MudBlazor dialogs (`IDialogService.ShowAsync`), per UI guide.

---

## 8. Persistence (`src/Infrastructure/Persistence`)

- Map enriched aggregate: blueprint stored as owned/JSON column (phase list + rules) or child rows.
  Prefer **owned JSON** for blueprint (immutable, read whole) + scalar columns for live tracking state
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
Register supervisor as hosted service on nodes (like `CopyEngineSupervisor`), gated by flag.
`INewsCalendar` + `PropFirmEquityCalculator` DI wiring in `Infrastructure/DependencyInjection.cs` and
node composition.

---

## 10. Feature flag

Reuse/extend existing `PropFirm` feature toggle; add sub-capability `PropFirmSimulation` if we want to
ship domain model before live engine. API + nav + supervisor all gate on it. Unset → feature invisible,
no tracker runs, app unchanged (parity with AI-key-unset behaviour).

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
- `PropFirmTrackingHost` unit tests against **extended fake session** (§11.4): start→snapshot→breach,
  reconnect→resync-under-gate (no double resync), token swap mid-run, throttle behaviour, terminal cleanup.
  All time via `FakeTimeProvider`; explicit timestamps only.

### 11.2 Integration (`tests/IntegrationTests/PropFirm`, real Postgres/Testcontainers)

- Blueprint round-trip (all kinds) incl. owned JSON / child rows survive save+load.
- `RecordEquity` persistence: equity/peak/day/history/lease columns persist and reload correctly.
- Lease claim + stale-reclaim across two simulated node identities on real Postgres (mirror
  `CopyLive` node-affinity test).
- Soft-delete + blocked-delete-while-Active.
- Domain-event → Alerts handler writes `AlertEvent` (integration of dispatcher path).
- Token-version propagation for tracker session (reuse copy token tests shape).

### 11.3 E2E (`tests/E2ETests/PropFirmTests.cs`, drive API + UI)

- Create each template via dialog; see it listed.
- Full custom challenge via dialog → persisted with correct rules.
- Start → Active; Stop → Stopped (lifecycle buttons + state).
- **Manual-equity happy path**: record snapshots to `Passed` (two-phase: through Verification→Funded).
- **Manual-equity breach path**: record snapshots that breach each headline rule → `Failed` + reason shown +
  **alert raised** (assert alert surface).
- With **fake/stub live session** wired in E2E host: start challenge, feed stub's execution +
  spot stream, assert equity tracks and challenge reaches Passed/Failed **without manual equity** —
  end-to-end proof of live path (real cTrader creds not required in CI; live tier optional).
- Detail page: live chart + per-rule progress + pass/fail banner render.

### 11.4 Fake simulator extension (`tests/UnitTests/CopyTrading/FakeTradingSession.cs`)

Extend cTrader-faithful fake (do **not** weaken it) to also serve tracker:
- Expose **balance**, **open-position book with entry price**, **spot feed** host subscribes to, so
  it can compute equity faithfully (incl. swap/commission fields, quote≠deposit symbols).
- Drive floating P&L by scripting spot ticks; support partial close, SL/TP hit, disconnect→desync→resync,
  token swap — reused from existing copy scenarios so equity reacts to same faithful events.
- Add helper to script **equity trajectory** (ticks that push equity to target/breach) for host tests.

### 11.5 Stress / DST (`tests/StressTests`)

Extend deterministic-simulation suite:
- New workload: N challenges (mixed kinds/rules) tracked concurrently across simulated multi-node cluster,
  seeded randomized spot trajectories + fault injection (socket flap, spot gap, token rotation, **node death
  + lease reclaim**, out-of-order/duplicate ticks).
- Drive to quiescence; assert invariants: exactly-once terminal transition per challenge, no challenge stuck
  `Active` after trajectory guarantees breach/pass, no double-tracking (single lease holder), no
  concurrent resync corruption (copy-trading race lesson), equity monotonic-consistency vs scripted P&L.
- Reuse DST harness that already found `CopyEngineHost` startup-resync race.

**Definition of done:** `dotnet test` green (all tiers incl. pre-existing), 0 new warnings, Rider
`get_file_problems` clean on every touched `.cs`/`.razor`, `caveman:cavecrew-reviewer` pass on diff.

---

## 12. Docs

- Rewrite `docs/features/prop-firm.md` to cover: rule taxonomy, live tracking engine, node hosting/lease,
  alerting, lifecycle, all challenge kinds, equity-computation ACL, removed "no live feed" gap.
- Update `docs/features/README.md` index and any nav/ops doc if supervisor changes deployment topology
  (node now also runs prop tracker — note in `docs/deployment/scaling.md`).
- Keep doc + code in **same commit** (docs mandate).

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
- **News calendar source** — start with configurable static calendar (`INewsCalendar`); real feed is
  follow-up. News/weekend rules optional per challenge so absence doesn't block.
- **Write frequency** — spot ticks high-rate; throttle persistence (breach still evaluated every tick
  in-memory) to avoid DB pressure. Tune via `PropFirmOptions.EquitySnapshotThrottle`.
- **Funded phase** — model as terminal `Passed` with `ProfitSplitPercent` recorded; ongoing funded-account
  payout/scaling tracking is **possible follow-up** (out of scope for "complete the challenge"), flag it.
- **Blueprint persistence shape** — owned JSON vs child tables: JSON simpler + immutable-read; confirm with
  EF review. Migration must be additive/back-compatible with existing `PropFirmChallenge` rows (provide
  defaults mapping old columns → `TwoPhase`/single-step blueprint).
- **Live E2E in CI** — real cTrader demo creds gated to optional live tier (as copy trading already is);
  CI proves path with fake/stub session.

---

## 15. DDD definition-of-done checklist (must all hold)

- [ ] New rules live on aggregate/VOs/domain services — not endpoints/host/supervisor.
- [ ] No new public setters; state via intention methods guarding invariants.
- [ ] `TradingAccount` referenced by strong id only; no new cross-aggregate nav.
- [ ] Each `SaveChanges` mutates one aggregate; Alerts reaction via domain event → second use-case.
- [ ] No primitive-obsessed domain signatures; new concepts are VOs.
- [ ] Invariant violations throw Core `DomainException`.
- [ ] Ubiquitous-language names; Core compiles with zero infra deps.
- [ ] Unit tests assert invariants/transitions; touched anemic parts left more encapsulated.
- [ ] `TimeProvider` only; no `DateTime.UtcNow`.
- [ ] All tiers green; docs in same commit.