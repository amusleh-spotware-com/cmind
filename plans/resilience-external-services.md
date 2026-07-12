# Resilience Plan — Unreliable External Services (cTrader Open API, Algo CLI, Nodes, AI)

Status: PLAN ONLY — no implementation. Author baseline: 2026-07-11.

## 0. Goal & non-negotiables

Treat **every** external dependency as hostile-unreliable: cTrader Open API socket, cTrader
Console/Algo CLI, remote node agents, Anthropic AI API. Assume: long weekend maintenance,
silent hangs, garbage/absent error messages, half-open sockets, duplicated/dropped events,
token invalidation mid-flight, node crash mid-operation.

**Definition of "recovered": when the external service comes back, the system re-converges to the
correct state with zero lost intent and zero duplicated side effects — automatically, no operator.**
The source of truth is always the broker/cTrader, never our in-memory state.

Every scenario below ships with unit + integration + E2E + stress coverage that is **deterministic,
reproducible, machine-independent** (virtual clock, in-memory fakes, Testcontainers, seeded RNG).

---

## 1. What we already have (baseline — do NOT rebuild)

| Area | Mechanism | File |
|---|---|---|
| Socket reconnect | Exponential backoff (`BackoffPolicy`), reset on connect | `CTraderOpenApi/OpenApiConnection.cs` |
| Maintenance handling | `Maintenance` error kind → wait until `MaintenanceEndsAt` (clamped) | `OpenApiConnection.MaintenanceDelay` |
| Dead-socket detection | Inbound watchdog + heartbeat pump | `OpenApiConnection.WatchdogAsync/HeartbeatPumpAsync` |
| Error taxonomy | `Recoverable / Fatal / TokenInvalid / Maintenance` classification | `CTraderOpenApi/OpenApiError.cs` |
| Reconnect reconcile | `OnReconnected` → full broker resync (orphan-close + missing-reopen) | `CopyEngine/CopyEngineHost.ResyncAsync` |
| Rejection storm guard | G8 per-destination circuit breaker (bypassed on resync) | `CopyEngineHost` |
| Partial-fill repair | G5 one-shot true-up on next resync | `CopyEngineHost` |
| Token rotation | In-place `SwapAccessTokenAsync` on live socket, no stream drop | `CopyEngineHost.ConsumeTokenUpdatesAsync` |
| Node ownership | Lease claim/renew/release, `FOR UPDATE SKIP LOCKED`, dead-host watchdog | `Nodes/CopyTrading/CopyEngineSupervisor.cs` |
| Graceful shutdown | Lease release on SIGTERM so survivor reclaims fast | `CopyEngineSupervisor.StopAsync` |
| Proactive token refresh | Refresh-before-expiry background service | `Nodes/CopyTrading/OpenApiTokenRefreshService.cs` |
| Fake fidelity | Rejections, partial fill, token-invalid, volume bounds, socket drop/restore | `tests/UnitTests/CopyTrading/FakeTradingSession.cs` |
| Stress harness | Deterministic-sim (DST) copy-trading stress suite | `tests/StressTests` |

The reconnect-resync model is architecturally correct (broker = source of truth). This plan
**hardens the edges, closes the orphan gaps, and proves it all with tests** — it does not replace it.

---

## 2. Industry research — how fintech/trading/banking handle this

Distilled patterns from FIX-engine gateways, exchange connectivity layers, and payment rails,
mapped to what we adopt:

1. **Broker/exchange is the ledger of record; local state is a cache.** On any reconnect, *reconcile
   from the venue*, never replay local intent blindly. → We do this (resync). **Extend:** make it
   the universal recovery primitive for *every* actor (runs/backtests too, not just copy).
2. **Idempotent, correlated orders.** Every order carries a stable client-side key so a resend after
   an ambiguous timeout is deduplicated by the venue. → We label copies with source position/order
   id. **Gap:** market-order sends on ambiguous timeout are not yet idempotency-keyed; adopt a
   deterministic client-msg-id / `label` dedup pass in resync (already partially there).
3. **Bounded exponential backoff + full jitter** on reconnect to avoid thundering herd against a
   just-recovered venue. → Backoff exists; **add full jitter** (currently plain exponential).
4. **Separate "maintenance" from "outage".** Scheduled maintenance → long patient wait; unexpected
   outage → aggressive retry. → We classify Maintenance vs Recoverable. Keep + test long-weekend.
5. **Circuit breakers per counterparty** to stop hammering a failing endpoint and to shed load. →
   G8 breaker exists for copy destinations; **add** a breaker to node HTTP + AI HTTP.
6. **Persistent, append-only audit journal** of every intent and outcome for post-incident replay,
   audits, and legal claims. → `AuditChainInterceptor` (tamper-evident chain) exists; **route
   connection/order/reconnect lifecycle events into an immutable audit stream** with correlation IDs.
7. **Heartbeat + lease + fencing** so a partitioned node can't double-act. → Lease + SKIP LOCKED
   exists for copy; **extend fencing to run/backtest instances**.
8. **Reconciliation loops as first-class background services**, not one-shot startup code. → We have
   several pollers; **add the missing node-death → running-instance reclaim loop**.
9. **Dead-letter + replay** for actions that fail after all retries, surfaced to operators. → **New:
   copy/node/AI dead-letter table + operator view + manual replay.**
10. **Observable SLOs**: reconnect time, resync duration, event-age, order-latency, breaker trips. →
    `CopyMetrics` exists; **standardize a resilience metric set + structured logs across all actors**.

---

## 3. Workstreams

### WS-1 — cTrader Open API socket & session hardening

**W1.1 Full-jitter backoff.** Add jitter to `BackoffPolicy.NextDelay()` (currently deterministic
exponential). Test: N reconnects never collide within a tolerance; delay stays within `[base, max]`.

**W1.2 Long-maintenance survival.** `MaintenanceMaxDelay` is 6h; a weekend can exceed that. Make the
maintenance wait *loop* (re-check `MaintenanceEndsAt`, keep waiting past the clamp) instead of
falling through to a tight reconnect loop that pounds a down server. Emit periodic
`MaintenanceStillDown` heartbeat log. Test: 60h simulated maintenance → exactly one clean reconnect
+ resync when it ends, no busy loop in between.

**W1.3 Ambiguous-send safety.** A `SendAsync` that times out (no response) after the socket flaps
may or may not have reached the venue. Today the pending TCS is failed and the action is retried on
resync. Confirm+test: a market open whose ACK was lost is **not** double-placed after reconnect
(resync sees the labelled position and skips). Add explicit test for the "sent, ACK lost, reconnect"
race in `FakeTradingSession` (new `DropAckForNextOrder` flag).

**W1.4 Garbage/partial frame defense.** `TcpSslOpenApiTransport` throws on invalid length; ensure a
malformed frame triggers reconnect not process crash (it does via run loop). Add fuzz-ish test:
inject a truncated/oversized frame → transport throws → run loop reconnects → resync converges.

**W1.5 Token-invalid inline recovery.** `TokenInvalid` is classified but the connection's run loop
only special-cases `Fatal`/`Maintenance`. Define behavior: a `TokenInvalid` during account-auth must
(a) not spin-fail forever, (b) signal the supervisor to force a token refresh + swap, (c) alert.
Wire `OnReconnected`/host to request an out-of-band refresh. Test: token invalidated mid-run →
refresh service rotates → host swaps → resync → copying resumes; assert no busy-fail loop.

**Acceptance:** socket faults, maintenance, token-invalid, and malformed frames each converge to a
correct resynced state deterministically in tests.

### WS-2 — Copy engine durability & no-lost-intent

**W2.1 Reconnect resync is the recovery contract — lock it with tests, not code churn.** Add an
exhaustive resync test matrix (unit, on `FakeTradingSession`):
- master opened while socket down → slave opens on resync
- master closed while down → slave orphan closed (mid-run) / preserved (initial, per SyncClosed flag)
- master partial-closed while down → slave trued to proportion
- master scaled-in while down → slave scaled
- slave partially filled → G5 true-up on next resync
- destination token rotated during outage → swap then resync
- destination circuit tripped → resync still reconverges (bypass), live opens still gated

**W2.2 Persist a copy dead-letter.** When an action fails after retries/breaker (e.g. `NotEnoughMoney`
persistent, symbol not tradable), append to a `CopyDeadLetter` record (profile, ctid, source id,
reason, attempts, first/last seen) so it's visible and manually replayable — never silently dropped.
DDD: new aggregate/child under the copy context; write via intention method. Test: integration
(Testcontainers) asserts row written + E2E surfaces it in the copy notifications feed.

**W2.3 Host crash mid-dispatch.** Supervisor watchdog already restarts a dead host; add a test that a
host killed mid-fan-out is restarted and the fresh host's initial resync converges the book (no
duplicate opens because of id labels). Stress: kill+restart hosts under load in DST.

**Acceptance:** for every "master did X while we were disconnected/crashed" there is a passing test
proving convergence with no duplicate/lost trade.

### WS-3 — Node communication & node-death recovery

**W3.1 Resilient node HTTP client.** The `"node-agent"` HttpClient has **no** resilience pipeline.
Add `AddStandardResilienceHandler` (or explicit Polly): per-attempt timeout, retry with jitter on
transient 5xx/timeout for **idempotent** ops (status/report/stats/logs), **no blind retry** on
`Start` (non-idempotent — instead reconcile), and a per-node circuit breaker. `StopAsync` must
observe failures (today it ignores the response) and escalate. Test: integration with a stub agent
returning 503/timeout then 200 → op succeeds after retry; `Start` failure does not double-start
(assert single container by `app.instance` label).

**W3.2 Node-death → running-instance reclaim (CRITICAL GAP).** `InstanceReconciler` only fails stuck
`Pending/Starting` instances. A **`Running` instance on a node that crashed or went unreachable is
orphaned** — no loop reclaims it. Add a reconciliation path: when a node is `MarkUnreachable` (or a
`Running`/`Stopping` instance's node lease is stale), transition its running instances to a
recoverable state — reschedule on a healthy node (runs) or mark `Failed` with a clear reason
(backtests, since they self-exit). Model transitions on the `Instance` aggregate (DDD), not in the
poller. Test: unit (transition rules), integration (dead node → instances reclaimed), E2E (node goes
away, dashboard shows instance rescheduled/failed, not stuck "Running" forever).

**W3.3 Fencing for instances.** Give run/backtest scheduling the same lease/fencing guarantee copy
profiles have, so a partitioned-but-alive node and its replacement can't both act on one instance.
Reuse the lease pattern. Test: two nodes race to reclaim → exactly one wins (`SKIP LOCKED`).

**W3.4 Agent self-registration/heartbeat under flap.** Confirm `NodeRegistrationClient` re-registers
after the main node restarts and after its own network blip; `NodeHeartbeatMonitor` already flags
stale. Add stress test: nodes flap register/heartbeat repeatedly → no duplicate node rows (upsert by
name), scheduler never places on unreachable.

**Acceptance:** kill any node at any lifecycle point → its work is reclaimed or cleanly failed with a
recorded reason; no orphaned "Running" rows; no double-execution.

### WS-4 — cBot / Algo Console CLI resilience

**W4.1 Container/CLI unreliability.** The Console CLI can hang, exit nonzero with no report, or
produce a partial report. Pollers (`RunCompletionPoller`, `BacktestCompletionPoller`) reconcile exit
codes; ensure: a hung container past a max-runtime is force-stopped + failed; a missing report →
`Failed` with reason (backtest already does); a partial/corrupt `ReportJson` is caught by
`ParseEquityCurve` without throwing (defensive parse). Test: integration feeds truncated/garbage
report JSON → instance `Failed` gracefully, no crash.

**W4.2 Build sandbox failures.** `CBotBuilder` runs `dotnet build` in a throwaway container; a Docker
daemon hiccup or image-pull failure must surface as a clean build error, retried once for transient
pull failures. Test: simulate pull failure → clear user-facing error, no partial artifact.

**Acceptance:** no run/backtest can hang forever or corrupt state on CLI misbehavior.

### WS-5 — AI feature resilience (Anthropic API)

**W5.1 Resilient AI HTTP.** `AnthropicAiClient` (typed HttpClient) needs: per-request timeout,
bounded retry with jitter on 429/5xx honoring `Retry-After`, and a circuit breaker so a provider
outage doesn't stall `AiRiskGuard`/endpoints. Already degrades to `AiResult.Fail` when key unset —
extend graceful-fail to *runtime* failures (timeouts, 5xx, malformed JSON) returning `AiResult.Fail`
with a typed reason, never throwing into a page/tool/hosted service.

**W5.2 Bad-output defense.** Model returns non-JSON, truncated, or oversized output → parse defensively,
fail closed. `generate-project` self-repair loop already bounded (≤3); assert it terminates on
persistent failure. `AiRiskGuard` must skip a cycle on AI failure, not crash the service.

**W5.3 Tests for every AI feature failure path.** For each of the 10 features + MCP `AiTools`:
timeout, 429, 5xx, malformed body, empty body, key-disabled → each yields a graceful typed failure.
E2E: Assistant page renders an error state (no Blazor error UI) when AI backend is down (fault-inject
via a stub `IAiClient`).

**Acceptance:** AI provider can do anything; app stays up, features degrade gracefully, all paths tested.

### WS-6 — Token lifecycle robustness

**W6.1 Refresh failure backoff + escalation.** `OpenApiTokenRefreshService` marks
`MarkRefreshFailed` but retries only on the next fixed interval; during a provider/maintenance outage
a token can expire before the next window. Add: shortened retry cadence for failed auths, an alert
when an auth is within X of expiry and still failing, and a "refresh endpoint down" circuit. Test
(FakeTimeProvider): refresh endpoint 503 across several cycles → escalating retries + alert fired;
recovers when endpoint returns.

**W6.2 Single-valid-token invariant.** Linking another account on the same cID invalidates the prior
token; ensure the swap path + resync always run so no host holds a dead token. Test already partially
covered — extend to the outage-during-relink case.

**Acceptance:** tokens never silently expire; failures are visible and self-heal on recovery.

---

## 4. FakeTradingSession — new fidelity to add (WS-1..WS-6 depend on these)

Extend the cTrader stand-in (keep it faithful, never weaken to pass a test — per CLAUDE.md):
- `DropAckForNextOrder(ctid)` — models "sent, ACK lost" ambiguous send (W1.3).
- `EnterMaintenance(until)` / `ExitMaintenance()` — server returns Maintenance error with
  `MaintenanceEndsAt`, then a long/weekend-length window on the virtual clock (W1.2).
- `InjectMalformedFrame()` — transport-level corrupt frame (W1.4). (Requires a transport-level fake or
  a hook; may live at `IOpenApiTransport` fidelity instead.)
- `FlapSocket(times, interval)` — repeated drop/restore to exercise repeated resync (W2.x).
- `InvalidateToken(token)` already exists — add "invalidate mid-request" timing (W1.5/W6).
- Persistent vs transient rejection distinction so breaker/dead-letter paths differ (W2.2).
All driven by a `FakeTimeProvider` virtual clock — zero real waits, fully deterministic.

Node-side fake: a stub `CtraderCliNode` agent (in-memory HTTP) that can return 5xx/timeout/partial and
simulate crash (stop responding) — for WS-3 integration tests.

AI fake: a stub `IAiClient` returning timeout/429/5xx/malformed/empty — for WS-5.

---

## 5. Testing strategy — reliable, reproducible, cross-machine

Mandatory for **every** item above; nothing is "done" without all applicable tiers:

- **Unit** (xUnit + FluentAssertions + NSubstitute, `FakeTimeProvider`, `FakeTradingSession`):
  invariants & transitions — reconnect convergence, breaker trip/reset, backoff bounds, token swap,
  instance transitions, AI graceful-fail. No real time, no network, no sleeps.
- **Integration** (Testcontainers Postgres, stub agent, stub AI): lease reclaim, node-death
  instance reclaim, dead-letter persistence, refresh escalation, HTTP resilience pipeline behavior.
  Deterministic containers → identical on any machine/CI.
- **E2E** (Playwright via `AppFixture`, per CLAUDE.md mandate): fault-inject external services
  (maintenance, node death, AI down, token invalid) and assert the **UI** shows correct state
  (rescheduled/failed/degraded), pages render without the Blazor error UI, notifications appear.
  Add API-level E2E for copy-only paths. Add new routes to `PageSmokeTests`.
- **Stress / DST** (`tests/StressTests`): deterministic simulation — thousands of events with
  scheduled socket flaps, node kills, token rotations, maintenance windows, all on a seeded RNG +
  virtual clock, asserting **final book convergence** and **no lost/duplicate trade** invariants.
  Reproducible by seed; a failing seed is a permanent regression fixture.

Reproducibility rules: seeded `Random`, injected `TimeProvider`, no wall-clock, no `Task.Delay` on
real time in tests, no external network — so a red test means a real bug, identically on every box.

---

## 6. Structured logging & audit (industry / regulatory grade)

Logs may back legal claims, audits, regulation → they must be complete, structured, correlated,
immutable where it matters.

- **Correlation:** stamp every copy/order/connection/node/AI event with `profileId` / `instanceId` /
  `nodeName` / `ctidTraderAccountId` and the existing OTel `trace_id`/`span_id` (already enriched).
  Add a per-session `connectionId` and per-action `correlationId`.
- **Lifecycle events as first-class structured logs** via `Core/Logging/LogMessages.cs` (never raw
  `ILogger.Log*`): connect, reconnect(attempt,delay), maintenance-enter/still-down/exit,
  token-refresh/swap/fail, order placed/rejected/dead-lettered, resync start/summary(counts),
  breaker trip/reset, node unreachable/reclaimed, instance reclaimed/failed(reason), AI call
  fail(reason). Each with stable `EventId`.
- **Immutable audit stream:** route the money-affecting/decision events (order placed/closed,
  flatten, prop-rule breach, account-protection trigger, dead-letter) through the tamper-evident
  `AuditChainInterceptor` chain (already exists) so the record is verifiable for audits/claims.
- **Metrics/SLOs** (extend `CopyMetrics` + add node/AI): reconnect-duration, resync-duration,
  event-age, order-latency, breaker-trips, dead-letter-count, node-reclaim-count, AI-failure-rate,
  token-refresh-failures. Exported via existing OTel pipeline (OTLP / Azure Monitor / X-Ray).
- **User vs owner views:** end-user notifications (copy feed, already present) for user-actionable
  events; owner/operator dashboards + logs for infra events (node death, breaker, dead-letter).

---

## 7. Deliverables & sequencing

1. **WS-1** socket hardening (jitter, long-maintenance loop, ambiguous-send, token-invalid inline) + tests.
2. **WS-3** node HTTP resilience + **node-death running-instance reclaim (critical)** + fencing + tests.
3. **WS-2** copy dead-letter + resync test matrix + host-crash tests.
4. **WS-6** token refresh backoff/escalation + tests.
5. **WS-4** CLI/container hardening + tests.
6. **WS-5** AI resilience + full failure-path tests.
7. **WS-Fakes** FakeTradingSession + stub agent + stub AI fidelity (interleaved — each WS needs its fake bits first).
8. **Logging/audit/metrics** standardization across all actors (cross-cutting, land alongside each WS).
9. **Docs:** update `docs/features/*` (copy trading, nodes, AI) + `docs/operations/*` (node-discovery,
   logging) in the same commits (CLAUDE.md docs-in-sync mandate).

## 8. Definition of done (whole plan)

- Every external service can fail/hang/lie at any moment and the system converges on recovery with
  no lost intent, no duplicate side effect — proven by deterministic unit+integration+E2E+stress tests.
- No orphaned `Running` instances; node death always reclaimed or cleanly failed with a reason.
- All failure paths for copy, nodes, CLI, AI, tokens have tests (happy + every failure branch).
- Structured, correlated, audit-grade logs + resilience metrics for every lifecycle event.
- Feature + ops docs updated. `dotnet test` green. DDD checklist satisfied for all new domain code.
