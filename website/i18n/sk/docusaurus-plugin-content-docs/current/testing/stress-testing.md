---
description: "Stress suite. Hrubí časti app, ktorých zlyhanie stojí používateľov peniaze — hlavne copy trading — s nehostinnými, randomizovanými, fault-injected workloadmi. Assertuje systém…"
---

# Stress testing

Stress suite. Hrubí časti app, ktorých zlyhanie stojí používateľov peniaze — hlavne **copy trading** — s nehostinnými, randomizovanými, fault-injected workloadmi. Assertuje systém zostáva korektný. Žije v `tests/StressTests`, beží v normálnom `dotnet test` green gate.

## Prístup — Deterministic Simulation Testing (DST)

Najlepší spôsob stress testovania distribuovaných finančných systémov = **deterministic simulation testing**, per TigerBeetle, FoundationDB, Antithesis: beží reálnu logiku proti *simulovanému* svetu, hnané so **seeded** random workload + injected faults, assertuje invariants at quiescence. Všetko seeded + deterministic → akékoľvek zlyhanie reprodukuje presne zo seed. Kombinované s:

- **Chaos-engineering fault injection** (Netflix Chaos Monkey štýl) — connection drops, order rejections, token rotation, node death.
- **Property-based invariants** — neassertuje exact call sequences; assertuje vlastnosti, ktoré musia platiť bez ohľadu na to, ako sa udalosti prelínajú (convergence, no orphans, at-most-one lease holder).

App už lodive perfektný DST svet model: `FakeTradingSession`, cTrader-faithful in-memory Open API session. Stress suite ho znova používa (linked, single source of truth) nie mock, takže simulated broker sa správa ako reálny.

## Čo pokrýva

### Copy trading (primárny focus)

Hnané cez `CopyDstWorld` (`tests/StressTests/CopyTrading/`), beží live `CopyEngineHost` proti fake session, vydáva membership-konzistentný source workload:

| Scenár | Stresuje |
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

`FakeTradingSession` single-threaded; stress workload ho mutuje z test threadu kým host číta/píše z jeho loop. `SyncTradingSession` ho wrapuje, robí každú session operáciu atomickú na jednej bráne (bez držania brány naprieč reconnect callback — by invertovalo lock order vs host's `_stateGate` a deadlock-nulo). Simulator samotný nedotknutý.

## Bugs nájdené

- **Startup resync race v `CopyEngineHost`.** `OnReconnected` zapojené pred iniciálnym reference-load + first resync, čo bežalo bez `_stateGate`. Socket flap počas startup run-nul druhé resync súbežne + korumpovalo host's non-concurrent state dicts (`_symbolDetails`, `_sourceVolumes`). Opravené: bež startup load + first resync pod bránou. Produkčná race, nie test artifact — DST chaos workload to surface-nul.

## Bežanie

```bash
dotnet test tests/StressTests/StressTests.csproj
```

Suite **serializovaná** (`[assembly: CollectionBehavior(DisableTestParallelization = true)]`): každý test spíní live host background loop, hná k quiescencii pod wall clock, takže parallel run starves host tasks a makes convergence timeouts flaky. Workloads dimensionované na dokončenie v sekundách takže suite zostáva v default green gate. Failure vytlačí its seed; re-run that seed pre reprodukciu presného interleaving.

## Rozširovanie

- Nové copy správanie → pridaj source op do `CopyDstWorld` (udržiavaj source book membership konzistentné s event stream) + weighted case in `CopyChaosDstTests`. Ak môže vytvoriť alebo retirovú destination position, uistite sa, že convergence invariant stále drží.
- Nový fault → pridaj injector do `CopyDstWorld` (deleguj na `FakeTradingSession`'s control surface cez `SyncTradingSession`) + exercise in a named scenario plus chaos mix.
- Udržiavaj simulátor cTrader-faithful (pozrite root `CLAUDE.md` mandát); nikdy ho neoslabuj pre passing stress testu.
