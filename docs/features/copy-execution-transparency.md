# Copy execution transparency (Phase 3)

Per-copy execution facts — latency, realized slippage, fill vs failure — captured for every copy attempt
and surfaced as a per-profile transparency report. **Off by default**; enable with
`App:Copy:TransparencyEnabled=true`. When off, the copy engine is byte-for-byte unchanged: the host emits
to a no-op sink and nothing is written.

## How it works

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

- **Hot path stays free of I/O.** The host calls `ICopyEventSink.Record(...)` — a non-blocking,
  never-throwing enqueue. It never awaits, never touches the DB, and never blocks order execution.
- **Loss is preferred over back-pressure.** The channel is bounded (`CopyExecutionChannelCapacity`) with
  `DropOldest`: if the DB drainer stalls, the *oldest* transparency rows are dropped rather than delaying a
  copy. Transparency is best-effort telemetry, not a trading dependency.
- **Out-of-band persistence.** `CopyExecutionDrainer` drains the channel in batches
  (`CopyExecutionDrainBatchSize`) on `CopyExecutionDrainInterval`, writing `CopyExecution` rows through a
  scoped `DataContext`. A final flush runs on shutdown.
- **Facts, not commands.** `CopyExecution` is an append-only log (like `InstanceLog`/`AuditLog`), not an
  aggregate. The read model queries it directly (CQRS-lite) and aggregates in memory.

## What is recorded

One `CopyExecutionRecord` per copy attempt on one destination:

| Kind | When | Carries |
|------|------|---------|
| `Opened` | a copy order was placed | symbol, side, wire volume, master price, realized slippage (points), latency (ms) |
| `Failed` | the copy open threw/rejected | symbol, side, master volume/price, latency, failure reason (exception type) |

(`Closed`/`Skipped`/`Reconciled` exist in the enum for future expansion.)

## The report

`GET /api/copy/profiles/{id}/transparency` (owner-scoped) returns, over the most recent 500 facts:

- **Summary** — total, opened, failed, **fill rate**, **average latency (ms)**, **average slippage (points)**.
- **Recent** — the raw recent facts (destination, source position, symbol, side, volume, master price,
  slippage, latency, reason, timestamp).

## Configuration (`App:Copy`)

| Setting | Default | Effect |
|---------|---------|--------|
| `TransparencyEnabled` | `false` | Turn per-copy fact capture + the drainer on for the node. |

Channel capacity, drain batch size, and drain interval are `CopyDefaults` constants
(`CopyExecutionChannelCapacity` / `CopyExecutionDrainBatchSize` / `CopyExecutionDrainInterval`).

## Tests

- **Unit** (`CopyTransparencyTests`) — a successful open emits an `Opened` fact with the right
  symbol/side/volume/latency; a rejected open emits a `Failed` fact with the reason. Driven through a
  capturing sink.
- **Integration** (`CopyExecutionDrainerTests`, real Postgres) — the drainer persists buffered facts to the
  `CopyExecution` log; an empty sink writes nothing.
- **DST** — the host change is fire-and-forget with a no-op default sink, so the deterministic copy stress
  suite stays green (23/23).
