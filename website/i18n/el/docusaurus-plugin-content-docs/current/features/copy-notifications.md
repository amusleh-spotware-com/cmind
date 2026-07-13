---
description: "Per-owner feed of safety-relevant copy events — destination tripping rejection breaker, account-protection or prop-rule breach, panic flatten. On by…"
---

# Copy operational notifications (Phase 2b)

Per-owner feed των safety-relevant copy events — destination tripping rejection breaker, account-protection ή prop-rule breach, panic flatten. **On by default** (`App:Copy:NotificationsEnabled`, default `true`); set false για να σιγήσει. Δικό concept στο Copy context, ξεχωριστό από market/AI `AlertRule` aggregate.

## Πώς λειτουργεί

Ίδιο out-of-band host→sink→drainer pattern όπως execution-transparency log:

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
- Drainer resolves owning `UserId` από κάθε notification's profile; notification του οποίου το profile gone (owner unresolvable) dropped, όχι orphaned.
- `CopyNotification` = append-only, per-row-acknowledgeable feed (όχι aggregate).

## Τι ανυψώνεται

| Kind | Severity | When |
|------|----------|------|
| `DestinationTripped` | Warning | G8 rejection budget εξαντλήθηκε; νέα opens paused για το cooldown. |
| `AccountProtectionTriggered` | Critical | ZuluGuard equity floor/ceiling breached; opens latched (SellOut liquidates). |
| `PropRuleBreached` | Critical | Prop daily-loss / trailing-drawdown breached; destination flattened + locked out για την ημέρα. |
| `FlattenAll` | Critical | Panic flatten executed; κάθε destination κλειστή + locked. |
| `TokenInvalidated` | (reserved) | Το destination's token δεν ισχύει πλέον; awaiting rotation. |

## API

- `GET /api/copy/notifications` (owner-scoped) — οι πρόσφατες notifications του user (πιο πρόσφατα 200) διαφορά όλα profiles, plus **unacknowledged** count.
- `POST /api/copy/notifications/{id}/acknowledge` — mark one read.

## Configuration (`App:Copy`)

| Setting | Default | Effect |
|---------|---------|--------|
| `NotificationsEnabled` | `true` | Emit safety notifications + τρέξε το drainer. `false` → no-op sink. |

## Tests

- **Unit** (`CopyNotificationTests`) — tripped destination raises `DestinationTripped`; panic flatten raises profile-level `FlattenAll`. Via capturing sink.
- **Integration** (`CopyNotificationDrainerTests`, real Postgres) — drainer resolves owner + persists; notification για unknown profile dropped.
- **DST** — host emits fire-and-forget με no-op default sink, ώστε το copy stress suite παραμένει green (23/23).
