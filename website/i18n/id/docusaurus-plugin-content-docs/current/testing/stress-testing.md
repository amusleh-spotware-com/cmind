---
description: "Suite stress. Hammer app part yang failure cost user uang — chiefly copy trading — dengan hostile, randomized, fault-injected workload. Assert sistem…"
---

# Stress testing

Suite stress. Hammer app part yang failure cost user uang — chiefly **copy trading** — dengan hostile, randomized, fault-injected workload. Assert sistem tetap correct. Hidup di `tests/StressTests`, jalankan di normal `dotnet test` green gate.

## Approach — Deterministic Simulation Testing (DST)

Best way stress distributed financial system = **deterministic simulation testing**, per TigerBeetle, FoundationDB, Antithesis: jalankan real logic terhadap *simulated* world, drive dengan **seeded** random workload + injected fault, assert invariant di quiescence. Semua seeded + deterministic → failure apa pun reproduce exact dari seed. Combined dengan:

- **Chaos-engineering fault injection** (Netflix Chaos Monkey style) — connection drop, order rejection, token rotation, node death.
- **Property-based invariant** — tidak assert exact call sequence; assert property yang harus hold tidak peduli bagaimana event interleave (convergence, tidak ada orphan, at-most-one lease holder).

App sudah ship perfect DST world model: `FakeTradingSession`, cTrader-faithful in-memory Open API session. Stress suite reuse (linked, single source of truth) tidak mock, jadi simulated broker behave seperti real one.

## Apa yang itu cover

### Copy trading (primary focus)

Driven via `CopyDstWorld` (`tests/StressTests/CopyTrading/`), jalankan live `CopyEngineHost` terhadap fake session, issue membership-consistent source workload:

| Scenario | Stresses |
|---|---|
| `Mass_fan_out…` | 1 source → 80 destination, 150 open kemudian close; full fan-out + drain |
| `High_frequency_open_close…` | 300 rapid interleaved open/close; tidak ada leaked position |
| `Partial_close_and_scale_in_storm…` | partial-close + scale-in churn; label-set stability |
| `Connection_flap_storm…` | repeated socket disconnect/reconnect + mid-flight desync; resync convergence |
| `Order_rejection_cascade…` | subset reject setiap order; healthy destination unaffected, kemudian self-heal via resync |
| `Token_rotation_storm…` | rapid in-place token swap during order storm |
| `Randomized_chaos_workload…` (10 seeds) | **DST core** — setiap event type + setiap fault interleaved unpredictably |
| `CopyLeaseReclaimStressTests` | node death + lease reclaim melintasi scaled cluster (pure domain, `FakeTimeProvider`) |

**Convergence invariant.** Di rest, setiap healthy destination mirror exactly set dari still-open source position — tidak ada orphan, tidak ada missing. Asserted pada label *set* (scale-in legitimately buka second destination position di bawah sama source label, jadi duplicate label expected). Destination saat ini reject order allowed lag, reconciled sekali healed.

**Lease invariant.** Di cluster di mana node mati + revive pada seeded schedule, at most satu node pernah hold valid lease di profile; dead node lease lapses exact di expiry, get reclaimed; healthy cluster settle dengan setiap profile held oleh exact satu node. Mirror `CopyEngineSupervisor` claim predicate terhadap `CopyProfile` domain lease method.

### Thread-safety dari harness
