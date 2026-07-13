---
description: "Suite stress. Martella le parti dell'app il cui failure costa soldi agli utenti — principalmente copy trading — con carichi di lavoro ostili, randomizzati e fault-injected. Asserts che il sistema resti corretto."
---

# Stress testing

Suite stress. Martella le parti dell'app il cui failure costa soldi agli utenti — principalmente **copy trading** — con
carichi di lavoro ostili, randomizzati e fault-injected. Asserts che il sistema resti corretto. Vive in
`tests/StressTests`, gira nel normale `dotnet test` green gate.

## Approccio — Deterministic Simulation Testing (DST)

Il modo migliore per stressare sistemi finanziari distribuiti = **deterministic simulation testing**, per
TigerBeetle, FoundationDB, Antithesis: gira logica reale contro un *mondo simulato*, guida con
**random workload seeded** + fault iniettati, assert invarianti a quiescenza. Tutto seeded + deterministico →
qualsiasi fallimento riproduce esatto dal seed. Combinato con:

- **Chaos-engineering fault injection** (stile Netflix Chaos Monkey) — connection drops, order rejections, token rotation, node death.
- **Property-based invariants** — no assert sequenze di chiamata esatte; assert proprietà che devono valere
  non importa come gli eventi si intrecciano (convergence, no orphans, at-most-one lease holder).

L'app già spedisce un modello DST world perfetto: `FakeTradingSession`, sessione Open API in-memory
cTrader-faithful. La stress suite lo riutilizza (linked, singola source of truth) non mock, così il
broker simulato si comporta come quello reale.

## Cosa copre

### Copy trading (focus primario)

Guidato via `CopyDstWorld` (`tests/StressTests/CopyTrading/`), gira `CopyEngineHost` live contro fake session, emette workload sorgente consistente con membership:

| Scenario | Stressa |
|---|---|
| `Mass_fan_out…` | 1 sorgente → 80 destinazioni, 150 aperture poi chiusure; full fan-out + drain |
| `High_frequency_open_close…` | 300 rapid interleaved open/close; no leaked positions |
| `Partial_close_and_scale_in_storm…` | partial-close + scale-in churn; label-set stability |
| `Connection_flap_storm…` | repeated socket disconnect/reconnect + mid-flight desync; resync convergence |
| `Order_rejection_cascade…` | un subset rifiuta ogni ordine; destinazioni healthy unaffected, poi self-heal via resync |
| `Token_rotation_storm…` | rapid in-place token swaps durante un order storm |
| `Randomized_chaos_workload…` (10 seeds) | **il DST core** — ogni tipo di evento + ogni fault interleaved unpredictably |
| `CopyLeaseReclaimStressTests` | node death + lease reclaim attraverso un cluster scaled (pure domain, `FakeTimeProvider`) |

**Invariante di convergence.** A riposo, ogni destinazione healthy mima esattamente il set di source position ancora aperte — no orphans, nessuna mancante. Assert su label *set* (scale-in legittimamente apre una seconda destination position sotto la stessa source label, quindi duplicate labels expected). Destinazione che attualmente rifiuta ordini può laggare, riconciliata una volta healed.

**Invariante di lease.** In cluster dove i nodi muoiono + rivivono su schedule seeded, al massimo un nodo detiene mai un lease valido su un profilo; il lease del nodo morto scade esatto a expiry, viene reclamato; il cluster healthy si stabilizza con ogni profilo detenuto da esattamente uno. Specchia il claim predicate di `CopyEngineSupervisor` contro i metodi di lease del domain `CopyProfile`.

## Bugs trovati

- **Startup resync race in `CopyEngineHost`.** `OnReconnected` wired prima del reference-load iniziale + prima resync, che girava senza `_stateGate`. Socket flap durante startup girava una seconda resync concorrente, corrompeva i dict di stato non-concurrent dell'host (`_symbolDetails`, `_sourceVolumes`). Fix: gira startup load + prima resync under gate. Race di produzione, non test artifact — il workload chaos DST l'ha surfacata.

## Running

```bash
dotnet test tests/StressTests/StressTests.csproj
```

Suite **serializzata** (`[assembly: CollectionBehavior(DisableTestParallelization = true)]`): ogni test fa girare il background loop dell'host live, guida a quiescenza sotto wall clock, così run parallelo affama i task dell'host e rende i timeout di convergence flaky. Workload dimensionati per finire in secondi così la suite resta nel default green gate. Fallimento stampa il suo seed; re-run quel seed per riprodurre esatto interleaving.

## Estendere

- Nuovo comportamento copy → aggiungi source op a `CopyDstWorld` (mantieni source book membership consistente con event stream) + weighted case in `CopyChaosDstTests`. Se può creare o ritirare una destination position, assicurati che l'invariante di convergence tenga ancora.
- Nuovo fault → aggiungi injector a `CopyDstWorld` (delegate a `FakeTradingSession`'s control surface via `SyncTradingSession`) + esercita in uno scenario nominato plus chaos mix.
- Mantieni il simulatore cTrader-faithful (vedere mandate root `CLAUDE.md`); mai indebolirlo per far passare uno stress test.
