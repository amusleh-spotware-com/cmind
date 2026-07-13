---
description: "Stress suite. Hammers app parts których failure kosztuje users money — chiefly copy trading — z hostile, randomized, fault-injected workloads. Asserts system…"
---

# Stress testing

Stress suite. Hammers app parts których failure kosztuje users money — chiefly **copy trading** — z
hostile, randomized, fault-injected workloads. Asserts system stay correct. Żyje w `tests/StressTests`,
runs w normalnym `dotnet test` green gate.

## Approach — Deterministic Simulation Testing (DST)

Najlepszy sposób stress distributed financial systems = **deterministic simulation testing**, per
TigerBeetle, FoundationDB, Antithesis: run real logic przeciwko *simulated* world, drive z
**seeded** random workload + injected faults, assert invariants na quiescence. Wszystkie seeded +
deterministic → każdy failure reproduces exact z seed. Combined z:

- **Chaos-engineering fault injection** (Netflix Chaos Monkey style) — connection drops, order
  rejections, token rotation, node death.
- **Property-based invariants** — brak assert exact call sequences; assert properties że muszą hold
  no matter jak events interleave (convergence, brak orphans, at-most-one lease holder).

App już wysyła perfect DST world model: `FakeTradingSession`, cTrader-faithful in-memory Open API
session. Stress suite reuses to (linked, single source of truth) nie mock, więc simulated broker
behave jak real jeden.

## Co to covers

### Copy trading (primary focus)

Driven poprzez `CopyDstWorld` (`tests/StressTests/CopyTrading/`), runs live `CopyEngineHost`
przeciwko fake session, issues membership-consistent source workload:

| Scenario | Stresses |
|---|---|
| `Mass_fan_out…` | 1 source → 80 destinations, 150 opens potem closes; full fan-out + drain |
| `High_frequency_open_close…` | 300 rapid interleaved open/close; brak leaked positions |
| `Partial_close_and_scale_in_storm…` | partial-close + scale-in churn; label-set stability |
| `Connection_flap_storm…` | repeated socket disconnect/reconnect + mid-flight desync; resync convergence |
| `Order_rejection_cascade…` | subset rejects każdy order; healthy destinations unaffected, potem self-heal poprzez resync |
| `Token_rotation_storm…` | rapid in-place token swaps podczas order storm |
| `Randomized_chaos_workload…` (10 seeds) | **DST core** — każdy event type + każdy fault interleaved unpredictably |
| `CopyLeaseReclaimStressTests` | node death + lease reclaim across scaled cluster (pure domain, `FakeTimeProvider`) |

**Convergence invariant.** Na rest, każdy healthy destination mirrors exactly set z still-open source
positions — brak orphans, none missing. Asserted na label *set* (scale-in legitimately opens second
destination position pod tą samą source label, więc duplicate labels expected). Destination currently
rejecting orders allowed do lag, reconciled raz healed.

**Lease invariant.** W cluster gdzie nodes die + revive na seeded schedule, at most jeden node ever
holds valid lease na profile; dead node'a lease lapses exact przy expiry, gets reclaimed; healthy
cluster settles z każdy profile held przez exact jeden node. Mirrors `CopyEngineSupervisor'a claim
predicate przeciwko `CopyProfile` domain lease methods.

### Thread-safety z harness

`FakeTradingSession` single-threaded; stress workload mutates to z test thread podczas host reads/writes
z jego loop. `SyncTradingSession` wraps to, makes każdy session operation atomic na jeden gate (bez
holding gate across reconnect callback — would invert lock order vs host'a `_stateGate` i deadlock).
Simulator sam left untouched.

## Bugs found

- **Startup resync race w `CopyEngineHost`.** `OnReconnected` wired zanim initial reference-load + first
  resync, który ran bez `_stateGate`. Socket flap podczas startup ran second resync concurrent,
  corrupted host'a non-concurrent state dicts (`_symbolDetails`, `_sourceVolumes`). Fixed: run startup
  load + first resync pod gate. Production race, nie test artifact — DST chaos workload surfaced to.

## Running

```bash
dotnet test tests/StressTests/StressTests.csproj
```

Suite **serialized** (`[assembly: CollectionBehavior(DisableTestParallelization = true)]`): każdy
test spins live host background loop, drives do quiescence pod wall clock, więc parallel run starves
host tasks i makes convergence timeouts flaky. Workloads sized do finish w seconds więc suite stays
w default green gate. Failure prints its seed; re-run że seed do reproduce exact interleaving.

## Extending

- Nowe copy behavior → add source op do `CopyDstWorld` (keep source book membership consistent z event
  stream) + weighted case w `CopyChaosDstTests`. Jeśli to może create albo retire destination position,
  make sure convergence invariant ciągle holds.
- Nowe fault → add injector do `CopyDstWorld` (delegate do `FakeTradingSession'a control surface poprzez
  `SyncTradingSession`) + exercise w named scenario plus chaos mix.
- Keep simulator cTrader-faithful (zobacz root `CLAUDE.md` mandate); nigdy nie weaken to aby make
  stress test pass.
