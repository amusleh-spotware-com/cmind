# Copy operational notifications (Phase 2b)

A per-owner feed of safety-relevant copy events — a destination tripping the rejection breaker, an
account-protection or prop-rule breach, a panic flatten. **On by default** (`App:Copy:NotificationsEnabled`,
default `true`); set false to silence the feed. These are their own concept in the Copy context, kept
separate from the market/AI `AlertRule` aggregate.

## How it works

Same out-of-band host→sink→drainer pattern as the execution-transparency log:

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

- The host `Notify(...)` call is non-blocking and never throws — it never touches the DB or delays a copy.
- The drainer resolves the owning `UserId` from each notification's profile; a notification whose profile no
  longer exists (owner unresolvable) is dropped, not orphaned.
- `CopyNotification` is an append-only, per-row-acknowledgeable feed (not an aggregate).

## What is raised

| Kind | Severity | When |
|------|----------|------|
| `DestinationTripped` | Warning | G8 rejection budget exhausted; new opens paused for the cooldown. |
| `AccountProtectionTriggered` | Critical | ZuluGuard equity floor/ceiling breached; opens latched (SellOut liquidates). |
| `PropRuleBreached` | Critical | Prop daily-loss / trailing-drawdown breached; destination flattened + locked out for the day. |
| `FlattenAll` | Critical | Panic flatten executed; every destination closed + locked. |
| `TokenInvalidated` | (reserved) | A destination's token was invalidated; awaiting rotation. |

## API

- `GET /api/copy/notifications` (owner-scoped) — the user's recent notifications (most recent 200) across
  all their profiles, plus an **unacknowledged** count.
- `POST /api/copy/notifications/{id}/acknowledge` — mark one read.

## Configuration (`App:Copy`)

| Setting | Default | Effect |
|---------|---------|--------|
| `NotificationsEnabled` | `true` | Emit safety notifications + run the drainer. `false` → no-op sink. |

## Tests

- **Unit** (`CopyNotificationTests`) — a tripped destination raises `DestinationTripped`; a panic flatten
  raises a profile-level `FlattenAll`. Via a capturing sink.
- **Integration** (`CopyNotificationDrainerTests`, real Postgres) — the drainer resolves the owner and
  persists; a notification for an unknown profile is dropped.
- **DST** — the host emits are fire-and-forget with a no-op default sink, so the copy stress suite stays
  green (23/23).
