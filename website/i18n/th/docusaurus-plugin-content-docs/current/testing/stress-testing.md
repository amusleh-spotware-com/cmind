---
description: "Stress suite. Hammers app parts whose failure cost users money — chiefly copy trading — with hostile, randomized, fault-injected workloads. Asserts system…"
---

# Stress testing

stress suite hammers app parts ที่ failure cost users money — chiefly **copy trading** — ด้วย hostile randomized fault-injected workloads asserts system stay correct lives ใน `tests/StressTests` runs ใน normal `dotnet test` green gate

## Approach — Deterministic Simulation Testing (DST)

best way stress distributed financial systems = **deterministic simulation testing** per TigerBeetle FoundationDB Antithesis: run real logic against *simulated* world drive ด้วย **seeded** random workload + injected faults assert invariants ที่ quiescence ทั้งหมด seeded + deterministic → any failure reproduces exact จาก seed combined ด้วย:

- **Chaos-engineering fault injection** (Netflix Chaos Monkey style) — connection drops order rejections token rotation node death
- **Property-based invariants** — ไม่มี assert exact call sequences; assert properties ที่ must hold ไม่มี matter how events interleave (convergence ไม่มี orphans at-most-one lease holder)

app already ships perfect DST world model: `FakeTradingSession` cTrader-faithful in-memory Open API session stress suite reuses มัน (linked single source ของ truth) ไม่ mock ดังนั้น simulated broker behave like real one

## What มันcovers

### Copy trading (primary focus)

driven ผ่าน `CopyDstWorld` (`tests/StressTests/CopyTrading/`) runs live `CopyEngineHost` against fake session issues membership-consistent source workload:

| Scenario | Stresses |
|---|---|
| `Mass_fan_out…` | 1 source → 80 destinations 150 opens จากนั้น closes; full fan-out + drain |
| `High_frequency_open_close…` | 300 rapid interleaved open/close; ไม่มี leaked positions |
| `Partial_close_and_scale_in_storm…` | partial-close + scale-in churn; label-set stability |
| `Connection_flap_storm…` | repeated socket disconnect/reconnect + mid-flight desync; resync convergence |
| `Order_rejection_cascade…` | subset rejects ทุก order; healthy destinations unaffected จากนั้น self-heal ผ่าน resync |
| `Token_rotation_storm…` | rapid in-place token swaps ระหว่าง order storm |
| `Randomized_chaos_workload…` (10 seeds) | **DST core** — ทุก event type + ทุก fault interleaved unpredictably |
| `CopyLeaseReclaimStressTests` | node death + lease reclaim ข้าม scaled cluster (pure domain `FakeTimeProvider`) |

**Convergence invariant** ที่ rest ทุก healthy destination mirrors exactly set ของ still-open source positions — ไม่มี orphans none missing asserted on label *set* (scale-in legitimately opens second destination position ภายใต้ same source label ดังนั้น duplicate labels expected) destination currently rejecting orders allowed ไป lag reconciled once healed

**Lease invariant** ใน cluster ที่ nodes die + revive on seeded schedule at most one node ever holds valid lease on profile; dead node ของ lease lapses exact ที่ expiry gets reclaimed; healthy cluster settles ด้วย ทุก profile held โดย exact one node mirrors `CopyEngineSupervisor` ของ claim predicate against `CopyProfile` domain lease methods

### Thread-safety ของ harness

`FakeTradingSession` single-threaded; stress workload mutates มัน จาก test thread ขณะที่ host reads/writes จาก loop ของมัน `SyncTradingSession` wraps มัน makes ทุก session operation atomic on one gate (ไม่มี holding gate ข้าม reconnect callback — would invert lock order vs host ของ `_stateGate` และ deadlock) simulator itself left untouched

## Bugs found

- **Startup resync race ใน `CopyEngineHost`** `OnReconnected` wired ก่อน initial reference-load + first resync ซึ่ง ran ไม่มี `_stateGate` socket flap ระหว่าง startup ran second resync concurrent corrupted host ของ non-concurrent state dicts (`_symbolDetails` `_sourceVolumes`) fixed: run startup load + first resync ภายใต้ gate production race ไม่ test artifact — DST chaos workload surfaced มัน

## Running

```bash
dotnet test tests/StressTests/StressTests.csproj
```

suite **serialized** (`[assembly: CollectionBehavior(DisableTestParallelization = true)]`): ทุก test spins live host background loop drives ไป quiescence ภายใต้ wall clock ดังนั้น parallel run starves host tasks และ makes convergence timeouts flaky workloads sized ไป finish ใน seconds ดังนั้น suite stays ใน default green gate failure prints seed; re-run นั่น seed ไป reproduce exact interleaving

## Extending

- new copy behavior → add source op ไป `CopyDstWorld` (keep source book membership consistent ด้วย event stream) + weighted case ใน `CopyChaosDstTests` ถ้า มัน can create หรือ retire destination position make sure convergence invariant still holds
- new fault → add injector ไป `CopyDstWorld` (delegate ไป `FakeTradingSession` ของ control surface ผ่าน `SyncTradingSession`) + exercise ใน named scenario บวก chaos mix
- keep simulator cTrader-faithful (ดู root `CLAUDE.md` mandate); never weaken มัน ไป make stress test pass
