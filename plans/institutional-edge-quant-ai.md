# Institutional-Edge Quant & AI for Retail FX/CFD

**Goal:** bring institution-only edges to retail cTrader traders on cMind. Ground every feature in
peer-reviewed quant research, and hold each to the house bars: **resilience, full E2E testability,
security, ease of use, reliability.**

**Design north star:** the durable edge is not "AI predicts price" (research shows retail
price-prediction bots overfit and fail after costs). The durable edge is **the validation,
risk, execution, and portfolio machinery that funds use and retail never gets** — most of which is
*deterministic math* that fits our pure `src/Core` domain and is therefore fully unit + integration +
E2E testable with zero flaky external dependencies. The Claude/LLM layer sits on top as explainer and
research assistant, always gated and always degradable.

---

## 1. Research findings (what the evidence actually supports)

### 1.1 What retail traders want from AI (2026)
- **AI as co-pilot, not oracle.** Regulators (CFTC advisory) and vendors agree AI cannot predict the
  market; retail uses it for *confluence/confirmation*, scanning, and self-analysis. Explainable AI
  (XAI) — "signal because X + Y" — is the headline 2026 demand.
- **Sentiment, two ways:** (a) NLP over news/social for confirmation; (b) **retail-positioning
  contrarian** signal (>60% long ⇒ sell bias, <40% ⇒ buy bias, 40–60% neutral).
- **Pattern/market scanning** across many pairs/timeframes for setups.
- **AI trading journal / self-analysis** — the newest genuinely-useful category: analyse *your own*
  trades, tag by condition, coach. (TradeZella "Zella AI".)
- **Reality check:** most retail AI bots fail — overfitting, transaction costs, regime change. So the
  win is decision-support + *discipline tooling*, not autopilot.

### 1.2 How institutions / prop firms / banks get their edge
- Edge is **data science, not raw latency** — in-house ML/DL on proprietary + alternative data.
- **NLP sentiment** funds have historically out-performed discretionary peers by ~8–12%/yr (vendor
  claim — treat as directional, not guaranteed).
- **Alternative data** (satellite, card, order-flow, positioning) adds ~2–3%/yr alpha over price/vol
  alone in the literature.
- **Execution alpha** — smart order routing, TCA, timing: tiny per trade, compounds over thousands.
- **Generative-AI research workflows** — e.g. Man Group *AlphaGPT* (hypothesis → code → validate).
- **Alpha decay** is the defining constraint: half-life of a quant edge fell from ~5yr (2000s) to
  ~18 months (2025). **Adaptation > discovery.** Continuous retraining/validation is the real moat.
- Honest calibration: the *average* AI fund barely beats benchmark after fees; the top ones win on
  **proprietary signal + validation rigor**, not model architecture alone.

### 1.3 Scientifically-grounded, defensible use cases for FX/CFD
- **Deep-learning FX forecasting** (LSTM/GRU/Bi-LSTM, attention/ALFA, CNN-LSTM w/ lag features,
  transformer hybrids): consistently improve *statistical* metrics (RMSE/MAE/directional accuracy),
  **not proven profitable after costs/slippage** and dataset-dependent. Use as a *feature*, never as
  a standalone signal.
- **Volatility forecasting** with deep learning + **complexity measures** (Hurst exponent, fuzzy
  entropy) meaningfully improves accuracy → directly useful for **vol-targeted position sizing**.
- **Regime detection** (HMM/IOHMM, GRU posterior regime probabilities): explicit *regime-probability*
  features yield the best, most interpretable policies.
- **Reinforcement-learning optimal execution** (Almgren-Chriss extensions, DDQN, DDPG, queue-reactive
  models): learns market-vs-limit order placement to cut market impact → **execution alpha** for our
  copy-trade mirror.
- **Backtest-overfitting statistics — the single most defensible institutional tool for retail:**
  **Deflated Sharpe Ratio (DSR)**, **Probability of Backtest Overfitting (PBO)**, **Probabilistic
  Sharpe (PSR)**, **Minimum Backtest Length**, **Purged/Embargoed K-Fold** and **Combinatorial
  Purged Cross-Validation (CPCV)** (Bailey & López de Prado). Harvey et al.: new signals should clear
  **t-stat ≥ 3.0**, not 2.0.
- **Look-ahead bias** is the top reproducibility killer for LLM/point-in-time finance
  (Look-Ahead-Bench 2026) → our data plane must be **point-in-time (PIT)** by construction.
- **Multi-agent LLM desks** (TradingAgents: alpha/sentiment/fundamentals/risk/portfolio agents +
  reviewer, human-in-loop): explainable, but crowding + hallucination + reproducibility are real
  risks → guardrails + evidence ledger mandatory.

**Sources:** SSRN 2460551 (Deflated Sharpe); Bailey/López de Prado *Probability of Backtest
Overfitting*; Harvey/Liu/Zhu *…and the Cross-Section of Expected Returns* (t≥3); arXiv 2507.06345 (RL
execution); arXiv 2511.00190 (regime-aware DRL); MDPI *JRFM* 17/12/557 (vol + complexity measures);
Springer *Computational Economics* CNN-LSTM lag-feature & Forex-Net; arXiv 2601.13770 (Look-Ahead-Bench);
ACM ICAIF *LLM Agents for Investment Management*; TradingAgents playbook; CFTC AI customer advisory.

---

## 2. How this maps onto cMind today

Existing surface we build on (do **not** rebuild):
- **Backtests** via `ghcr.io/spotware/ctrader-console` on nodes → JSON reports. This is our primary
  quant data surface. Today no CLI optimizer → we run an AI-proposed param loop.
- **Optimization is coming to the cTrader Console CLI** (upcoming version) — the same desktop
  optimizer (grid / genetic sweep over parameter ranges) exposed via CLI, invoked and containerized
  exactly like `backtest` today. When it lands it replaces the hand-rolled AI param loop as the
  *primary* optimizer and emits a **full trial surface** (hundreds–thousands of param sets × their
  backtest metrics). This is a large multiplier for cMind: a native sweep is the single biggest
  overfitting hazard for retail — which makes **F1's overfitting statistics the mandatory guard on
  optimizer output**, and CPCV/walk-forward the natural fit for an optimization grid.
- **`IAiFeatureService`** (Claude over raw HTTP, fully gated + degradable), MCP `AiTools`,
  `AiRiskGuard`, `AgentMandate`→`AgentProposal`, `AlertRule`→`AlertEvent`.
- **Copy trading** over cTrader Open API (mirror engine, `FakeTradingSession` simulator).
- **Prop-firm simulation** with live Open-API equity tracking + node lease.
- **Strong DDD**, `TimeProvider` everywhere, `ISecretProtector`, MudBlazor dialogs, three test tiers.

Gaps the research exposes (our opportunity):
1. We optimize params but **never test for overfitting** — a retail trap we close with fund-grade
   statistics. **(Highest-value, lowest-risk, most testable.)** Doubly urgent once the **CLI optimizer**
   lands: a native sweep manufactures thousands of trials, so the reported "best" is almost always
   overfit unless deflated. F1 becomes the guard that turns a raw optimizer winner into an honest
   verdict.
2. No **regime awareness** — one Sharpe hides "great in trend, dead in chop".
3. No **portfolio layer** — users run many cBots/copy strategies with no correlation, vol-target, or
   Kelly-capped sizing across them.
4. Copy mirror has slippage handling but no **execution-cost analytics / smart slicing**.
5. Sentiment exists but isn't **structured, scored, point-in-time, or contrarian**.
6. No **strategy-health / alpha-decay** statistical monitor (only an ad-hoc AI call).
7. No **AI trading journal** over the user's own instances/copy fills.

---

## 3. Feature catalogue (full implementation detail)

Ordered by **edge-per-risk**. Each feature lists: Core domain, Infrastructure, Web/UI, AI hook,
tests (unit + integration + E2E + failure paths), docs, migration. Every statistical feature is
**deterministic** → E2E without external calls, and carries an **explanation string** (XAI) so the UI
always answers "why".

---

### F0 — Autonomy & Safety Kernel *(shared building blocks — built once, used by F7B + F9)*

Everything that touches live money reuses **one** set of primitives (`src/Core/Autonomy/`). Defined
here so F7B and F9 reference it rather than redefine it. All are pure/deterministic and enforced as
**domain invariants**, not UI politeness.

- **`AutonomyLevel`** VO: `Advisory` (propose only) → `ApprovalGated` (per-action owner approval, F7) →
  `FullAuto` (no per-trade approval, bounded by the envelope below).
- **`RiskEnvelope`** VO: `MaxDailyLoss`, `MaxOpenExposure`, `MaxPositionSize`, `MaxLeverage`,
  `AllowedSymbols`, `MaxConsecutiveLosses`, `MaxOrdersPerHour`. Ctor rejects empty/absurd
  (`DomainException`). **Every order is validated against the envelope inside the aggregate before
  dispatch** — a breach is *refused* (raises `AutonomousActionRejected`), never silently clamped.
  Enforced **per-account and in aggregate** when a mandate spans multiple accounts. Hard
  `PerformanceTarget`s (F9 goals) unify into the same check — one source of truth.
- **`AutoTradingConsent`** (owned): versioned disclaimer acceptance (`Version`, `AcceptedAt` via
  `TimeProvider`, user, IP/UA) → `AuditLog`. Reaching `FullAuto` throws without a current-version
  consent; a disclaimer version bump forces re-consent. (One-time consent ≠ per-trade approval.)
- **`CircuitBreaker`** (deterministic, no LLM): trips `FullAuto → ApprovalGated`/halt on
  `MaxConsecutiveLosses`, daily-loss breach, equity anomaly, **hard-goal breach (F9)**, poison-loop
  (repeated failures), or **AI-provider unavailability** — a down/hallucinating LLM must never open
  fresh risk.
- **Kill switch**: `EmergencyStop()` (per-mandate) + tenant-wide `Disarm` → `AutonomyHalted` event →
  flatten/stop via the existing copy/instance stop path. Idempotent; safe to fire repeatedly.
- **Durable runtime contract** (for any live loop — F7B agent, F9 agents): runs under the existing
  **node lease**; **fault-isolated** (any AI/tool/Open-API/DB error is caught → *hold + manage
  open risk only* → logged, **never rethrown into the host** — same contract as `AnthropicAiClient`);
  **watermark-idempotent** (last-acted sequence → restart/failover can't re-send an order); resumes on
  app restart + node failover (lease reclaim + Phase-1 resync).
- **Audit & evidence**: every autonomous action is append-only and replayable — written to the F7
  **evidence ledger** + `AuditLog`, via source-generated `LogMessages` (no raw `ILogger.Log*`, no
  secrets logged).
- **Feature gating / white-label**: `AutonomousAutoTrading` toggle (**default ON**) + a finer
  `AgentStudio` toggle, wired to the existing white-label/feature-toggle system. Off ⇒ UI hidden, arm
  endpoints 404 (mind the ErrorBoundary gotcha), and any live mandate **fail-safes back to
  `ApprovalGated`/disarmed** — never orphaned mid-trade.

*Backstops that plug into the kernel:* F4 strategy-health, prop-guard exposure guardian, `AiRiskGuard`
auto-stop — always on, even in Full Auto.

---

### F1 — Backtest Integrity Lab *(the flagship institutional edge)*

Turn a raw backtest report into a fund-grade verdict that tells a retail trader **whether the result
is real or overfit** — the thing no retail platform gives them.

**Core (`src/Core/Quant/`):** pure, deterministic value objects & a domain service interface.
- Value objects: `SharpeRatio`, `ProbabilisticSharpeRatio`, `DeflatedSharpeRatio`,
  `ProbabilityOfBacktestOverfitting`, `MinimumBacktestLength`, `TStatistic`, `TrialCount`,
  `ReturnSeries` (immutable, guards: ≥N obs, finite). All reject out-of-range in ctor (throw
  `DomainException`).
- `BacktestIntegrityReport` aggregate-ish record: bundles the metrics + a `Verdict`
  (`Robust | Fragile | Overfit`) derived from thresholds (PBO < 0.5, DSR > 0, PSR > 0.95, t ≥ 3.0)
  + a human `Rationale` (XAI text, no LLM needed).
- `IBacktestIntegrityAnalyzer` (interface in Core) — takes `ReturnSeries` + `TrialCount`
  → `BacktestIntegrityReport`. **`TrialCount` sourcing:** today from the AI param loop; **once the CLI
  optimizer lands, `TrialCount` = the sweep's real grid/generation size** (hundreds–thousands) — the
  honest, large number that correctly *raises* the DSR/PBO penalty. Feeding the true trial count is
  the whole point: a native optimizer's "best" only means something after deflation.
- `TrialSurface` VO — the optimizer's full `(paramSet → OOS return series)` matrix. `AnalyzeGrid(...)`
  runs **CPCV / purged-embargoed walk-forward across the grid** → PBO + per-fold rank stability, so we
  judge the *selection process*, not just the single winner. This is exactly the problem CPCV was
  designed for.
- Math: PSR (Bailey/LdP 2012), DSR (deflate by trials, skew, kurtosis, sample length), PBO via CSCV
  (combinatorially split IS/OOS, rank, logit of relative rank), min-backtest-length. All closed-form
  or combinatorial — **no randomness**, seedable where combinatorial.

**Infrastructure (`src/Infrastructure/Quant/`):** `BacktestIntegrityAnalyzer` impl (pure C#, no infra
dep really — could live in Core; keep impl in Core if zero-dep, else Infra). Persist
`BacktestIntegrityReport` alongside the existing backtest report entity (EF owned type / JSON column).

**Web/UI:** on the backtest result view, an **Integrity** panel: traffic-light verdict, each metric
with a one-line plain-English gloss + `HelpTip`, and a bold banner when `Overfit`. New page
`/quant/integrity` for standalone analysis (paste/select a report). Owner action opens a MudBlazor
dialog to run integrity on a chosen cBot's trial history.

**AI hook (optional, degradable):** `IAiFeatureService.ExplainIntegrityAsync(report)` → narrative +
"what to change". Gated; absence never blocks the numeric verdict.

**Tests:**
- *Unit:* golden-vector tests against published DSR/PSR/PBO worked examples; invariant guards (too-few
  observations throws; PBO ∈ [0,1]); verdict transitions at each threshold boundary.
- *Integration:* run a real backtest via Testcontainers/console fixture → feed report → assert report
  persists and reloads.
- *E2E:* Playwright drives `/quant/integrity`, asserts traffic-light + banner render (mobile+desktop);
  add route to `PageSmokeTests`.
- *Failure paths:* degenerate series (all-zero returns, single trade, NaN guard), trial-count = 1
  (DSR = PSR), missing AI key (numeric verdict still shows).

**Docs:** `website/docs/features/backtest-integrity.md` (+ sidebar id). **Migration:** add integrity
columns/JSON to backtest report table.

---

### F1B — Native CLI Optimizer (overfitting-guarded) *(ships when upstream CLI adds it)*

Wrap the cTrader Console CLI optimizer exactly like we wrap `backtest` today, and pipe **every** trial
through F1 so cMind is the only place a retail trader gets a *deflated*, walk-forward-validated sweep.
Sequenced so the deterministic guts (F1 + a mock optimizer surface) ship first and the live CLI wrapper
drops in behind a feature flag the day the upstream image supports it — no idle waiting.

**Core (`src/Core/Optimization/`):** `OptimizationRun` aggregate (owns `OptimizationTrial` children),
`ParameterRange` VO (min/max/step, or genetic gene bounds), `OptimizationObjective`
(NetProfit | Sharpe | ProfitFactor | CustomDeflated), `OptimizationMethod` (Grid | Genetic).
State via the existing **TPH instance pattern** (`Queued → Running → terminal`) so it reuses the
node-dispatch, poller, and lease machinery unchanged. A run **must** carry its `TrialCount` and,
on completion, produce the `TrialSurface` (F1) — the winner is never surfaced without its
`BacktestIntegrityReport`. Domain event `OptimizationCompleted`.

**Infrastructure / Nodes:** `ContainerCommandHelpers` gains an `optimize` verb builder alongside
`run`/`backtest` (same `ArgumentList`, no shell; params via `params.cbotset`; dates `dd/MM/yyyy HH:mm`;
`--data-mode`). Dispatch through the existing `ContainerDispatcherFactory` (Http/Local) and reconcile
via the existing self-exit poller. **Unknowns to confirm against the real CLI when released** (flag
names, whether the sweep runs one container emitting all trials vs. N containers, results file shape,
genetic vs grid switches) — isolate them behind `ContainerCommandHelpers` + a parser so the rest of the
stack is decoupled. Because the sweep is heavy, honor node scheduling/lease + concurrency caps
(reuse prop-firm lease + Phase-1 resync); a killed node's optimization is reclaimed, not lost.

**Web/UI:** **Optimize** page upgraded from the AI-loop UI to a real sweep: range editor (dialog per
param), method/objective select, live trial progress, results table **sorted by deflated score not raw
NetProfit** (the anti-overfitting default), each row linking its F1 integrity verdict. Promote a winner
→ persists a `ParamSet` on the `CBot` (existing flow). "Apply best" is advisory/confirmed, never auto.

**AI hook (optional):** `AiFeatureService.ProposeParamSetSuiteAsync` becomes the *seed* for a genetic
run and the explainer of the deflated winner — the AI loop degrades to a helper, not the optimizer.

**Feature flag:** gate the live CLI wrapper on a capability check of the console image
(`AppOptions` toggle + image-version probe); when absent, the page falls back to the current AI param
loop, still F1-guarded. No dependency on upstream timing to ship P1.

**Tests:**
- *Unit:* `OptimizationRun` state transitions + invariant (no winner without integrity report);
  `ParameterRange`/objective guards; deflated-ranking orders a synthetic grid correctly.
- *Integration:* against a **mock optimizer surface** (deterministic fixture) end-to-end — dispatch →
  trials persist → `TrialSurface` → F1 verdict; and, when the real image is available, a gated
  smoke run of a tiny 2×2 grid via Testcontainers.
- *E2E:* Optimize page drives a (mocked) run, asserts results sort by deflated score + integrity links;
  route in `PageSmokeTests`.
- *Failure paths:* optimizer container non-zero exit / OOM → run `Failed` cleanly; node death
  mid-sweep → lease reclaim + resume/restart; empty/garbled results file → parser degrades, run marked
  `Failed` with reason, app fine; huge grid → concurrency cap + backpressure, no node meltdown.

**Docs:** `website/docs/features/optimization.md` (+ sidebar). **Migration:** `OptimizationRun` /
`OptimizationTrial` tables.

---

### F2 — Regime Lab (regime-aware backtest analytics)

Label history into regimes and show **per-regime** performance so a trader sees where an edge lives.

**Core (`src/Core/Quant/Regimes/`):** `MarketRegime` (`Trend | Range | HighVol | Crisis`),
`RegimeLabel(DateRange, MarketRegime, Confidence)`, `RegimeSegmentedPerformance` (metrics per regime),
`IRegimeLabeler`. Complexity features as VOs: `HurstExponent`, `FuzzyEntropy`, `RealizedVolatility`.
Deterministic labeling from OHLC (rules + HMM posterior); seedable.

**Infrastructure:** `HmmRegimeLabeler` (Baum-Welch / Viterbi, fixed seed) + a cheap rules labeler
fallback. Consumes OHLC we already fetch for backtests.

**Web/UI:** backtest view gains a **Regime breakdown** (stacked bar: return/Sharpe per regime) +
"this edge is trend-only" callout. `/quant/regimes` page.

**AI hook:** `MarketSentimentAsync` already exists; add regime context to sentiment prompt.

**Tests:** unit — labeler on synthetic trend/range series returns expected labels; Hurst/entropy golden
vectors. Integration — real OHLC → labels persisted. E2E — regime chart renders. Failure — flat series
(one regime), gaps in data.

**Docs + migration** as above.

---

### F3 — Portfolio Construction & Position-Sizing layer

Institutional portfolio math across a user's cBots/copy strategies: correlation, **volatility
targeting**, **fractional-Kelly** caps, risk parity. Turns "5 unrelated bots" into a managed book.

**Core (`src/Core/Portfolio/`):** `RiskBudget`, `VolatilityTarget`, `KellyFraction` (capped ≤ 0.5×,
rejects >1), `CorrelationMatrix`, `PortfolioWeight`, `PositionSizingPolicy` (VolTarget | RiskParity |
FixedFractional | FractionalKelly). Domain service `IPortfolioAllocator` → weights + per-strategy
sizing given return series + target vol + constraints (max weight, max gross). Pure, deterministic.

**Infrastructure:** `PortfolioAllocator` impl (covariance shrinkage — Ledoit-Wolf — for stability with
few samples; this matters for retail's short histories).

**Web/UI:** **Portfolio** page: target-vol slider, sizing-policy select (dialog), resulting weights +
projected portfolio vol / max-drawdown estimate, correlation heatmap. Feeds a *recommended lot/volume*
back into copy-trading and instance launch (advisory, opt-in — never auto-trades without confirm).

**AI hook:** `PortfolioDigestAsync` already exists → enrich with allocator output + explanation.

**Tests:** unit — Kelly cap invariants, vol-target scaling, risk-parity equal-risk property, shrinkage
keeps matrix PSD. Integration — pull real instance/backtest series → weights. E2E — portfolio page +
heatmap render, dialog flow. Failure — singular covariance, single strategy, all-correlated set.

**Docs + migration.**

---

### F4 — Strategy-Health & Alpha-Decay Monitor

Formalize the existing ad-hoc `AssessStrategyDecayAsync` into a **statistical**, always-on monitor —
adaptation being the real moat (§1.2).

**Core:** `StrategyHealth` (`Healthy | Degrading | Decayed`), rolling-window PSR drift, regime-shift
alarm (F2), CUSUM/change-point on rolling Sharpe. `RaiseIf...` on a `Strategy`/`Instance` aggregate →
domain event `StrategyDecayDetected`. Reuses `AlertRule` plumbing.

**Infrastructure/Nodes:** a `BackgroundService` (sibling of `AiRiskGuard`) recomputes health on new
backtest/live equity; **degradable** — no external calls needed for the statistics; AI narrative
optional. Emits `AlertEvent`, can **auto-pause** a copy/instance (opt-in, mirrors risk-guard auto-stop).

**Tests:** unit — change-point fires on a synthetic Sharpe break; no false alarm on stationary series.
Integration — decay event persists + alert raised. E2E — decay banner + alert surface. Failure —
node death mid-scan + lease reclaim; short series (insufficient data → `Unknown`, no false alarm).

---

### F5 — Copy-Execution TCA & Smart Slicing (execution alpha)

Apply optimal-execution research to the copy mirror: measure and reduce slippage — the compounding
edge banks live on.

**Core (`src/Core/CopyTrading/Execution/`):** `TransactionCostAnalysis` (arrival-price slippage,
implementation shortfall, spread cost), `ExecutionSchedule` (slice sizes/times), `ISliceStrategy`
(Immediate | Twap | AlmgrenChriss | Adaptive). Almgren-Chriss closed-form schedule from a
`MarketImpactModel` VO (temporary+permanent impact params). Deterministic.

**Infrastructure:** wire into the mirror dispatch — large mirrored orders can slice; record realized
vs arrival price for TCA. `FakeTradingSession` **extended** (never weakened) to simulate impact +
partial fills so slicing is testable offline.

**Web/UI:** copy dashboard gains a **TCA** panel (slippage per mirrored trade, cost saved by slicing).
Slice policy chosen in the copy-config dialog.

**Tests:** unit — Almgren-Chriss schedule matches closed form; TCA arithmetic golden vectors. Integration
— mirror a large order through `FakeTradingSession`, assert slices + TCA recorded. E2E — TCA panel
renders. Failure — order rejection mid-schedule, partial-fill true-up (reuse Phase-1 G5 logic),
connection drop between slices.

---

### F6 — Point-in-Time Signal & Alt-Data plane (+ contrarian retail sentiment)

Kill look-ahead bias by construction and add the retail-accessible "alt data" that actually exists for
FX/CFD: **economic calendar, COT positioning, session/volatility features, structured news sentiment**.

**Core (`src/Core/Signals/`):** `PointInTimeSignal(AsOf, Kind, Value, Provenance)` — every signal
stamped with the `DateTimeOffset` it was **knowable** (via `TimeProvider`), never leaking future data.
`SentimentScore` (−1..+1), `RetailPositioning` (long%/short%) with a **contrarian** derived signal
(>60/<40 rule as VOs). `EconomicEvent`, `CotReport` as VOs.

**Infrastructure:** typed `HttpClient` adapters (same resilience pattern as `AnthropicAiClient`:
bounded retry, total-fail → empty/degraded, never throws into a page). Cache PIT-stamped. Sources are
pluggable + gated; app runs fully without them.

**Web/UI:** a **Signals** panel on symbol pages: sentiment gauge (with XAI "why"), contrarian arrow,
next high-impact event countdown. Backtests can **opt-in** consume PIT signals as features (feeds F1's
trial count — more features ⇒ more trials ⇒ correctly *higher* overfitting penalty; the honesty loop).

**AI hook:** structured-output sentiment via Claude with `EnableWebSearch` (already supported) →
`SentimentScore`, not free text.

**Tests:** unit — PIT stamp prevents future leakage (property test: signal.AsOf ≤ query time);
contrarian thresholds. Integration — adapter retry-then-succeed; degraded on 5xx. E2E — signals panel.
Failure — provider 429/timeout/malformed → panel shows "unavailable", app fine (mirror the AI reliability
tests).

---

### F7 — Multi-Agent Research Desk + Evidence Ledger (LLM, guardrailed)

Formalize our `AgentMandate`/`Debate` into a **TradingAgents-style** desk with a reproducible
**evidence ledger** — the explainability + audit institutions require.

**Core:** extend `AgentProposal` with `AgentRole` (Alpha | Sentiment | Risk | Execution | Portfolio |
Reviewer), an `EvidenceLedger` (owned collection: each claim → PIT signal ids + backtest ids it rests
on), and a **human-in-the-loop** `ApprovalGate` (proposal can't act until owner approves above a
risk threshold — mirrors prop-guard). Every action carries an XAI rationale.

**Infrastructure:** orchestrate via existing `AiFeatureService` (multiple role prompts + a **reviewer**
pass that checks internal consistency before any write). Deterministic checks (structured-output
validation, tool-use verification) gate the LLM per the guardrail research. Fully gated/degradable.

**Web/UI:** **Research Desk** page — role cards, debate transcript, evidence links (click a claim →
the backtest/signal it cites), approve/reject gate. Reuses `AiFeatureNotice` when no key.

**Tests:** unit — approval gate blocks act above threshold; ledger requires ≥1 evidence ref per claim
(invariant). Integration — proposal + ledger persist, gate transitions. E2E — desk renders, approve
flow. Failure — AI 5xx mid-debate → partial proposal marked incomplete, no phantom action; reviewer
rejects inconsistent output.

The approval gate is one of **three autonomy levels** (see F7B) — F7's per-action gate is the
`ApprovalGated` middle level; `FullAuto` bypasses per-trade approval within a hard risk envelope.

---

### F7B — Autonomous Live Auto-Trading ("Full Auto" mode)

Let a user **arm an `AgentMandate` to trade their live cTrader account with no per-trade approval** —
the agent decides and executes inside the **Autonomy & Safety Kernel (F0)**: hard `RiskEnvelope`,
versioned consent, circuit breaker, kill switch, durable/fault-isolated runtime, audit. Available
**by default**, white-label can disable. Full Auto is the `FullAuto` level of F0's `AutonomyLevel`; F7's
approval gate is the `ApprovalGated` level below it. Everything money-touching here **is** the kernel —
this feature adds only the mode wiring, the disclaimer copy, and the arming UI.

**Core (`src/Core/Agent/`):** `AgentMandate` gains `AutonomyLevel` + a **required** `RiskEnvelope`
before it may reach `FullAuto`; execution reuses the live copy/instance order path + **F5 TCA/slicing**.
All F0 rails apply unchanged (envelope check per order, consent to arm, circuit breaker incl. AI-down,
kill switch, watermark idempotency, white-label fail-safe).

**Disclaimer (must render before arming, MudBlazor dialog, checkbox + typed confirm):**
> **Risk warning — autonomous trading.** Full Auto lets an AI agent place, modify, and close **real
> trades on your live account with no per-trade confirmation**. CFD/FX trading on leverage can lose
> money rapidly and you may lose **more than your deposit**. AI can be wrong, act on stale or
> hallucinated information, or behave unexpectedly in fast markets; past and backtested performance
> does not predict future results, and no result is guaranteed (per CFTC/ESMA guidance). **You remain
> solely responsible for every trade the agent makes.** Trading is bounded by the risk envelope you
> set and can be stopped instantly with the Kill Switch, but stops are not guaranteed to fill at your
> price. By enabling Full Auto you confirm you understand and accept these risks.

**Web/UI:** arming lives in the Research Desk / mandate dialog — pick `AutonomyLevel`, define the
`RiskEnvelope` (required for Full Auto), accept the disclaimer. While armed: a persistent, high-visibility
**"FULL AUTO ACTIVE"** banner with a one-tap **Kill Switch** on every page; a live **autonomy activity
ledger** (each action + envelope check + XAI rationale + evidence link). Mobile-first, branded.

**Tests:** (kernel invariants — envelope/consent/circuit-breaker/kill-switch/watermark — are unit-tested
once in **F0**; here we test the mode wiring end-to-end.)
- *Integration:* armed mandate drives `FakeTradingSession` — orders execute, envelope enforced, ledger
  + audit + consent persist; toggle off mid-session disarms cleanly.
- *E2E:* Playwright — disclaimer dialog blocks arming until accepted; "FULL AUTO ACTIVE" banner + kill
  switch render and halt on click (mobile+desktop); feature-off hides the UI. Route in `PageSmokeTests`.
- *Failure paths:* broker rejection → back off, no retry storm; node death mid-auto → watermark stops
  duplicate orders; AI 5xx while armed → circuit breaker halts new entries; kill switch during in-flight
  order → no orphan/double-close.

**Docs:** `website/docs/features/autonomous-trading.md` (disclaimer, risk envelope, kill switch,
white-label switch) + sidebar. **Migration:** `AutonomyLevel`, `RiskEnvelope`, `AutoTradingConsent`,
watermark columns on mandate; `AutonomousAutoTrading` toggle seed (default on).

---

### F8 — AI Trading Journal & Coach (retail's favourite category)

Analyse the user's **own** instances + copy fills: tag by regime (F2)/session/condition, surface
behavioural leaks (revenge trading, oversizing vs F3 caps), coach.

**Core:** `TradeJournalEntry` (already have instances/fills — derive), `BehaviouralTag`
(Oversized | AgainstRegime | Overtrading | RuleBreak). Deterministic tagging rules in Core; AI adds
narrative coaching on top.

**Web/UI:** **Journal** page — tagged timeline, stats, "ask about my trading" (memory-backed AI chat,
gated). MudBlazor dialog for entry notes.

**Tests:** unit — tag rules (oversize vs Kelly cap, against-regime). Integration — journal aggregates
real fills. E2E — journal renders, chat gated when no key. Failure — no trades yet (empty state),
AI down (tags + stats still show).

---

### F9 — Agent Studio: persona-driven, no-code autonomous trading agents *(the crown; orchestrates F1–F7B)*

Let a user **create a trading agent with a character/attitude** (no code, like a personality-driven
cBot) and hand it **full management of one or many live cTrader accounts** over the Open API. The
agent's goal: **minimize drawdown, maximize profit**, with **full access to every app feature and all
cTrader data as tools**. Ships as part of the existing **AI** feature group — it *is* our
`AgentMandate` grown up, wearing a persona and driving the F7 desk + F7B execution engine.

**Grounded in the research** (persona agents, ReAct tool-use, deterministic audit-truth state store,
layered memory + reflection, risk-profiled multi-agent desks). The durable, testable core is again the
*deterministic scaffolding around the LLM*, not the LLM's guesses.

**Persona & archetypes (Core, `src/Core/Agent/Persona/`):** persona is **structured config compiled
into a system prompt** — never user code.
- `AgentArchetype` VO: `Scalper | DayTrader | SwingTrader | PositionTrader | NewsTrader | Contrarian |
  MeanReversion | BreakoutMomentum | Custom`. Each preset fixes sane defaults: typical timeframe, hold
  time, trade frequency, instrument set, eval cadence, risk posture.
- `AgentTemperament` VO: attitude axes (`Aggressive…Cautious`, `Patient…Reactive`, `Contrarian…Trend`,
  conviction, loss-aversion). Optional Big-Five/style tags. Guards keep values in range.
- `AgentPersona` = archetype + temperament + objective (`MinimizeDrawdown`/`MaximizeReturn` weights) +
  universe + the **required** `RiskEnvelope` (F7B) + the user's **goal set** (below). `CompileSystemPrompt()`
  is a **pure, deterministic** function (persona + goals → prompt text) → unit-testable, reproducible,
  no LLM to author it. Presets ship as templates; user tweaks in a no-code dialog.

**User-defined goals & targets (Core, `src/Core/Agent/Goals/`) — the steering wheel:** the user gives
each agent **specific, measurable objectives**, e.g. *keep max drawdown below 4%*, *profit factor above
1.5*, *win rate ≥ 55%*, *monthly return ≥ 3%*, *max daily loss ≤ 2%*.
- `PerformanceTarget` VO: `Metric` (`MaxDrawdown | ProfitFactor | WinRate | SharpeRatio | MonthlyReturn
  | MaxDailyLoss | Expectancy | MaxOpenPositions`), `Comparator` (`Below/Above/AtLeast/AtMost`),
  `Threshold`, and `Enforcement`:
  - **`Hard`** = guardrail: a breach (or projected breach) **de-risks or halts** the agent via the
    circuit breaker — enforced as a domain invariant alongside `RiskEnvelope` (e.g. drawdown hitting 4%
    stops new entries / flattens, exactly like a risk limit). Hard targets that overlap the envelope are
    unified so there's one source of truth.
  - **`Soft`** = objective: steers reasoning + reflection (compiled into the prompt and used to
    down-weight strategies whose slope moves the metric the wrong way) but does not itself halt.
- `AgentGoalSet` (owned by the agent aggregate): a validated collection — ctor rejects contradictory or
  absurd targets (drawdown > 100%, profit factor < 0, mutually impossible pairs) with `DomainException`.
- `IGoalEvaluator` (Core interface): given the deterministic account state + realized history →
  per-target **status** (`OnTrack | AtRisk | Breached`) + headroom (e.g. "drawdown 3.1% of 4% cap").
  Pure/deterministic → fully unit-testable; drives circuit breaker (hard), reflection (soft), and the
  UI status badges. Re-evaluated every loop against ground truth, never the LLM's self-report.

**Deterministic account state store (the anti-hallucination spine):**
`IAccountStateStore` (Core interface) exposes live **positions, orders, balance, equity, margin,
open risk** per managed `TradingAccount` — sourced from the cTrader Open API, **read-only to the
agent**. Per the research, ground truth lives here, *never* in the LLM's memory; the agent reads it via
tools and every decision is re-validated against it before dispatch. `AgentAccountMandate` references
the managed accounts **by strong ID** (`CTraderIdAccountId`) — one agent, one-or-many accounts;
`RiskEnvelope` enforced **per-account and in aggregate**.

**Tooling (ReAct loop) = the whole app + all cTrader data:** the agent acts through a **capability
allow-list** of typed tools, reusing what exists — MCP `AiTools`, our endpoints, and Open-API reads:
quotes/history/depth, F1 integrity, F2 regime, F3 sizing, F4 health, F6 signals/sentiment/calendar,
backtest/optimize (F1B), and **execution** (place/modify/close via the F5 TCA-aware live path). Each
tool call is validated (structured I/O) and **envelope-checked** before any order. The allow-list is
per-agent and owner-scoped — an agent can only touch accounts + features its mandate grants (security
boundary, not just prompt instruction).

**Layered memory + reflection (Core interface, Infra/pgvector impl):** `IAgentMemory` with three
tiers — `MarketIntelligence`, `LowLevelReflection` (short-horizon numeric signals: 3-day/rolling
return, Sharpe slope), `HighLevelReflection` (aggregated lessons). Reflection is tied to **performance
slopes** (reuse F4): deteriorating rolling Sharpe → agent de-risks / circuit-breaker trips. This is the
"learn from mistakes, don't repeat them" loop, and it's the deterministic risk-adaptation the research
credits for lower drawdown.

**Runtime (Infrastructure/Nodes) — 24/7 on the F0 durable-runtime contract:** each agent runs in a
supervised loop (sibling of `AiRiskGuard`) on the F0 contract (node lease, fault-isolation — no crash
reaches the host or account, watermark idempotency, resume on restart/failover). It runs **continuously
24/7 from the moment the user starts it until the user explicitly Stops it** (or F0's circuit breaker /
kill switch halts it) — durable state, not a session; a slow archetype still *stays live* between
decisions. `Start`/`Stop` are explicit, audited domain operations; nothing else ends a running agent.
Cadence is set by archetype (scalper = tick/seconds–minutes, position trader = hourly/daily). A watchdog
restarts a stalled agent from its watermark.

**Full audit — every step, replayable:** each loop persists an append-only `AgentDecisionRecord`
(state snapshot → XAI reasoning → tool calls → envelope/goal check → order intent → broker ack/reject →
fill), linked into the F0 evidence ledger + `AuditLog`. A reviewer can replay *why* any trade happened
against the exact point-in-time state the agent saw. Nothing the agent does is unlogged.

**Feature gating / white-label:** F0's `AutonomousAutoTrading` toggle + a finer `AgentStudio` capability;
disable ⇒ Studio hidden, agents disarmed fail-safe.

**Web/UI — no-code Agent Studio (AI nav group):**
- **Create/Edit Agent** dialog (MudBlazor, mobile-first): pick archetype card (Scalper/News/Swing/…
  with a plain-English blurb), set attitude sliders, choose managed account(s), objective weighting, the
  risk envelope, and a **Goals editor** — add measurable targets from a dropdown (Max drawdown, Profit
  factor, Win rate, Sharpe, Monthly return, Max daily loss …) with comparator + value + Hard/Soft toggle,
  each with a `HelpTip` and inline validation. Goals are **fully editable on a live agent** (bounded:
  loosening a hard risk limit re-prompts the disclaimer). Accept the F7B disclaimer to arm Full Auto.
- **Agent roster** (the at-a-glance control room): one row/card per agent showing **which agent manages
  which account(s)**, its **type/archetype**, run state (Running 24/7 / Stopped / Halted) + uptime,
  autonomy level, live P&L/drawdown, health (F4), **last action + timestamp**, next-eval countdown,
  per-goal status badges (**OnTrack / AtRisk / Breached** with headroom, e.g. "DD 3.1% / 4%"), and
  **Start / Stop / Kill Switch** controls. A managed-account column makes the agent↔account mapping
  explicit (and flags if two agents touch the same account). Persistent "AGENT LIVE" banner while any
  agent runs.
- **Agent detail**: full **logs** (searchable/filterable decision records) + **activity & reflection
  feed** — streamed `AgentDecisionRecord`s with XAI rationale + evidence links + the exact state
  snapshot the agent saw. Live updates via the existing enhanced-poll/SignalR path. This is the audit
  trail, made legible.

**AI hook:** orchestrated entirely through `IAiFeatureService` (persona system prompt + ReAct tool
loop + reviewer pass). Extends, not replaces, the existing AI layer.

**Tests:**
- *Unit:* `CompileSystemPrompt` is deterministic per persona (golden text per archetype); temperament/
  envelope guards; **state store is read-only** (agent decision referencing stale state is rejected
  against ground truth); per-account + aggregate envelope enforcement; reflection slope trips de-risk;
  watermark makes execution idempotent; capability allow-list denies out-of-scope account/feature;
  `GoalSet` rejects contradictory targets; `IGoalEvaluator` status/headroom golden vectors; a **Hard**
  drawdown target breach halts/de-risks, a **Soft** target does not.
- *Integration:* agent manages **multiple accounts** through `FakeTradingSession` + a fake Open-API
  feed end-to-end — reads state, reasons (fake `IAiClient`), envelope-checks, executes, persists full
  `AgentDecisionRecord` + audit + memory; toggle-off disarms cleanly.
- *E2E:* Playwright — create a Scalper agent no-code, arm (disclaimer blocks until accepted), see it
  appear in roster + activity feed, hit Kill Switch → halts (mobile+desktop); Studio hidden when
  feature off. Route in `PageSmokeTests`.
- *Failure paths (mandatory — reliability + audit):* AI 5xx/timeout/hallucinated tool call → caught,
  logged, agent holds, **host stays up**; Open-API disconnect/resubscribe mid-loop → no duplicate
  orders (watermark), state resynced; node death mid-decision → lease reclaim + resume; DB write fail →
  decision not acted (no unaudited trade); order rejected → back-off, logged, no retry storm; poison
  loop → circuit breaker; envelope breach attempt across any managed account → rejected + audited.

**Docs:** `website/docs/features/agent-studio.md` (personas, archetypes, tools, memory, risk envelope,
kill switch, audit trail, white-label) + sidebar. **Migration:** `AgentPersona`, `AgentArchetype`,
`AgentTemperament`, `AgentGoalSet`/`PerformanceTarget`, `AgentAccountMandate`, `AgentMemory` (pgvector),
`AgentDecisionRecord`, watermark columns; `AgentStudio` toggle seed.

---

## 4. Cross-cutting: the five focus bars (how each is met)

- **Resilience.** All statistics are pure/deterministic and never call out — they can't be taken down.
  Every external adapter (F6, F7 LLM) copies the proven `AnthropicAiClient` pattern: bounded retry +
  typed `AiResult.Fail`/degraded, never throws into a page/host. New `BackgroundService`s (F4) take a
  node lease and survive node death (reuse prop-firm lease + Phase-1 resync).
- **Full E2E testability.** Because the edge is math, E2E is deterministic — no live market needed.
  Every new route added to `PageSmokeTests`; Playwright mobile+desktop for each page; `FakeTradingSession`
  extended (never weakened) for F5. LLM features tested via a fake `IAiClient` for deterministic runs.
- **Security.** No new secrets in plaintext — any provider keys via `ISecretProtector` +
  `EncryptionPurposes`; owner-only settings pages. Evidence ledger = tamper-evident audit
  (`AuditLog`). LLM guardrails: structured-output validation + reviewer pass + human approval gate
  before any state change. PIT plane prevents accidental future-data leakage (a correctness *and*
  integrity control). **Live autonomy (F7B/F9)** routes every money-touching path through the
  **Autonomy & Safety Kernel (F0)** — hard `RiskEnvelope` checked in-aggregate before every order,
  versioned disclaimer consent to arm, deterministic circuit breaker (incl. AI-down + hard-goal
  breach), always-on kill switch, capability allow-list scoping which accounts/features an agent may
  touch, white-label fail-safe — every action on the tamper-evident evidence ledger + `AuditLog`.
- **Ease of use.** Every feature answers **"why"** in plain English (XAI) with a `HelpTip`; every
  add/edit is a MudBlazor dialog; mobile-first 360px; traffic-light verdicts over raw numbers.
  Advisory-by-default for sizing/allocation; live autonomy (F7B) only after an explicit, informed
  disclaimer opt-in and a user-set risk envelope, with a one-tap kill switch always on screen.
- **Reliability.** `TimeProvider` only (PIT depends on it); one `SaveChanges` per aggregate;
  cross-aggregate via domain events; zero-warning build + analyzer sweep; docs in same commit;
  regression test per bug. Golden-vector tests pin the math to published references so it can't drift.
  **Agents (F9) run 24/7 and must never crash the account:** the whole agent loop is fault-isolated
  (any AI/tool/Open-API/DB error is caught → hold + manage-open-risk → logged, never rethrown into the
  host), watchdog-supervised, watermark-idempotent (restart/failover can't double-fire), and survives
  app restarts + node failover via lease reclaim. **Every** agent step is append-only audited
  (`AgentDecisionRecord` + evidence ledger + `AuditLog`), fully replayable against the point-in-time
  state the agent saw; the roster/detail UI surfaces which agent manages which accounts, its type, run
  state, last action, and searchable logs.

---

## 5. Delivery plan (phased, each phase independently shippable)

Commit style: direct to `main`, logical units, three test tiers + docs in the **same** commit
(house rule). Run the analyzer sweep + `get_file_problems` + `caveman:cavecrew-reviewer` on every
multi-file phase before "done".

| Phase | Feature(s) | Why first | Rough size |
|------|-----------|-----------|-----------|
| **P1** | **F1 Backtest Integrity Lab** | Highest edge-per-risk, pure Core, no external dep, closes the #1 retail trap. Proves the pattern. | L |
| **P1B** | **F1B Native CLI Optimizer (guarded)** | Deterministic guts (state machine + mock surface + deflated ranking) ship now behind a flag; live CLI wrapper drops in the day upstream ships it. Reuses node/lease/poller. | L |
| **P2** | **F2 Regime Lab** | Feeds F3/F4/F8; deterministic. | M |
| **P3** | **F3 Portfolio & Sizing** | Depends on F1/F2 series; big retail value. | L |
| **P4** | **F4 Strategy-Health Monitor** | Reuses AlertRule + risk-guard host; needs F1/F2. | M |
| **P5** | **F6 PIT Signal plane + contrarian sentiment** | Independent; upgrades existing sentiment; enables honest feature-count in F1. | M |
| **P6** | **F5 Copy-Execution TCA & slicing** | Needs `FakeTradingSession` extension; execution alpha. | L |
| **P7** | **F7 Research Desk + Evidence Ledger** | Builds on all above as evidence sources. | L |
| **P7-K** | **F0 Autonomy & Safety Kernel** | The shared safety spine — built first *within* P7B (before any live order path), unit-tested in isolation, then reused unchanged by P9. | M |
| **P7B** | **F7B Autonomous Live Auto-Trading (Full Auto)** | Needs F0 kernel + F4 rails + F5 execution + F7 evidence/consent; highest-risk, all backstops in place. | L |
| **P8** | **F8 Journal & Coach** | Ties instances + F2 regimes + F3 caps together. | M |
| **P9** | **F9 Agent Studio (persona-driven agents)** | The crown — orchestrates F1–F7B behind a no-code persona + 24/7 supervised runtime + full audit. Ships last; every prior phase is one of its tools/rails. | XL |

**Per-phase definition of done** (in addition to house DoD): golden-vector unit tests vs published
references; new route in `PageSmokeTests`; failure-path tests named in the phase; `website/docs/features/*`
page + sidebar id; EF migration if schema changed; XAI rationale string on every user-facing output.

### First concrete steps (P1)
1. `src/Core/Quant/` — value objects (`ReturnSeries`, `SharpeRatio`, `ProbabilisticSharpeRatio`,
   `DeflatedSharpeRatio`, `ProbabilityOfBacktestOverfitting`, `TStatistic`, `TrialCount`), the
   `BacktestIntegrityReport` + `Verdict`, and `IBacktestIntegrityAnalyzer`. Pure, zero infra dep.
2. Implement the analyzer (Core if zero-dep, else `src/Infrastructure/Quant/`). Golden-vector unit
   tests first (TDD) against published PSR/DSR/PBO examples.
3. Wire `TrialCount` from the existing AI param-optimize loop (we already persist the trial history);
   model it as a source-agnostic `TrialSurface` from day one so F1B's native optimizer plugs in with
   no F1 rework.
4. Persist `BacktestIntegrityReport` on the backtest report (EF owned type / JSON column) + migration.
5. `AiEndpoints`/new `QuantEndpoints` `POST /api/quant/integrity`; MCP tool `analyze-integrity`.
6. Blazor `/quant/integrity` page + Integrity panel on the backtest view; `AiFeaturePageBase` reuse;
   `PageSmokeTests` route.
7. `website/docs/features/backtest-integrity.md` + sidebar. Sweep + reviewer + full test run.

---

## 6. Explicitly out of scope (and why)
- **Ultra-low-latency / HFT** — not our arena; edge here is validation/portfolio/execution quality.
- **Proprietary alt-data we can't source** (satellite/card) — we ship the *accessible* alt data
  (calendar/COT/positioning/sentiment) done rigorously (PIT).
- **Hand-rolling our own optimizer engine** — we do **not** build a bespoke grid/genetic engine; we
  wrap the upcoming cTrader Console CLI optimizer (F1B) exactly as we wrap `backtest`, and until it
  ships we keep the AI-proposed param loop — now *guarded* by F1 so we stop surfacing overfit winners.
  Our value-add is the **deflation/validation layer on top**, not re-implementing the sweep.
- Anything promising guaranteed returns — every surface carries the "decision-support, not oracle"
  framing per the CFTC advisory.

---

## 7. Implementation readiness

### 7.1 New Core namespaces & key types (interfaces in Core, impls in Infrastructure)

| Namespace | Aggregates / VOs (Core) | Interface (Core) → impl (Infra) | Phase |
|---|---|---|---|
| `Core.Quant` | `ReturnSeries`, `SharpeRatio`, `ProbabilisticSharpeRatio`, `DeflatedSharpeRatio`, `ProbabilityOfBacktestOverfitting`, `TStatistic`, `TrialCount`, `TrialSurface`, `BacktestIntegrityReport`, `Verdict` | `IBacktestIntegrityAnalyzer` → `BacktestIntegrityAnalyzer` | P1 |
| `Core.Optimization` | `OptimizationRun` (owns `OptimizationTrial`), `ParameterRange`, `OptimizationObjective`, `OptimizationMethod` | (dispatch via existing `ContainerCommandHelpers`/`ContainerDispatcherFactory`) | P1B |
| `Core.Quant.Regimes` | `MarketRegime`, `RegimeLabel`, `RegimeSegmentedPerformance`, `HurstExponent`, `FuzzyEntropy`, `RealizedVolatility` | `IRegimeLabeler` → `HmmRegimeLabeler` | P2 |
| `Core.Portfolio` | `RiskBudget`, `VolatilityTarget`, `KellyFraction`, `CorrelationMatrix`, `PortfolioWeight`, `PositionSizingPolicy` | `IPortfolioAllocator` → `PortfolioAllocator` (Ledoit-Wolf) | P3 |
| `Core.Health` | `StrategyHealth`, change-point/PSR-drift VOs; event `StrategyDecayDetected` | (recompute host, sibling of `AiRiskGuard`) | P4 |
| `Core.Signals` | `PointInTimeSignal`, `SentimentScore`, `RetailPositioning`, `EconomicEvent`, `CotReport` | signal-source adapters (typed `HttpClient`, `AnthropicAiClient` resilience pattern) | P5 |
| `Core.CopyTrading.Execution` | `TransactionCostAnalysis`, `ExecutionSchedule`, `MarketImpactModel` | `ISliceStrategy` → Twap/AlmgrenChriss/Adaptive | P6 |
| `Core.Autonomy` (**F0 kernel**) | `AutonomyLevel`, `RiskEnvelope`, `AutoTradingConsent`, `CircuitBreaker`; events `AutonomousActionRejected`, `AutonomyHalted` | durable-runtime contract; `AutonomousAutoTrading`/`AgentStudio` toggles | P7-K |
| `Core.Agent` (extends `AgentMandate`) | `AgentRole`, `EvidenceLedger`, `ApprovalGate` (F7); `AgentPersona`, `AgentArchetype`, `AgentTemperament`, `AgentGoalSet`/`PerformanceTarget`, `AgentAccountMandate`, `AgentDecisionRecord` (F9) | `IAccountStateStore`, `IAgentMemory`, `IGoalEvaluator` → Open-API store, pgvector memory, evaluator | P7 / P9 |

### 7.2 Build order (dependency DAG)

```
P1 Integrity ─┬─> P1B Optimizer (TrialSurface)
              ├─> P3 Portfolio ──┐
P2 Regime ────┴─> P4 Health ─────┤
P5 Signals (independent) ────────┤
P6 Execution/TCA ────────────────┤
P7 Research Desk + Ledger ───────┤
                                 ├─> P7-K F0 Kernel ─> P7B Full Auto ─> P9 Agent Studio
P8 Journal (needs P2/P3) ────────┘
```
Rule: **F0 kernel lands before any code that can place a live order.** F1's `TrialSurface`,
F2 regimes, F3 sizing, F4 health, F5 execution, F6 signals, F7 ledger are each an F9 agent *tool* — so
F9 is genuinely last and mostly integration, not new math.

### 7.3 Shared test infrastructure to build once (reused across phases)
- **`FakeAiClient`** (`IAiClient`) — scripted/deterministic completions so every LLM feature (F7/F7B/F9)
  is unit + integration testable with no network and no flakiness.
- **`FakeOpenApiFeed`** — deterministic account state + fills for `IAccountStateStore`, so F9 multi-account
  runs are reproducible offline.
- **`FakeTradingSession` extensions** (F5/F7B/F9) — market impact, partial fills, rejections; *extend,
  never weaken* (house rule).
- **Mock optimizer surface** (F1B) — fixture `(paramSet → OOS series)` matrix so integrity + ranking run
  without the real CLI image.
- **Golden-vector fixtures** — published PSR/DSR/PBO/Hurst/Almgren-Chriss worked examples pin the math.

### 7.4 Open items to confirm before the dependent phase starts
- **CLI optimizer (P1B):** exact flags, one-container-all-trials vs N-containers, results-file schema,
  grid vs genetic switches — confirm against the real image; isolate behind `ContainerCommandHelpers` +
  a parser so nothing downstream depends on the shape.
- **pgvector (P9 memory):** confirm the Postgres image/extension is available in Aspire + Testcontainers;
  else fall back to a table + in-proc cosine search.
- **Open API (P9):** rate limits / subscription model for many concurrent agents on many accounts; the
  `IAccountStateStore` must batch/cache within limits (PIT-stamped) and degrade cleanly.
- **Agent scheduling (P9):** decide the loop cadence per archetype and the max concurrent live agents
  per node (envelope on the *platform*, not just per user).

### 7.5 Cross-phase definition of done (every phase)
House DoD (0-warning build, analyzer sweep, `get_file_problems`, 3 test tiers green, docs+migration in
the same commit, `caveman:cavecrew-reviewer` on multi-file phases) **plus**: golden-vector unit tests vs
published references; new route in `PageSmokeTests`; the phase's named failure-path tests; an XAI
rationale string on every user-facing output; any live-order path proven to route through F0.
