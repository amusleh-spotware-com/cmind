---
description: "Per-copy execution facts — latency, realized slippage, fill vs failure — captured every copy attempt, surfaced as per-profile transparency report. Off by…"
---

# Copy execution transparency (Phase 3)

Per-copy execution facts — latency, realized slippage, fill vs failure — captured every copy attempt,
surfaced as per-profile transparency report. **Off by default**; enable with
`App:Copy:TransparencyEnabled=true`. When off, copy engine byte-for-byte unchanged: host emits
to no-op sink, nothing written.

## Πώς λειτουργεί

```
CopyEngineHost ──Record(fact)──▶ ICopyEventSink
                                   │
             (transparency off) NullCopyEventSink   → discards (default; zero hot-path cost)
             (transparency on)  ChannelCopyEventSink → bounded in-memory channel (DropOldest)
                                   │
                                   ▼
                          CopyExecutionDrainer (BackgroundService)
                                   │  batches every App drain interval
                                   ▼
                          CopyExecution append-only table  ◀── GET /api/copy/profiles/{id}/transparency
```

- **Hot path stays free of I/O.** Host calls `ICopyEventSink.Record(...)` — non-blocking,
  never-throwing enqueue. Never awaits, never touches DB, never blocks order execution.
- **Loss preferred over back-pressure.** Channel bounded (`CopyExecutionChannelCapacity`) with
  `DropOldest`: αν το DB drainer stalls, τα *oldest* transparency rows αποτέμνονται αντί να καθυστερήσει copy. Transparency = best-effort telemetry, όχι trading dependency.
- **Out-of-band persistence.** `CopyExecutionDrainer` αποστραγγίζει channel σε batches
  (`CopyExecutionDrainBatchSize`) σε `CopyExecutionDrainInterval`, γράφει `CopyExecution` rows μέσω
  scoped `DataContext`. Final flush at shutdown.
- **Facts, not commands.** `CopyExecution` = append-only log (όπως `InstanceLog`/`AuditLog`), όχι
  aggregate. Read model queries το άμεσα (CQRS-lite), aggregates in memory.

## Τι καταγράφεται

Ένα `CopyExecutionRecord` ανά copy attempt σε ένα destination:

| Kind | When | Carries |
|------|------|---------|
| `Opened` | copy order placed | symbol, side, wire volume, master price, realized slippage (points), latency (ms) |
| `Failed` | copy open threw/rejected | symbol, side, master volume/price, latency, failure reason (exception type) |

(`Closed`/`Skipped`/`Reconciled` exist in enum για future expansion.)

## Η αναφορά

`GET /api/copy/profiles/{id}/transparency` (owner-scoped) επιστρέφει, πάνω από τα πιο πρόσφατα 500 facts:

- **Summary** — total, opened, failed, **fill rate**, **average latency (ms)**, **average slippage (points)**.
- **Recent** — raw recent facts (destination, source position, symbol, side, volume, master price,
  slippage, latency, reason, timestamp).

## Configuration (`App:Copy`)

| Setting | Default | Effect |
|---------|---------|--------|
| `TransparencyEnabled` | `false` | Ενεργοποιήστε per-copy fact capture + drainer για node. |

Channel capacity, drain batch size, drain interval = `CopyDefaults` constants
(`CopyExecutionChannelCapacity` / `CopyExecutionDrainBatchSize` / `CopyExecutionDrainInterval`).

## Tests

- **Unit** (`CopyTransparencyTests`) — successful open εκπέμπει `Opened` fact με σωστά
  symbol/side/volume/latency; rejected open εκπέμπει `Failed` fact με reason. Driven through
  capturing sink.
- **Integration** (`CopyExecutionDrainerTests`, real Postgres) — drainer persists buffered facts σε
  `CopyExecution` log; empty sink writes nothing.
- **DST** — host change fire-and-forget με no-op default sink, ώστε το deterministic copy stress
  suite παραμένει green (23/23).
