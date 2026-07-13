---
description: "Per-owner feed of safety-relevant copy events — destination tripping rejection breaker, account-protection or prop-rule breach, panic flatten. On by…"
---

# Copy operational notifications (Phase 2b)

Per-owner feed of safety-relevant copy events — destination tripping rejection breaker, account-protection or prop-rule breach, panic flatten. **On by default** (`App:Copy:NotificationsEnabled`, default `true`); set false to silence. Own concept in Copy context, separate from market/AI `AlertRule` aggregate.

## How it works

Same out-of-band host→sink→drainer pattern as execution-transparency log:

```
CopyEngineHost ──Notify(record)──▶ ICopyNotificationSink
                                     │
             (notifications off) NullCopyNotificationSink   → discards (no-op; unchanged engine)
             (notifications on)  ChannelCopyNotificationSink → bounded DropOldest channel
                                     │
                                     ▼
                            CopyNotificationDrainer (BackgroundService)
                                     │  resolves each profile's owner, batches
                                     ▼
                            CopyNotification feed  ◀── GET /api/copy/notifications
```

- Host `Notify(...)` non-blocking, never throws — never touches DB, never delays copy.
- Drainer resolves owning `UserId` from each notification's profile; notification whose profile gone (owner unresolvable) dropped, not orphaned.
- `CopyNotification` = append-only, per-row-acknowledgeable feed (not aggregate).

## What is raised

| Kind | Severity | When |
|------|----------|------|
| `DestinationTripped` | Warning | G8 rejection budget exhausted; new opens paused for the cooldown. |
| `AccountProtectionTriggered` | Critical | ZuluGuard equity floor/ceiling breached; opens latched (SellOut liquidates). |
| `PropRuleBreached` | Critical | Prop daily-loss / trailing-drawdown breached; destination flattened + locked out for the day. |
| `FlattenAll` | Critical | Panic flatten executed; every destination closed + locked. |
| `TokenInvalidated` | (reserved) | A destination's token was invalidated; awaiting rotation. |

## API

- `GET /api/copy/notifications` (owner-scoped) — user's recent notifications (most recent 200) across all profiles, plus **unacknowledged** count.
- `POST /api/copy/notifications/{id}/acknowledge` — mark one read.

## Configuration (`App:Copy`)

| Setting | Default | Effect |
|---------|---------|--------|
| `NotificationsEnabled` | `true` | Emit safety notifications + run the drainer. `false` → no-op sink. |

## Tests

- **Unit** (`CopyNotificationTests`) — tripped destination raises `DestinationTripped`; panic flatten raises profile-level `FlattenAll`. Via capturing sink.
- **Integration** (`CopyNotificationDrainerTests`, real Postgres) — drainer resolves owner + persists; notification for unknown profile dropped.
- **DST** — host emits fire-and-forget with no-op default sink, so copy stress suite stays green (23/23).
<!-- [ZH-HANS] Translation needed -->
