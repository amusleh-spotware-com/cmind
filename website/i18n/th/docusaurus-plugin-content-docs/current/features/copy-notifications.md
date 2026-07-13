---
description: "Per-owner feed of safety-relevant copy events — destination tripping rejection breaker, account-protection or prop-rule breach, panic flatten. On by…"
---

# Copy operational notifications (Phase 2b)

Per-owner feed ของ safety-relevant copy events — destination tripping rejection breaker account-protection หรือ prop-rule breach panic flatten **On by default** (`App:Copy:NotificationsEnabled` default `true`); set false ไป silence own concept ใน Copy context separate จาก market/AI `AlertRule` aggregate

## How มันworks

Same out-of-band host→sink→drainer pattern เช่น execution-transparency log:

```
CopyEngineHost ──Notify(record)──▶ ICopyNotificationSink
                                     │
             (notifications off) NullCopyNotificationSink   → discards (no-op; unchanged engine)
             (notifications on)  ChannelCopyNotificationSink → bounded DropOldest channel
                                     │
                                     ▼
                            CopyNotificationDrainer (BackgroundService)
                                     │  resolves ทุก profile ของ owner batches
                                     ▼
                            CopyNotification feed  ◀── GET /api/copy/notifications
```

- Host `Notify(...)` non-blocking never throws — never touches DB never delays copy
- Drainer resolves owning `UserId` จาก ทุก notification ของ profile; notification ที่ profile gone (owner unresolvable) dropped ไม่ orphaned
- `CopyNotification` = append-only per-row-acknowledgeable feed (ไม่ใช่ aggregate)

## What raised

| Kind | Severity | When |
|------|----------|------|
| `DestinationTripped` | Warning | G8 rejection budget exhausted; new opens paused สำหรับ cooldown |
| `AccountProtectionTriggered` | Critical | ZuluGuard equity floor/ceiling breached; opens latched (SellOut liquidates) |
| `PropRuleBreached` | Critical | Prop daily-loss / trailing-drawdown breached; destination flattened + locked out สำหรับ day |
| `FlattenAll` | Critical | Panic flatten executed; ทุก destination closed + locked |
| `TokenInvalidated` | (reserved) | destination ของ token invalidated; awaiting rotation |

## API

- `GET /api/copy/notifications` (owner-scoped) — user ของ recent notifications (most recent 200) ข้าม ทุก profiles บวก **unacknowledged** count
- `POST /api/copy/notifications/{id}/acknowledge` — mark one read

## Configuration (`App:Copy`)

| Setting | Default | Effect |
|---------|---------|--------|
| `NotificationsEnabled` | `true` | Emit safety notifications + run drainer `false` → no-op sink |

## Tests

- **Unit** (`CopyNotificationTests`) — tripped destination raises `DestinationTripped`; panic flatten raises profile-level `FlattenAll` via capturing sink
- **Integration** (`CopyNotificationDrainerTests` real Postgres) — drainer resolves owner + persists; notification สำหรับ unknown profile dropped
- **DST** — host emits fire-and-forget ด้วย no-op default sink ดังนั้น copy stress suite stays green (23/23)
