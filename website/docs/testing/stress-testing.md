# Stress testing

Stress suite. Hammers app parts whose failure cost users money — chiefly **copy trading** — with hostile, randomized, fault-injected workloads. Asserts system stay correct. Lives in `tests/StressTests`, runs in normal `dotnet test` green gate.

## Approach — Deterministic Simulation Testing (DST)

Best way stress distributed financial systems = **deterministic simulation testing**, per TigerBeetle, FoundationDB, Antithesis: run real logic against *simulated* world, drive with **seeded** random workload + injected faults, assert invariants at quiescence. All seeded + deterministic → any failure reproduces exact from seed. Combined with:

- **Chaos-engineering fault injection** (Netflix Chaos Monkey style) — connection drops, order rejections, token rotation, node death.
- **Property-based invariants** — no assert exact call sequences; assert properties that must hold no matter how events interleave (convergence, no orphans, at-most-one lease holder).

App already ships perfect DST world model: `FakeTradingSession`, cTrader-faithful in-memory Open API session. Stress suite reuses it (linked, single source of truth) not mock, so simulated broker behave like real one.

## What it covers

### Copy trading (primary focus)

Driven via `CopyDstWorld` (`tests/StressTests/CopyTrading/`), runs live `CopyEngineHost` against fake session, issues membership-consistent source workload:

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

**Convergence invariant.** At rest, every healthy destination mirrors exactly set of still-open source positions — no orphans, none missing. Asserted on label *set* (scale-in legitimately opens second destination position under same source label, so duplicate labels expected). Destination currently rejecting orders allowed to lag, reconciled once healed.

**Lease invariant.** In cluster where nodes die + revive on seeded schedule, at most one node ever holds valid lease on a profile; dead node's lease lapses exact at expiry, gets reclaimed; healthy cluster settles with every profile held by exact one node. Mirrors `CopyEngineSupervisor`'s claim predicate against `CopyProfile` domain lease methods.

### Thread-safety of the harness

`FakeTradingSession` single-threaded; stress workload mutates it from test thread while host reads/writes from its loop. `SyncTradingSession` wraps it, makes every session operation atomic on one gate (without holding gate across reconnect callback — would invert lock order vs host's `_stateGate` and deadlock). Simulator itself left untouched.

## Bugs found

- **Startup resync race in `CopyEngineHost`.** `OnReconnected` wired before initial reference-load + first resync, which ran without `_stateGate`. Socket flap during startup ran second resync concurrent, corrupted host's non-concurrent state dicts (`_symbolDetails`, `_sourceVolumes`). Fixed: run startup load + first resync under gate. Production race, not test artifact — DST chaos workload surfaced it.

## Running

```bash
dotnet test tests/StressTests/StressTests.csproj
```

Suite **serialized** (`[assembly: CollectionBehavior(DisableTestParallelization = true)]`): each test spins live host background loop, drives to quiescence under wall clock, so parallel run starves host tasks and makes convergence timeouts flaky. Workloads sized to finish in seconds so suite stays in default green gate. Failure prints its seed; re-run that seed to reproduce exact interleaving.

## Extending

- New copy behavior → add source op to `CopyDstWorld` (keep source book membership consistent with event stream) + weighted case in `CopyChaosDstTests`. If it can create or retire a destination position, make sure convergence invariant still holds.
- New fault → add injector to `CopyDstWorld` (delegate to `FakeTradingSession`'s control surface via `SyncTradingSession`) + exercise in a named scenario plus chaos mix.
- Keep simulator cTrader-faithful (see root `CLAUDE.md` mandate); never weaken it to make a stress test pass.