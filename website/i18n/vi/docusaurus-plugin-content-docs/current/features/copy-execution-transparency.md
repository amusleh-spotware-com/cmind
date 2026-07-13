---
description: "Per-copy execution facts — latency, realized slippage, fill vs failure — captured every copy attempt, surfaced as per-profile transparency report. Off by…"
---

# Copy execution transparency (Phase 3)

Per-copy execution facts — latency, realized slippage, fill vs failure — captured every copy attempt,
surfaced as per-profile transparency report. **Off by default**; enable với
`App:Copy:TransparencyEnabled=true`. Khi off, copy engine byte-for-byte unchanged: host emits
to no-op sink, không có gì written.

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

- **Hot path stays free of I/O.** Host calls `ICopyEventSink.Record(...)` — non-blocking,
  never-throwing enqueue. Không bao giờ awaits, không bao giờ touches DB, không bao giờ blocks order execution.
- **Loss preferred over back-pressure.** Channel bounded (`CopyExecutionChannelCapacity`) với
  `DropOldest`: nếu DB drainer stalls, *oldest* transparency rows dropped hơn là delay a
  copy. Transparency = best-effort telemetry, không phải trading dependency.
- **Out-of-band persistence.** `CopyExecutionDrainer` drains channel in batches
  (`CopyExecutionDrainBatchSize`) on `CopyExecutionDrainInterval`, writes `CopyExecution` rows through
  scoped `DataContext`. Final flush on shutdown.
- **Facts, not commands.** `CopyExecution` = append-only log (như `InstanceLog`/`AuditLog`), không phải
  aggregate. Read model queries nó directly (CQRS-lite), aggregates in memory.

## What is recorded

Một `CopyExecutionRecord` per copy attempt on one destination:

| Kind | When | Carries |
|------|------|---------|
| `Opened` | copy order placed | symbol, side, wire volume, master price, realized slippage (points), latency (ms) |
| `Failed` | copy open threw/rejected | symbol, side, master volume/price, latency, failure reason (exception type) |

(`Closed`/`Skipped`/`Reconciled` exist in enum for future expansion.)

## The report

`GET /api/copy/profiles/{id}/transparency` (owner-scoped) returns, over most recent 500 facts:

- **Summary** — total, opened, failed, **fill rate**, **average latency (ms)**, **average slippage (points)**.
- **Recent** — raw recent facts (destination, source position, symbol, side, volume, master price,
  slippage, latency, reason, timestamp).

## Configuration (`App:Copy`)

| Setting | Default | Effect |
|---------|---------|--------|
| `TransparencyEnabled` | `false` | Turn per-copy fact capture + drainer on for node. |

Channel capacity, drain batch size, drain interval = `CopyDefaults` constants
(`CopyExecutionChannelCapacity` / `CopyExecutionDrainBatchSize` / `CopyExecutionDrainInterval`).

## Tests

- **Unit** (`CopyTransparencyTests`) — successful open emits `Opened` fact với right
  symbol/side/volume/latency; rejected open emits `Failed` fact với reason. Driven through
  capturing sink.
- **Integration** (`CopyExecutionDrainerTests`, real Postgres) — drainer persists buffered facts to
  `CopyExecution` log; empty sink writes nothing.
- **DST** — host change fire-and-forget với no-op default sink, vì vậy deterministic copy stress
  suite stays green (23/23).
