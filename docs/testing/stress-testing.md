# Stress testing

A stress suite that hammers the parts of the app whose failure costs users money — above all
**copy trading** — with hostile, randomized, fault-injected workloads and asserts the system stays
correct. Lives in `tests/StressTests`, runs in the normal `dotnet test` green gate.

## Approach — Deterministic Simulation Testing (DST)

The industry-leading way to stress distributed financial systems is **deterministic simulation
testing**, as used by TigerBeetle, FoundationDB, and Antithesis: run the real logic against a
*simulated* world, drive it with a **seeded** random workload plus injected faults, and assert
invariants at quiescence. Because everything is seeded and deterministic, any failure reproduces
exactly from its seed. We combine this with:

- **Chaos-engineering fault injection** (Netflix Chaos Monkey style) — connection drops, order
  rejections, token rotation, node death.
- **Property-based invariants** — instead of asserting exact call sequences, assert properties that
  must hold no matter how events interleave (convergence, no orphans, at-most-one lease holder).

The app already ships the perfect DST world model: `FakeTradingSession`, a cTrader-faithful
in-memory Open API session. The stress suite reuses it (linked, single source of truth) rather than
mocking, so the simulated broker behaves like the real one.

## What it covers

### Copy trading (primary focus)

Driven through `CopyDstWorld` (`tests/StressTests/CopyTrading/`), which runs a live `CopyEngineHost`
against the fake session and issues a membership-consistent source workload:

| Scenario | Stresses |
|---|---|
| `Mass_fan_out…` | 1 source → 80 destinations, 150 opens then closes; full fan-out + drain |
| `High_frequency_open_close…` | 300 rapid interleaved open/close; no leaked positions |
| `Partial_close_and_scale_in_storm…` | partial-close + scale-in churn; label-set stability |
| `Connection_flap_storm…` | repeated socket disconnect/reconnect + mid-flight desync; resync convergence |
| `Order_rejection_cascade…` | a subset rejects every order; healthy destinations unaffected, then self-heal via resync |
| `Token_rotation_storm…` | rapid in-place token swaps during an order storm |
| `Randomized_chaos_workload…` (10 seeds) | **the DST core** — every event type + every fault interleaved unpredictably |
| `CopyLeaseReclaimStressTests` | node death + lease reclaim across a scaled cluster (pure domain, `FakeTimeProvider`) |

**Convergence invariant.** At rest, every healthy destination mirrors exactly the set of still-open
source positions — no orphans, none missing. Asserted on the label *set* (a scale-in legitimately
opens a second destination position under the same source label, so duplicate labels are expected).
A destination currently rejecting orders is allowed to lag and is reconciled once healed.

**Lease invariant.** In a cluster where nodes die and revive on a seeded schedule, at most one node
ever holds a valid lease on a profile; a dead node's lease lapses exactly at expiry and is reclaimed;
a healthy cluster settles with every profile held by exactly one node. Mirrors
`CopyEngineSupervisor`'s claim predicate against the `CopyProfile` domain lease methods.

### Thread-safety of the harness

`FakeTradingSession` is single-threaded; the stress workload mutates it from the test thread while
the host reads/writes it from its loop. `SyncTradingSession` wraps it and makes every session
operation atomic on one gate (without holding that gate across the reconnect callback, which would
invert lock order against the host's `_stateGate` and deadlock). The simulator itself is left
untouched.

## Bugs found

- **Startup resync race in `CopyEngineHost`.** `OnReconnected` was wired before the initial
  reference-load + first resync, which ran without `_stateGate`. A socket flap during startup ran a
  second resync concurrently and corrupted the host's non-concurrent state dictionaries
  (`_symbolDetails`, `_sourceVolumes`). Fixed by running the startup load + first resync under the
  gate. This is a production race, not a test artifact — the DST chaos workload surfaced it.

## Running

```bash
dotnet test tests/StressTests/StressTests.csproj
```

The suite is **serialized** (`[assembly: CollectionBehavior(DisableTestParallelization = true)]`):
each test spins a live host background loop and drives it to quiescence under a wall clock, so
running them in parallel starves the host tasks and makes convergence timeouts flaky. Workloads are
sized to finish in seconds so the suite stays in the default green gate. A failure prints its seed;
re-run that seed to reproduce the exact interleaving.

## Extending

- New copy behavior → add a source op to `CopyDstWorld` (keep the source book membership consistent
  with the event stream) and a weighted case in `CopyChaosDstTests`. If it can create or retire a
  destination position, make sure the convergence invariant still holds.
- New fault → add an injector to `CopyDstWorld` (delegating to `FakeTradingSession`'s control
  surface via `SyncTradingSession`) and exercise it in a named scenario plus the chaos mix.
- Keep the simulator cTrader-faithful (see the root `CLAUDE.md` mandate); never weaken it to make a
  stress test pass.
