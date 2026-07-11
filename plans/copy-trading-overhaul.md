# Copy Trading — Major Improvement & Overhaul Plan

> Status: **proposed** · Author: AI session 2026-07-11 · Scope: `src/Core/CopyTrading`,
> `src/CopyEngine`, `src/Nodes/CopyTrading`, `src/CopyAgent`, `src/Web/Endpoints/CopyEndpoints.cs`,
> `src/Web/Components/Pages` (Copy Trading), tests across all tiers, `docs/features/copy-trading.md`.
>
> Binding constraints: strict **DDD** (`ddd-dotnet` skill), **TimeProvider** (never `DateTime.UtcNow`),
> **all three test tiers** per change, docs in same commit. See root `CLAUDE.md`.

---

## 1. Where we are today (baseline)

The feature is already mature. Current shipped surface:

- **Domain**: `CopyProfile` aggregate (owns `CopyDestination`), lifecycle `Draft → Running → Paused →
  Stopped/Error`, node lease + affinity (`ClaimBy`/`RenewLease`/`IsLeaseHeldBy`), value objects
  (`RiskSettings`, `LotBounds`, `SlippagePips`, `MaxCopyDelay`, `DrawdownPercent`, `SymbolMapEntry`).
- **Engine** (`CopyEngineHost`): mirrors market/market-range opens, partial close, scale-in, full close,
  pending limit/stop/stop-limit place/amend/cancel, SL/TP + trailing, pending expiry, master slippage.
  Labels every copy by source position id (pending by order id) → id-based reconcile after reconnect
  (open-missing / close-orphan, no duplicates). In-place token swap on the live socket. `_stateGate`
  serializes event handling, resync, and token swap.
- **Supervisor** (`CopyEngineSupervisor` on Web local node + `CopyAgent` worker): atomic
  `ExecuteUpdate` lease claim → exactly one node hosts each profile; self-heals on node death via lease
  lapse; pushes rotated tokens into the running host.
- **Sizing**: FixedLot, Lot/Notional multiplier, Proportional Balance/Equity/FreeMargin,
  AutoProportional, FixedRiskPercent, FixedLeverage + min/max bounds + force-min-lot.
- **Filters/options**: direction, reverse (swaps SL↔TP), symbol map, symbol whitelist/blacklist,
  order-type flags, copy-SL/TP/trailing, mirror-partial-close/scale-in, copy-pending-expiry,
  copy-master-slippage.
- **Tests**: unit (`tests/UnitTests/CopyTrading`, `FakeTradingSession`), integration (real Postgres,
  `CopyLive` node-affinity/token-version), E2E (API + UI round-trip + lifecycle), stress/DST
  (`tests/StressTests`, seeded fault injection to quiescence), **live** (real cTrader demo accounts,
  1:1 / 1:many / reverse / cross-cID / partial-close / pending / trailing).

This overhaul does **not** rewrite that. It closes concrete gaps, adds competitor-parity features that
fit our model, and turns the live suite into an **option-matrix + chaos** framework that can catch any
regression without human interaction.

---

## 2. Competitive research (what the market does)

| Platform | Model | Notable features we should learn from | Source |
|----------|-------|----------------------------------------|--------|
| **Duplikium** (Trade Copier) | Cloud copier, broker-agnostic | Multiplier / fixed-lot / **equity-ratio** sizing; **Max Slippage (pips)** + **Max Lag (time)** qualifiers; **Global Account Protection** (equity TP/SL → *Close-Only* / *Frozen* / *Sell-Out*); blacklist/whitelist; reverse; live monitoring alerts on failed order / disconnect; 1–3 ms internal latency | [trade-copier.com/features](https://www.trade-copier.com/features), [propfirm](https://www.trade-copier.com/features/propfirm-trade-copier) |
| **ZuluTrade** | Broker-agnostic aggregator | **ZuluGuard** — auto-exit/unfollow when a provider deviates from a set loss profile; deep provider filtering (drawdown, consistency, recovery); profit-sharing fees | [forexbrokers.com social-copy-trading](https://www.forexbrokers.com/guides/social-copy-trading) |
| **eToro** | Integrated social broker | Risk-rating per provider; one-click copy; multi-asset; virtual/demo copy practice | same |
| **cCopy** (Spotware cTrader Copy) | Native cTrader, Spotware's own solution | Equity-to-equity copying; **execution transparency** (entry/exit vs signal price report); management + performance fees per strategy provider; in-platform strategy marketplace | [brokeree cTrader PAMM](https://brokeree.com/articles/pamm-for-ctrader-for-brokers-and-investors/) |
| **MAM/PAMM (Brokeree/Brokeret)** | Money-manager | **6 allocation methods** (Lot / Percent / Prop-Balance / Prop-Equity / **Equal-Risk** / **P&L**); **fee engine with High-Water Mark**; per-investor Stop-Loss auto-liquidation; **withdrawal-protection** (proportional close + buffer window); provider ratings/reporting | [brokeree MAM](https://fxtrusts.com/solutions/mam), [track360 PAMM/MAM](https://track360.io/blog/pamm-mam-account-software-for-forex-brokers-operator-guide-2026) |
| **FX Blue Personal Trade Copier** (MT4/MT5) | Local/desktop copier | Equity-relative + fixed + multiplier sizing; **symbol filter, magic-number filter, daily trading-hours window**; adjust SL/TP; **invert direction**; partial close; **auto broker-suffix + 2/3DP↔4/5DP price handling**; one-to-many; email alerts; investor-password read-only; `RequireSenderFillWithinMinutes` for pendings | [fxblue copier](https://www.fxblue.com/tools-for-download/fx-blue-personal-trade-copier) |

### 2.1 cMAM (our own prior project) — feature parity audit

Source: **cMAM**, the author's own earlier multi-account-manager project,
`github.com/amusleh-spotware-com/cMAM` (`src/MirroringModule`, `docs/mirroring/slave-settings.md`) — a
WPF desktop app that mirrors one cTrader/MT master onto many slaves. It is **not** a Spotware product
(Spotware's own copy solution is **cCopy**, above). We mine cMAM's rich per-slave setting **set** as a
requirements checklist because it already encodes years of real cTrader-mirroring know-how.

> ⚠️ **Caution — reuse cMAM's features, not its design.** cMAM is our own prior work and is known
> **buggy and unreliable** (WPF desktop, per-terminal engines, MT bridge). We take its *feature ideas*
> only; we do **not** port its engine architecture or carry over its bugs. Every feature we adopt is
> re-implemented from scratch under cMind's model (server-side, DDD aggregates, id-based reconcile,
> `_stateGate`, live test matrix) and must pass our three-tier + chaos suite before it ships. Treat cMAM
> as a requirements checklist, not a reference implementation.

Mapping to ours:

| cMAM slave setting | cMind status | Action |
|--------------------|--------------|--------|
| Volume Type: Multiplier / Auto-Risk-Balance / Auto-Risk-Equity / Fixed Lots | ✅ have (superset) | none |
| **Min Equity** — equity below X → close/cancel all + stop copying, notify | ❌ | **Phase 2** (this *is* our account-protection `StopEquity`; confirms design) |
| Max Slippage | ✅ `SlippagePips` | wire to real latency/price (Phase 0) |
| Order Types filter | ✅ `CopyOrderTypes` | none |
| Trade Side / Reverse | ✅ | none |
| **Sync Open Orders** (toggle: open master's pre-existing trades on start) | ⚠️ always-on resync | make an explicit per-destination toggle |
| **Sync Closed Orders** (close/cancel what master closed while profile was stopped) | ⚠️ always-on resync | make an explicit toggle |
| **Ignore New Trades** (manage existing copies only, open none) | ❌ | add manage-only mode (= Duplikium "Close-Only") |
| **Per-symbol overrides via CSV symbol map**: per-symbol SL/TP copy, Volume Type, Volume Amount, Max/Min Volume, Max Slippage | ⚠️ symbol map is name→name only | enrich `CopySymbolMapEntry` with per-symbol sizing/bounds/protection overrides |
| **Symbol-map CSV import/export + "sample file" generator** (prefilled with master symbols) | ❌ | add CSV import/export + sample endpoint |
| Auto vs manual symbol mapping | ✅ `Normalize` + map | expose the auto/manual choice explicitly |
| Telegram / email notifications (min-equity trigger, failed order, disconnect) | ❌ (we have structured logs + alerts epic) | route new domain events into the existing alert/notification channel |
| Profile "Disabled" status (master/slave link broken) | ✅ `Error` + `CopyProfileNotLinkable` | none |
| cTrader↔MT4/MT5 cross-platform | ❌ | **out of scope** — cMind is cTrader-only by design |

Net new items cMAM surfaces (folded into the roadmap): **Min-Equity stop** (already Phase 2),
**Sync-Open / Sync-Closed / Ignore-New-Trades toggles**, **per-symbol volume/bounds/SL-TP overrides**,
**symbol-map CSV import/export + sample generation**, **notification routing**.

## 3. Documented pain points / historical failure modes (to defend against)

From cross-platform reviews, help centers, and trader forums:

1. **Slippage & latency** — followers get worse fills; cloud copiers add 400–1000 ms; only ~48% of
   copied trades profit vs ~97% for leaders. Slippage 0.1–0.5% per trade, 5–10 pips on news.
   ([copygram slippage/latency](https://copygram.app/blog/education/understanding-slippage-latency-copy-trading))
2. **Duplicate orders / double-copy** — running two copiers, or the **pending→position transition
   sending both a close and an open with the same id** (FX Blue class bug), produces orphans / ghost
   positions. ([forexfactory FX Blue thread](https://www.forexfactory.com/thread/1139695))
3. **Position desync / ghost positions** — leader can't see follower state; partial fills and rejections
   on the slave (but not the master) leave accounts out of sync.
   ([mt4copier pitfalls](https://www.mt4copier.com/copy-trading-pitfalls-every-account-manager-must-avoid/))
4. **Multi-account queue congestion** — copying to N accounts sequentially means later accounts get
   worse fills, or some fill while others reject. ([NinjaTrader forum](https://forum.ninjatrader.com/forum/ninjatrader-8/platform-technical-support-aa/1231250-slow-trade-copier-executions))
5. **Stop-order rejection in fast markets** — a copied stop rejected because price already passed the
   level. (Tradesyncer / Tradecopia help centers)
6. **Symbol mapping failures** — broker suffix mismatch → "unknown symbol", copy silently dropped.
7. **Missed trades while offline** — a copier only replicates what it observes live; an offline gap is
   permanently missed unless reconciled.
8. **Equity-sizing disruption on deposit/withdrawal** — recalculates volumes mid-trade. (Duplikium note)

Our engine already defends #2 (id-based reconcile + `_mirroredPendingOrders` dedupe), #6 (`Normalize`
+ symbol map), and #7 (resync on start/reconnect). This plan closes **#1, #3, #4, #5, #8** and hardens
the rest with explicit regression tests.

### 3.1 Real user complaints → cMind response (the core of this overhaul)

Verbatim complaints mined from Trustpilot, ForexPeaceArmy, the **cTrader community forum**, ForexFactory,
EA Trading Academy, and prop-firm guides. Each maps to a root cause and a concrete cMind action. This is
the "prevent the pain users actually hit" list.

| # | What users complain about (platform) | Root cause | cMind response | Phase |
|---|--------------------------------------|-----------|----------------|-------|
| C1 | **"Ghost trades" — positions on my copy account that the provider never opened** (cCopy); copied positions auto-closed on "rebalance" (eToro) | Equity-to-equity model **rebalances open copies** on any deposit/withdrawal / equity drift | cMind mirrors **discrete master events by position id**, never rebalances an open copy. Make it an **invariant + regression test**: a balance op on either side opens/closes **nothing**. Sizing decided **once at open**. Document as a headline differentiator. | 0 |
| C2 | **Provider stopped → all my open copies force-closed, fees realised** (cCopy) | Stop = liquidate | On profile Stop/Pause, offer a per-profile **on-stop policy**: `LeaveOpen` (default) vs `CloseAll`. Never force-close silently. | 2 |
| C3 | **ROI mismatch — provider makes 4%, I make 2%** (cCopy) | Leverage/stop-out/lot-rounding differences; micro-lot rounds to zero | **Pre-flight margin/leverage check** per destination; surface `size_zero` skips to the UI (already logged, not shown); leverage-aware margin-safe sizing; warn when proportional size rounds below min lot. | 0/1 |
| C4 | **Delayed execution, no Deal Info / Market Snapshot to see why my price differed** (cCopy) | No execution-transparency tooling | **Execution-transparency report**: per copy, slave entry vs master signal price + measured latency + realized slippage. (cCopy's own missing feature.) | 3 |
| C5 | **Accounts silently disconnected; open positions orphaned, closed by SL; no notification** (ZuluTrade) | No host-health alerting; orphan left unmanaged | Host-down / disconnect **alerts** via existing notification channel; supervisor self-heal already reclaims lease; add **on-disconnect orphan policy**. | 1/2 |
| C6 | **Leaders trade on demo / open a new account to hide losses; not vetted** (ZuluTrade) | Marketplace with no verification | If/when we expose a **provider marketplace**: live-only providers, verified track record, drawdown/consistency stats. Not relevant to own-account copying. | 3 (marketplace) |
| C7 | **Copier doesn't enforce prop-firm rules (daily loss, trailing drawdown); blew my challenge** | Copiers replicate, don't police | **Per-destination prop-rule guard**: daily-loss cap, max & **trailing** drawdown, tracked in **real time** against live equity → **auto-flatten + lockout** for the day. | 2c |
| C8 | **Wish: "Flatten All" panic button** (prop traders) | No instant kill switch | **Flatten-All** endpoint + UI button: close every copied position on one/all destinations instantly. | 2c |
| C9 | **Wish: per-account independent risk rules; lockout against impulsive edits** | One global setting | All guards already **per-destination**; add **config lock** (freeze a destination's settings for a period). | 2c |
| C10 | **Wish: consistency-rule tracking + pre-breach alert** | Hard to track across many accounts | Track per-destination daily profit-contribution %; **alert before** a consistency threshold trips. | 2c |
| C11 | **Identical trades/timestamps across accounts get flagged/banned by firms** | Cloud copiers place microsecond-identical orders | Optional per-destination **execution jitter (50–200 ms) + size-rounding variance** (anti-correlation). Firm-permitting; user responsible for their firm's rules — **not** for evading a firm that forbids copying. | 2c |
| C12 | **Pending-order duplicate bug: fill sends a 2nd signal with same id → close+reopen, rejection** (FX Blue MT5) | Pending→position transition mis-read | Already defended (`_mirroredPendingOrders` + order-id→position-id dedupe at `HandleOpenAsync`). Add a **named regression test** replicating the FX Blue sequence. | 0 |
| C13 | **Slave pending fills when master's doesn't → unmanaged position** (FX Blue warning) | No fill-correlation timeout | **Slave-pending timeout** (`RequireSenderFillWithin`): if the master pending isn't filled/still-resting within N, cancel the slave pending; resync closes an **order-id-labelled** filled-pending orphan. | 1 |
| C14 | **Catastrophic lot error: master 0.23 lots → 3 lots each on receivers** (FX Blue) | No sanity ceiling on computed size | **Lot sanity ceiling**: hard-block + alert any copy whose size exceeds an absolute cap or an N× multiple of the expected proportional size. | 1 |
| C15 | **Symbol suffix / 2-3DP vs 4-5DP mismatch → "symbol cannot be converted", silent drop** (FX Blue, Duplikium whitelist) | Broker naming + price precision differ | Explicit per-destination **suffix config + price-precision normalization**; **auto symbol-map suggestion**; a dropped symbol is a **surfaced skip reason**, never silent. | 1/2b |
| C16 | **Trades not copied — insufficient funds / instrument missing / lower leverage** (cCopy) | Destination can't take the trade | Pre-flight checks (C3) + explicit skip reasons surfaced to UI + alert; never a silent miss. | 0/1 |
| C17 | **Virtual-ticket tracking files lost on VPS migration → position mapping gone** (FX Blue netting) | Local state file | cMind is **stateless** — reconciles from the broker by source-id label; no local mapping file to lose. Document as differentiator; keep it true. | — |
| C18 | **Wish: daily trading-hours window; magic-number filter** (FX Blue) | — | Per-destination **trading-hours window** + **source-label/comment filter** (cTrader equivalent of magic number — copy only master trades matching a label, e.g. one bot or manual-only). | 2b |
| C19 | **Real latency 250–600 ms, not the advertised 1–3 ms** (Duplikium) | Marketing vs reality | Measure + expose **real** per-copy latency (histogram, p50/p95) and assert a budget in the live suite. Honest numbers. | 0 |

**Design stance that falls out of the feedback:** cMind's **stateless, id-based, event-mirroring** engine
(no equity rebalancing of open copies, no local mapping file) structurally **prevents** the two worst and
most-reported failure classes — **ghost trades** (C1) and **lost position mapping** (C17). We lean into
that as the core value proposition and defend it with invariants + tests, then add the prop-firm risk
layer (C7–C11) that every copier user begs for and none reliably deliver.

### 3.2 cMAM support-group evidence (our own 13.8k-message user base)

Mined from the official cMAM Telegram support group export (13,811 text messages; **anonymized** —
aggregate theme counts + paraphrased issues only, no usernames/account data, export gitignored). This is
the **highest-value** dataset: real recurring failures from real users of the author's own prior copier.
Theme frequency (messages matching): token/auth **1,639** · install/version **1,428** · errors **1,035**
· crash/stuck **836** · symbol/mapping **694** · lot/volume **603** · slippage/lag **478** · SL/TP **368**
· not-copying **322** · equity/risk-% demand **312** · pending **200** · duplicate/ghost **153**.

| # | Recurring cMAM issue (paraphrased, anonymized) | cMind status / response | Phase |
|---|-----------------------------------------------|-------------------------|-------|
| M1 | **Token invalidation is the #1 pain.** "Access token invalidated → account goes *not active*"; caused by **not selecting all of a cID's accounts during authorization** (partial auth kills the token); must re-enter API creds after every app update. | cMind already does in-place single-valid-token swap. **Elevate**: authorize **all accounts under a cID atomically** in onboarding; a partial/again-auth must not silently kill a live host; **token-invalidated alert** + **auto re-auth/recover** (no manual re-add). Server-side creds — no re-entry on deploy. | 0/1 |
| M2 | **"Just restart it."** Standard support answer — restart cMAM / restart the profile; app **gets stuck**, sometimes on **orphan trades**. | cMind is server-side + supervised with a self-healing lease. **Add a copy-host watchdog/liveness**: detect a wedged/stalled host and auto-restart it; guarantee **one profile's orphan/error never stalls another** (per-profile + per-destination isolation). Orphan handling must never wedge the loop. | 1 |
| M3 | **"Disabled/not-linkable profile → re-add master + all accounts."** Manual recovery. | cMind has `CopyProfileNotLinkable`; **make it auto-recover** when the token refreshes — no manual re-add. | 1 |
| M4 | **Hit-and-miss copying**: trades push through inconsistently; **a master SL move doesn't mirror**; pending activates but doesn't mirror; slave account number mismatch. | Determinism + tests are our answer. **Explicitly cover SL-movement mirroring** (real reported miss) and add a reliability regression for "every master op mirrors exactly once." | 0/1 |
| M5 | **Duplicate copy**: "cTrader copied the order **twice**" (esp. **limit orders**), "happened twice in 3 days, nothing in logs." | Defended by id-based dedupe; **add a named regression: a cTrader→cTrader limit order copies exactly once** (place → fill → no second copy), with an audit trail so a dup is never invisible. | 0 |
| M6 | **Symbol digit/precision mismatch** → "Invalid SL/TP error on CFD symbols"; DE30 vs GER30; docs unclear on mapping. | Extends C15: **normalize SL/TP price precision to the destination symbol's digits** before amending (a digit mismatch is what produced cMAM's invalid-SL/TP errors — fixed there in a patch, so bake it in from day one). Auto-map + clear docs. | 1/2b |
| M7 | **Risk-%/equity sizing is the most-demanded feature.** "Risk % based on master's **stop-loss distance**"; "master risks 2% → slave auto-risks 2%"; "**max-risk parameter** for orders **without** a SL." | cMind has `FixedRiskPercent`/`ProportionalEquity`, but users want the **SL-distance-derived** variant + a **max-risk fallback when the master has no SL**. Add both as sizing refinements. | 1 |
| M8 | **Conditional/scheduled Close-All + "sleep mode"**: "close all on profit **and** on loss"; robust **sync-closed that really closes ALL** and tolerates "position ID not found." | Flatten-All is C8/Phase 2c. **Add**: conditional close-all (equity/PnL trigger), scheduled/sleep windows, and a **sync-closed that never chokes on a missing/closed id** (graceful reconcile). | 2/2c |
| M9 | **Fees** requested ("is it possible to set fees?"). | Phase 3 fee engine (high-water mark). | 3 |
| M10 | **Desktop-app churn**: license re-registration, version updates re-ask API creds, MT4/MT5 EA must stay open, VPS moves. | **Structurally gone** in cMind: server-side, no per-terminal EA, no license file, no re-enter-creds-on-update. Document as differentiator; do not regress into any local-state dependency. | — |

**Takeaway from our own users:** the dominant real-world failures are **token/auth churn (M1)** and
**process wedging that "restart" is the only cure for (M2)** — not the exotic trading edge cases. cMind's
architecture already removes most of M10 and the ghost-trade class; the plan now **prioritizes token
robustness + a host watchdog + auto-recovery** (Phase 0/1) above the fancier features, because that is
what actually burned the existing user base. Risk-%-from-SL (M7) and conditional close-all (M8) are the
top **feature** asks and are folded into Phases 1–2c.

---

## 4. Concrete gaps found in the current code

These are real, cited to `src/CopyEngine/CopyEngineHost.cs`:

| # | Gap | Evidence | Impact |
|---|-----|----------|--------|
| G1 | **Copy delay is always `TimeSpan.Zero`** passed into the decision context | lines 352, 453 (`TimeSpan.Zero`) | `MaxDelaySeconds` / max-lag guard is **dead in live** — stale signals are never skipped |
| G2 | **No live equity/margin feed** — `Snapshot(balance) => new(balance, balance, balance)` | line 633; `LoadBalanceAsync` only | `ProportionalEquity`, `ProportionalFreeMargin`, `FixedRiskPercent`, `FixedLeverage` size off **balance**, not equity/free-margin → wrong sizing when floating P&L ≠ 0 |
| G3 | **No continuous account-level protection** — guards (`MaxDrawdownPercent`, `DailyLossLimit`) are stored + checked at open only, never enforced against **running** equity | `CopyDestination.SetGuards`; no equity poller | can't honor prop-firm daily-loss / max-drawdown; no ZuluGuard / Sell-Out |
| G4 | **Sequential destination dispatch** — `foreach (destination) await …` | `HandleOpenAsync` 179–190 and every mirror method | multi-slave queue congestion (pain point #4); Nth slave lags |
| G5 | **Slave partial-fill not reconciled** — copy assumed fully filled; volume never trued-up | `CopyOpenToDestinationAsync`; resync only opens-missing / closes-orphan | slave volume drifts from proportional target on partial fills / rejections |
| G6 | **No realized-slippage / latency metrics** — only structured logs, no histograms | `LogMessages` only | can't detect a latency/slippage regression quantitatively |
| G7 | **Reconcile reload per event** — `ReconcileAsync` called inside each mirror per destination | `MirrorPartialClose/StopChange/HandleClose` | extra round-trips add latency under load; no local position cache |
| G8 | **No rejection circuit breaker** — a rejecting destination keeps getting orders every event | `CopyOpenFailed` logged, loop continues | rejection storm, prop-firm rule risk |

---

## 5. Target architecture additions (DDD-aligned)

New/changed domain concepts (Core, infra-free). All new entities/VOs full-DDD from the start.

### 5.1 Value objects (Core/CopyTrading/CopyValueObjects.cs)
- `CopyLatency(TimeSpan)` — non-negative; carries master-event → dispatch delta.
- `EquityThreshold` and `AccountProtectionPolicy` VO — `Mode` (`Off | CloseOnly | Frozen | SellOut`),
  `StopEquity`, `TakeEquity` (nullable), evaluated against a live `EquitySnapshot`.
- `AllocationTier` (optional, money-manager phase) — named risk band reused across destinations.
- `PerformanceFee` / `ManagementFee` + `HighWaterMark` VO (money-manager phase).
- Replace the `Snapshot(balance,balance,balance)` placeholder with a real `EquitySnapshot(balance,
  equity, freeMargin)` sourced from a live account-state read.

### 5.2 Aggregate changes (`CopyDestination` / `CopyProfile`)
- `CopyDestination.SetAccountProtection(AccountProtectionPolicy)` intention method + jsonb-persisted
  columns; invariant: `SellOut` requires a `StopEquity`.
- `CopyDestination.SetMaxLag(MaxCopyDelay)` already exists — wire it to a **real** measured latency.
- `CopyProfile` gains a **health** sub-concept: `RejectionBudget` per destination and a domain event
  `CopyDestinationTripped(reason)` when the budget is exhausted → destination auto-paused (Follower
  Guard). One aggregate, one transaction — trip state lives on the destination.
- Domain events: `CopyLatencyBreached`, `AccountProtectionTriggered(mode)`, `CopyDestinationTripped`,
  `SlaveVolumeReconciled`. Dispatched after `SaveChanges`; SignalR/alerts subscribe (no logic inlined).

### 5.3 Engine changes (`CopyEngine`)
- **`AccountStateReader`** abstraction (Core interface, infra impl) — live balance **+ equity +
  free-margin** via Open API account/spot; feeds real `EquitySnapshot` (fixes G2).
- **Latency stamp** — each `ExecutionEvent` carries the master event server timestamp; host computes
  `now - eventTime` via injected `TimeProvider` and passes real `CopyLatency` into the decision (fixes
  G1). Emit a latency metric per copy (G6).
- **Bounded-concurrency dispatch** — replace sequential `foreach await` with
  `Parallel.ForEachAsync`-style bounded fan-out (configurable `MaxDestinationConcurrency`, default e.g.
  4) so N slaves are placed near-simultaneously; per-destination failure still isolated (fixes G4).
  Keep `_stateGate` for state mutation; do network I/O outside the mutation critical section.
- **Local position cache** — maintain a per-destination label→position map updated from events, refreshed
  on resync, so mirror ops don't re-`Reconcile` every time (fixes G7). Reconcile stays the source of
  truth on reconnect.
- **Partial-fill true-up** — after placing a copy, verify filled volume vs proportional target on the
  next reconcile pass; top-up or trim to target within lot-step (fixes G5). Emit
  `SlaveVolumeReconciled`.
- **Rejection circuit breaker** — count consecutive rejects per destination in a window; on breach raise
  `CopyDestinationTripped` and stop sending to it until manual resume or cooldown (fixes G8).
- **EquityGuard loop** — a bounded poller (respecting `AccountStateReader`) evaluates each destination's
  `AccountProtectionPolicy` against live equity every N seconds; on breach applies the mode
  (`CloseOnly`/`Frozen`/`SellOut`) and raises `AccountProtectionTriggered` (adds ZuluGuard / Duplikium
  Global Account Protection). Market-order sell-out documented as best-effort (no slippage guarantee),
  mirroring the honest caveat competitors publish.

### 5.4 Money-manager model (Phase 4 — larger, gate behind a feature toggle)
- Fee engine: performance fee on **High-Water Mark**, management fee on AUM, per-lot fee; computed at a
  rollover boundary; append-only `CopyFeeAccrual` records (not an aggregate — like `AuditLog`).
- Provider stats/ratings read model (CQRS-lite): drawdown, win-rate, consistency, realized-slippage vs
  signal — query EF directly into DTOs, not through the aggregate.
- Withdrawal-protection hook (proportional close + buffer window) — only if we later hold pooled funds;
  documented as out-of-scope until a PAMM-style pooling context exists.

---

## 6. Methodical implementation roadmap

Each phase is independently shippable, ships all three test tiers + docs, and leaves the tree green.

### Phase 0 — Instrumentation & correctness fixes (no new features)
1. Fix **G1** (real copy latency via `TimeProvider` + event timestamp) and **G2** (real
   `EquitySnapshot` from a live `AccountStateReader`). These are correctness bugs, not features.
2. Add OpenTelemetry metrics (**G6**): copy latency histogram, realized-slippage gauge, per-destination
   fill-rate / skip-reason counters, dispatch fan-out duration. Wire into existing
   `OpenTelemetryConfigurator`.
3. Tests: unit (latency + equity sizing now driven by `FakeTimeProvider` + fake equity), extend
   `FakeTradingSession` with equity/free-margin + event timestamps, live assertion that copy latency is
   recorded. Docs: update `copy-trading.md` + `live-copy-trading.md`.

> **Priority note (from §3.2):** our own users were burned most by **token churn (M1)** and
> **process wedging (M2)**, not exotic trading cases. So Phase 0/1 lead with **token robustness +
> host watchdog + auto-recovery** before the fancier features.

### Phase 0.5 — Token robustness & host liveness (top real-world failures)
1. **M1 — atomic full-cID authorization**: onboarding authorizes **every account under a cID together**;
   a re-auth/partial-auth must not silently invalidate the token a live host is using. Emit a
   **`CopyTokenInvalidated`** alert and **auto re-auth/recover** instead of dropping to *not-active*.
2. **M2 — copy-host watchdog**: a liveness monitor detects a stalled/wedged host (no heartbeat / stuck on
   an orphan) and **auto-restarts** it; guarantee **per-profile + per-destination isolation** so one
   profile's error never stalls another (harden the supervisor loop + `try` boundaries).
3. **M3 — auto-recover** a `NotLinkable`/disabled profile when its token refreshes — no manual re-add.
4. Tests: DST (inject token-invalidation + a wedged host → assert alert + auto-recover, other profiles
   keep running), integration (partial-auth doesn't kill a live host), **live** (force a token refresh
   mid-run → host recovers and keeps copying).

### Phase 1 — Resilience hardening
1. **G4** bounded-concurrency destination dispatch. 2. **G8** rejection circuit breaker +
   `CopyDestinationTripped` (Follower Guard). 3. **G5** partial-fill volume true-up. 4. **G7** local
   position cache. 5. **G3 (part)** stop-order-rejection fast-market handling — prefer market execution
   on breach, log/skip with reason instead of orphaning. 6. **C13** slave-pending fill-correlation
   timeout + order-id-labelled orphan cleanup. 7. **C14** lot sanity ceiling. 8. **M4** SL-movement
   mirroring reliability regression + "every master op mirrors exactly once." 9. **M6** SL/TP price-
   precision normalization to destination digits (prevents the invalid-SL/TP class). 10. **M7** sizing
   refinements: **risk-% from master SL distance** + **max-risk fallback when master has no SL**.
2. Tests: extend `FakeTradingSession` to inject partial fills, rejection bursts, per-destination latency,
   digit-mismatched symbols, SL-less positions; DST/stress for rejection storms and partial-fill drift →
   assert convergence. Live: N-slave fan-out latency spread bounded; forced-reject destination trips and
   is isolated; risk-% row sizes off SL distance.

### Phase 2 — Account-level protection (ZuluGuard / Global Account Protection)
1. `AccountProtectionPolicy` VO + `SetAccountProtection` + persistence + endpoint + dialog UI.
2. `EquityGuard` poller + `AccountProtectionTriggered` event; modes CloseOnly/Frozen/SellOut.
3. Tests: unit (policy evaluation at/above/below threshold, mode transitions), DST (equity walk crosses
   threshold → correct mode applied, idempotent), **live** (drive a demo slave's equity across a set
   `StopEquity` by opening a losing position → assert Sell-Out closes all + destination frozen).
4. Docs: new section in `copy-trading.md`; note the no-guarantee caveat.

### Phase 2b — cMAM feature parity (options, re-implemented under our model)
1. **Sync-Open / Sync-Closed / Ignore-New-Trades** per-destination toggles — intention methods on
   `CopyDestination`; the resync path already opens-missing / closes-orphan, so these become *gates* on
   that path (Sync-Open off ⇒ don't open pre-existing; Ignore-New ⇒ manage existing copies only, place
   no new opens). Invariant: Ignore-New + Sync-Open-off = pure manage/close mode.
2. **Per-symbol overrides** — enrich `CopySymbolMapEntry` (VO) with optional per-symbol Volume Type /
   Amount / Min-Max lot / Copy-SL / Copy-TP; the decision engine prefers the per-symbol override over the
   destination default. Full DDD: overrides validated in the VO, mutated only through the root.
3. **Symbol-map CSV import/export + sample generator** — endpoint + dialog: export master symbols to a
   prefilled CSV template, import a filled map; parse/validate at the edge (anti-corruption), map to VOs.
4. **Notification routing** — the new domain events (`AccountProtectionTriggered`,
   `CopyDestinationTripped`, `CopyLatencyBreached`, copy-open-failed, disconnect) subscribe into the
   existing alerts/notification channel (Telegram/email/in-app) — no logic inlined into the engine.
5. Tests: unit (toggle gating truth-table, per-symbol override precedence, CSV round-trip),
   DST (manage-only never opens; sync-closed reconciles a stop-gap), **live** matrix rows for each toggle
   + a per-symbol-override row; notification event assertions.

### Phase 2c — Prop-firm risk layer (the most-requested, least-delivered feature set)
Directly from prop-trader complaints C7–C11. Extends Phase 2's account protection with the risk controls
copier users beg for. All per-destination, all on aggregates/VOs, all event-driven.
1. **Prop-rule guard** — per-destination `DailyLossCap`, `MaxDrawdown`, **`TrailingDrawdown`** VOs tracked
   in **real time** against the live equity feed (Phase 0). On breach → **auto-flatten** that destination
   + **lockout** for the trading day; raise `PropRuleBreached`.
2. **Flatten-All panic button** (C8) — `POST /api/copy/profiles/{id}/flatten` (+ per-destination) closes
   every copied position immediately; UI toolbar button with confirm. Domain method on the aggregate.
2b. **Conditional / scheduled Close-All + sleep mode** (M8) — close-all on an equity/PnL trigger
   (on-profit **and** on-loss), scheduled sleep windows; and a **robust sync-closed** that closes *all*
   orphans and never chokes on a "position ID not found" (graceful reconcile — the exact cMAM failure).
3. **Config lock** (C9) — `CopyDestination.Lock(until)` freezes settings against impulsive edits during a
   drawdown; enable-vs-lock distinction.
4. **Consistency tracking** (C10) — track per-destination daily profit-contribution %; `alert before`
   crossing a configured consistency threshold (`ConsistencyThresholdApproaching` event).
5. **Execution anti-correlation** (C11) — optional per-destination **jitter (50–200 ms) + size-rounding
   variance**, off by default. Documented strictly as a compliance aid for firms that **permit** copying;
   **not** a tool to bypass a firm that forbids it (that stays the user's responsibility).
6. Tests: unit (trailing-drawdown math, lockout state machine, consistency %), DST (equity walk trips
   trailing DD exactly at threshold → flatten + lockout, idempotent; jitter stays within bounds), **live**
   (losing position drives a demo destination past its daily-loss cap → auto-flatten + locked out; flatten
   -all closes everything). Docs: `copy-trading.md` prop-firm section + honest no-guarantee caveat.

### Phase 3 — Provider analytics, transparency & (optional) money-manager fees
1. **Execution-transparency report** (C4) — per copy: slave entry vs master signal price, measured
   latency, realized slippage; read model + UI (the tool cCopy users keep asking for).
2. Provider rating / drawdown / consistency stats; **verified live-only** providers if a marketplace is
   exposed (C6). 3. Fee engine w/ high-water mark behind a feature toggle. Full DDD; append-only accrual
   records; CQRS-lite read side.

### Cross-cutting hardening folded into Phases 0–2b (from user feedback)
- **C1/C17 differentiator invariants** (Phase 0): a balance op rebalances/closes **no** open copy; no
  local mapping file — reconcile from broker by label. Named regression tests lock both.
- **C12 / M5** (Phase 0): named regression tests — FX-Blue pending→position duplicate-bug **and** the
  cMAM "cTrader→cTrader limit order copied twice" case; a duplicate is never silent (audit trail).
- **C13** (Phase 1): slave-pending fill-correlation timeout + order-id-labelled filled-pending orphan
  cleanup in resync.
- **C14** (Phase 1): lot sanity ceiling (absolute cap + N×-expected block) + alert.
- **C15** (Phase 1/2b): suffix config + price-precision (DP) normalization + auto symbol-map suggestion;
  dropped symbol = surfaced skip reason.
- **C18** (Phase 2b): per-destination trading-hours window + source-label/comment filter.

### Phase 4 — E2E **live** testing framework overhaul (see §7)
Runs in parallel with Phases 0–3; every option added above lands with a matrix entry + chaos case.

---

## 7. E2E live testing framework — "every option, no human"

Goal (from the request): **every part and every option is fully E2E live-testable without user
interaction, and the suite catches any regression after a change.** Build on the existing
`LiveCopyScenario` / `LiveCopyFixture` (headless onboarding already refreshes tokens with no browser
prompt) — turn it into a systematic, matrix-driven, self-cleaning, regression-baselined framework.

### 7.1 Design principles
- **Zero interaction**: reuse the existing headless onboarding (`CMIND_ONBOARD=1`) + non-expiring
  refresh-token cache. No option requires a human in the loop.
- **Demo-only safety**: fixture filters `IsLive == false`, demo gateway only, every scenario cleans up
  every position/order it opens (already the pattern — keep it mandatory).
- **Deterministic verdict**: each scenario returns `Pass | Fail | Inconclusive`. A **closed market** is
  `Inconclusive` (never a false Fail). A functional miss is a hard `Fail`.
- **Self-cleaning + idempotent**: prefix every artifact with a unique run label; a teardown sweep closes
  any leftover `cmind-*`-labelled position/order at fixture dispose, so a crashed run can't poison the
  next.

### 7.2 Option-matrix harness (new `LiveCopyMatrix`)
A single data-driven runner takes a `CopyDestination` configuration + an action script and asserts the
observed slave outcome. Every option becomes one row; xUnit `[Theory]` / `MemberData` enumerates them so
adding an option = adding a row, and CI runs the whole grid:

- **Sizing**: one live row per `MoneyManagementMode` (FixedLot, Lot/Notional multiplier, Prop
  Balance/Equity/FreeMargin, AutoProportional, FixedRiskPercent, FixedLeverage) → assert slave volume
  matches the calculator's expectation within lot-step.
- **Filters**: direction Both/Long/Short, reverse, symbol whitelist/blacklist (assert skip), symbol map
  (open on mapped symbol), order-type filter (place each pending kind, assert only allowed ones mirror).
- **Advanced mirroring**: partial close, scale-in, pending place/amend/cancel/expiry, trailing, SL/TP
  copy on/off, market-range slippage.
- **Guards** (Phase 0/2): max-lag skip (inject delay), slippage-filter skip, drawdown/daily-loss,
  account-protection modes.
- **Bounds**: min-lot skip vs force-min, max-lot cap.
- **cMAM-parity toggles** (Phase 2b): Sync-Open on/off (pre-existing master trade opened or not),
  Sync-Closed, Ignore-New (manage-only), per-symbol override precedence, symbol-map CSV round-trip,
  trading-hours window, source-label filter.
- **Prop-firm risk layer** (Phase 2c): daily-loss cap → auto-flatten + lockout; trailing-drawdown trip;
  flatten-all panic button closes everything; config-lock blocks edits; jitter stays within bounds.
- **User-feedback guards**: lot sanity ceiling blocks a catastrophic oversize; slave-pending timeout
  cancels an uncorrelated pending; balance-op on master/slave rebalances **nothing** (C1 invariant).

Each row: warm host → drive master via the probe session → poll slave by source-id label → assert →
clean up. The matrix is the **regression net**: any behavior change flips exactly the affected rows.

### 7.3 Chaos / fault-injection **live** layer (new `LiveCopyChaos`)
The DST/stress suite already injects faults deterministically in-memory; extend the *live* tier with the
faults that only manifest against the real socket:

- **Socket flap** — force a reconnect mid-scenario (drop the connection) → assert resync converges (no
  duplicate, no orphan) via the id-based reconcile.
- **Token rotation mid-run** — call `IOpenApiTokenClient.RefreshAsync` while a position is open → assert
  the host swaps in place and keeps copying (`CopyHostTokenRotated`).
- **Node death + lease reclaim** — run two supervisors against the same DB; kill the owner → assert the
  other reclaims the lease and continues hosting without double-copy (real Postgres + real accounts).
- **Rejection injection** — point a destination at a symbol/volume the broker will reject → assert
  circuit breaker trips and isolates it (Phase 1).
- **Start-with-open-positions** — open master positions *before* starting the host → assert reconcile
  opens the labelled copies.
- **Balance-op no-rebalance (C1)** — with a copy open, perform a demo deposit/withdrawal on master and on
  slave → assert **no** copy is opened or closed (the anti-ghost-trade invariant that breaks cCopy/eToro).
- **Catastrophic-size guard (C14)** — force a sizing config that would compute an absurd lot → assert the
  sanity ceiling blocks it and alerts, rather than placing it.
- **Token invalidation + auto-recover (M1)** — invalidate a running host's token mid-scenario → assert
  `CopyTokenInvalidated` alert fires and the host re-auths and keeps copying (no manual re-add).
- **Wedged-host watchdog (M2)** — stall a host (inject a hang on an orphan) → assert the watchdog restarts
  it and **other** profiles keep running untouched.

### 7.4 Regression baselining & CI gating
- **Latency/slippage budget assertions**: each live copy records master→slave latency and realized
  slippage; the harness asserts they stay within a configured budget (e.g. p95 latency < X ms, realized
  slippage < Y pips) so a **performance** regression fails the build, not just a functional one.
- **Golden verdict file** per matrix row (`Pass`/expected volume band); a run diffs against it. New
  behavior updates the golden in the same commit (like the docs rule).
- **Tiers wired to CI**: deterministic + DST run on every PR (no secrets). The live matrix + chaos run in
  the in-cluster Job (`scripts/k8s-e2e.sh`, `TEST_FILTER=...LiveCopyMatrix`) on a schedule / pre-release,
  reading the gitignored secret. Suite prints a per-row table and asserts `Passed!`.
- **Flake control**: bounded polling with explicit timeouts (existing pattern), `Inconclusive` on closed
  market, unique labels, teardown sweep — so a green run means green, and a red run points at one row.

### 7.5 What this gives us
A single command runs the whole copy feature — every sizing mode, every filter, every advanced-mirroring
option, every guard, plus socket flap / token rotation / node death / rejection — against real cTrader
demo accounts, with no human, self-cleaning, and a pass/fail/perf verdict per option. Any change that
breaks any option flips its row. That is the "rock solid, regression-proof" bar the request asks for.

---

## 8. Definition of done (per phase)

- [ ] DDD checklist holds (new logic on aggregates/VOs/domain services, no anemic surface, events for
      cross-aggregate reactions, Core stays infra-free).
- [ ] `TimeProvider` everywhere; no `DateTime.UtcNow`; `FakeTimeProvider` in time-dependent tests.
- [ ] Unit + integration + **live** matrix/chaos rows for every new option; `dotnet test` green.
- [ ] `FakeTradingSession` extended (never weakened) for each new real behavior.
- [ ] `docs/features/copy-trading.md` + `docs/testing/live-copy-trading.md` updated in the same commit.
- [ ] OpenTelemetry metrics emitted for latency + slippage + fill-rate; `LogMessages` audit events added.
- [ ] Rider `get_file_problems` clean on every touched `.cs`/`.razor`; `caveman:cavecrew-reviewer` pass.

---

## 9. Sources

- Duplikium: [features](https://www.trade-copier.com/features) ·
  [prop-firm protection](https://www.trade-copier.com/features/propfirm-trade-copier) ·
  [account settings](https://www.trade-copier.com/how-to/tutorials/account-settings)
- Comparison: [ForexBrokers social/copy trading](https://www.forexbrokers.com/guides/social-copy-trading) ·
  [Wundertrading best platforms](https://wundertrading.com/journal/en/reviews/article/best-copy-trading-platforms)
- cMAM (author's own prior project, features-only — buggy, do not copy design):
  `github.com/amusleh-spotware-com/cMAM` · `docs/mirroring/slave-settings.md` · `src/MirroringModule`
- cMAM support-group evidence (§3.2): official cMAM Telegram support group export, 13,811 messages,
  **anonymized aggregate themes only — raw export gitignored, never committed** (`ChatExport*/`, `result.json`)
- cCopy — Spotware's own native cTrader copy solution (the actual Spotware product, distinct from cMAM)
- MAM/PAMM: [Brokeree cTrader PAMM](https://brokeree.com/articles/pamm-for-ctrader-for-brokers-and-investors/) ·
  [FxTrusts MAM allocation methods](https://fxtrusts.com/solutions/mam) ·
  [track360 PAMM/MAM guide](https://track360.io/blog/pamm-mam-account-software-for-forex-brokers-operator-guide-2026)
- User complaints (mined for §3.1):
  [cTrader Copy forum — ghost/missing trades](https://community.ctrader.com/forum/ctrader-copy/) ·
  [cCopy ROI mismatch](https://community.ctrader.com/forum/ctrader-copy/38510/) ·
  [cCopy executed later than provider](https://community.ctrader.com/forum/ctrader-copy/36102/) ·
  [ZuluTrade Trustpilot (silent disconnects, orphans)](https://www.trustpilot.com/review/zulutrade.com) ·
  [eToro auto-close on rebalance](https://help.etoro.com/en-us/s/article/Why-are-copied-positions-being-closed-automatically-US) ·
  [Duplikium Trustpilot (doubled/latency)](https://www.trustpilot.com/review/www.trade-copier.com) ·
  [FX Blue pending-bug / lot-error thread](https://www.forexfactory.com/thread/1139695) ·
  [prop-firm copier rules (no rule enforcement, flatten-all, jitter)](https://tradecopia.com/faq)
- FX Blue Personal Trade Copier: [product](https://www.fxblue.com/tools-for-download/fx-blue-personal-trade-copier) ·
  [MT4 user guide (symbol suffix / DP / RequireSenderFillWithinMinutes)](https://api.fxblue.com/appstore/u2/mt4-personal-trade-copier/user-guide)
- Pain points: [copygram slippage & latency](https://copygram.app/blog/education/understanding-slippage-latency-copy-trading) ·
  [mt4copier pitfalls](https://www.mt4copier.com/copy-trading-pitfalls-every-account-manager-must-avoid/) ·
  [FX Blue duplicate-signal bug thread](https://www.forexfactory.com/thread/1139695) ·
  [NinjaTrader multi-account lag](https://forum.ninjatrader.com/forum/ninjatrader-8/platform-technical-support-aa/1231250-slow-trade-copier-executions) ·
  [Tradesyncer troubleshooting](https://help.tradesyncer.com/en/articles/13905201-troubleshoot-trade-copying)
</content>
</invoke>
