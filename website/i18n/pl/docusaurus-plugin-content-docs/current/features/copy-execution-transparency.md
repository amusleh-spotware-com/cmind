---
description: "Per-copy execution facts — latency, realized slippage, fill vs failure — captured każdy copy attempt, surfaced jako per-profile transparency report. Off by…"
---

# Copy execution transparency (Phase 3)

Per-copy execution facts — latency, realized slippage, fill vs failure — captured każdy copy attempt,
surfaced jako per-profile transparency report. **Off domyślnie**; enable z
`App:Copy:TransparencyEnabled=true`. Gdy off, copy engine byte-for-byte unchanged: host emits
do no-op sink, nic written.

## Jak to działa

```
CopyEngineHost ──Record(fact)──▶ ICopyEventSink
                                   │
             (transparency off) NullCopyEventSink   → discards (domyślnie; zero hot-path cost)
             (transparency on)  ChannelCopyEventSink → bounded in-memory channel (DropOldest)
                                   │
                                   ▼
                          CopyExecutionDrainer (BackgroundService)
                                   │  batches każdy App drain interval
                                   ▼
                          CopyExecution append-only table  ◀── GET /api/copy/profiles/{id}/transparency
```

- **Hot path stays free of I/O.** Host calls `ICopyEventSink.Record(...)` — non-blocking,
  nigdy-throwing enqueue. Nigdy awaits, nigdy touches DB, nigdy blocks order execution.
- **Loss preferred nad back-pressure.** Channel bounded (`CopyExecutionChannelCapacity`) z
  `DropOldest`: jeśli DB drainer stalls, *oldest* transparency rows dropped rather delay
  copy. Transparency = best-effort telemetry, nie trading dependency.
- **Out-of-band persistence.** `CopyExecutionDrainer` drains channel w batches
  (`CopyExecutionDrainBatchSize`) na `CopyExecutionDrainInterval`, writes `CopyExecution` rows przez
  scoped `DataContext`. Final flush na shutdown.
- **Facts, nie commands.** `CopyExecution` = append-only log (jak `InstanceLog`/`AuditLog`), nie
  aggregate. Read model queries to directly (CQRS-lite), aggregates w memory.

## Co jest zaznaczone

Jeden `CopyExecutionRecord` per copy attempt na jeden destination:

| Kind | Gdy | Niesie |
|------|------|---------|
| `Opened` | copy order placed | symbol, side, wire volume, master price, realized slippage (points), latency (ms) |
| `Failed` | copy open threw/rejected | symbol, side, master volume/price, latency, failure reason (exception type) |

(`Closed`/`Skipped`/`Reconciled` istnieją w enum dla future expansion.)

## Raport

`GET /api/copy/profiles/{id}/transparency` (owner-scoped) zwraca, nad most recent 500 facts:

- **Summary** — total, opened, failed, **fill rate**, **average latency (ms)**, **average slippage (points)**.
- **Recent** — raw recent facts (destination, source position, symbol, side, volume, master price,
  slippage, latency, reason, timestamp).

## Konfiguracja (`App:Copy`)

| Ustawienie | Domyślnie | Effect |
|---------|---------|--------|
| `TransparencyEnabled` | `false` | Turn per-copy fact capture + drainer na node. |

Channel capacity, drain batch size, drain interval = `CopyDefaults` constants
(`CopyExecutionChannelCapacity` / `CopyExecutionDrainBatchSize` / `CopyExecutionDrainInterval`).

## Testy

- **Unit** (`CopyTransparencyTests`) — successful open emits `Opened` fact z right
  symbol/side/volume/latency; rejected open emits `Failed` fact z reason. Driven przez
  capturing sink.
- **Integracja** (`CopyExecutionDrainerTests`, real Postgres) — drainer persists buffered facts do
  `CopyExecution` log; empty sink writes nic.
- **DST** — host change fire-and-forget z no-op default sink, więc deterministyczne copy stress
  suite stays green (23/23).
