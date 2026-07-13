---
description: "Fatti di esecuzione per-copy — latenza, slippage realizzato, fill vs failure — catturati ogni copy attempt, surfaced come report trasparenza per-profile. Spento per default."
---

# Copy execution transparency (Phase 3)

Fatti di esecuzione per-copy — latenza, slippage realizzato, fill vs failure — catturati ogni copy attempt,
surfaced come report trasparenza per-profile. **Spento per default**; abilita con
`App:Copy:TransparencyEnabled=true`. Quando spento, copy engine byte-for-byte unchanged: host emette
a no-op sink, niente scritto.

## Come funziona

```
CopyEngineHost ──Record(fact)──▶ ICopyEventSink
                                   │
             (trasparency off) NullCopyEventSink   → scarta (default; zero hot-path cost)
             (trasparency on)  ChannelCopyEventSink → bounded in-memory channel (DropOldest)
                                   │
                                   ▼
                          CopyExecutionDrainer (BackgroundService)
                                   │  batcha ogni App drain interval
                                   ▼
                          CopyExecution append-only table  ◀── GET /api/copy/profiles/{id}/transparency
```

- **Hot path resta libero da I/O.** Host chiama `ICopyEventSink.Record(...)` — non-blocking,
  never-throwing enqueue. Mai await, mai tocca DB, mai blocca order execution.
- **Loss preferita su back-pressure.** Channel bounded (`CopyExecutionChannelCapacity`) con
  `DropOldest`: se il DB drainer stalla, *le più vecchie* righe transparency droppate piuttosto che
  ritardare un copy. Transparency = telemetry best-effort, non dipendenza trading.
- **Persistenza out-of-band.** `CopyExecutionDrainer` draina il channel in batch
  (`CopyExecutionDrainBatchSize`) su `CopyExecutionDrainInterval`, scrive righe `CopyExecution` attraverso
  scoped `DataContext`. Flush finale su shutdown.
- **Facts, non commands.** `CopyExecution` = log append-only (come `InstanceLog`/`AuditLog`), non
  aggregate. Il read model lo querying direttamente (CQRS-lite), aggregates in memory.

## Cosa è registrato

Un `CopyExecutionRecord` per copy attempt su una destinazione:

| Kind | Quando | Porta |
|------|------|--------|
| `Opened` | copy order placed | symbol, side, wire volume, master price, realized slippage (points), latency (ms) |
| `Failed` | copy open threw/rejected | symbol, side, master volume/price, latency, failure reason (exception type) |

(`Closed`/`Skipped`/`Reconciled` existono nell'enum per espansione futura.)

## Il report

`GET /api/copy/profiles/{id}/transparency` (owner-scoped) restituisce, sugli ultimi 500 fatti:

- **Summary** — total, opened, failed, **fill rate**, **average latency (ms)**, **average slippage (points)**.
- **Recent** — fatti recenti raw (destination, source position, symbol, side, volume, master price,
  slippage, latency, reason, timestamp).

## Configurazione (`App:Copy`)

| Impostazione | Default | Effetto |
|---------|---------|--------|
| `TransparencyEnabled` | `false` | Attiva fact capture per-copy + drainer per node. |

Channel capacity, drain batch size, drain interval = costanti `CopyDefaults`
(`CopyExecutionChannelCapacity` / `CopyExecutionDrainBatchSize` / `CopyExecutionDrainInterval`).

## Test

- **Unit** (`CopyTransparencyTests`) — successful open emette fact `Opened` con right
  symbol/side/volume/latency; rejected open emette fact `Failed` con reason. Driven through
  capturing sink.
- **Integration** (`CopyExecutionDrainerTests`, Postgres reale) — drainer persiste fatti bufferizzati a
  log `CopyExecution`; empty sink scrive niente.
- **DST** — host change fire-and-forget con no-op default sink, così la suite stress copy deterministica
  resta green (23/23).
