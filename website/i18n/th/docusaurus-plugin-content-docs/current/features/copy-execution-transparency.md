---
description: "Per-copy execution facts — latency, realized slippage, fill vs failure — captured every copy attempt, surfaced as per-profile transparency report. Off by…"
---

# Copy execution transparency (Phase 3)

Per-copy execution facts — latency realized slippage fill vs failure — captured ทุก copy attempt
surfaced เป็น per-profile transparency report **Off by default**; enable ด้วย
`App:Copy:TransparencyEnabled=true` เมื่อ off copy engine byte-for-byte ไม่เปลี่ยนแปลง: host emits
ไป no-op sink nothing written

## How มันworks

```
CopyEngineHost ──Record(fact)──▶ ICopyEventSink
                                   │
             (transparency off) NullCopyEventSink   → discards (default; zero hot-path cost)
             (transparency on)  ChannelCopyEventSink → bounded in-memory channel (DropOldest)
                                   │
                                   ▼
                          CopyExecutionDrainer (BackgroundService)
                                   │  batches ทุก App drain interval
                                   ▼
                          CopyExecution append-only table  ◀── GET /api/copy/profiles/{id}/transparency
```

- **Hot path stays free ของ I/O** Host calls `ICopyEventSink.Record(...)` — non-blocking
  never-throwing enqueue ไม่เคย awaits ไม่เคยtouches DB ไม่เคยblocks order execution
- **Loss preferred over back-pressure** Channel bounded (`CopyExecutionChannelCapacity`) ด้วย
  `DropOldest`: if DB drainer stalls *oldest* transparency rows dropped แทน delay
  copy transparency = best-effort telemetry ไม่ trading dependency
- **Out-of-band persistence** `CopyExecutionDrainer` drains channel ใน batches
  (`CopyExecutionDrainBatchSize`) on `CopyExecutionDrainInterval` writes `CopyExecution` rows ผ่าน
  scoped `DataContext` final flush on shutdown
- **Facts ไม่ใช่ commands** `CopyExecution` = append-only log (like `InstanceLog`/`AuditLog`) ไม่
  aggregate read model queries มัน directly (CQRS-lite) aggregates ใน memory

## What recorded

One `CopyExecutionRecord` per copy attempt on one destination:

| Kind | When | Carries |
|------|------|---------|
| `Opened` | copy order placed | symbol side wire volume master price realized slippage (points) latency (ms) |
| `Failed` | copy open threw/rejected | symbol side master volume/price latency failure reason (exception type) |

(`Closed`/`Skipped`/`Reconciled` exist ใน enum สำหรับ future expansion)

## The report

`GET /api/copy/profiles/{id}/transparency` (owner-scoped) returns over most recent 500 facts:

- **Summary** — total opened failed **fill rate** **average latency (ms)** **average slippage (points)**
- **Recent** — raw recent facts (destination source position symbol side volume master price
  slippage latency reason timestamp)

## Configuration (`App:Copy`)

| Setting | Default | Effect |
|---------|---------|--------|
| `TransparencyEnabled` | `false` | Turn per-copy fact capture + drainer on สำหรับ node |

Channel capacity drain batch size drain interval = `CopyDefaults` constants
(`CopyExecutionChannelCapacity` / `CopyExecutionDrainBatchSize` / `CopyExecutionDrainInterval`)

## Tests

- **Unit** (`CopyTransparencyTests`) — successful open emits `Opened` fact ด้วย right
  symbol/side/volume/latency; rejected open emits `Failed` fact ด้วย reason driven ผ่าน
  capturing sink
- **Integration** (`CopyExecutionDrainerTests` real Postgres) — drainer persists buffered facts ไป
  `CopyExecution` log; empty sink writes nothing
- **DST** — host change fire-and-forget ด้วย no-op default sink ดังนั้น deterministic copy stress
  suite stays green (23/23)
