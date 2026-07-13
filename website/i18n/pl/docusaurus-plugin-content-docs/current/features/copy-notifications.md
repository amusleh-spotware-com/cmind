---
description: "Per-owner feed safety-relevant copy events — destination tripping rejection breaker, account-protection lub prop-rule breach, panic flatten. On by…"
---

# Copy operational notifications (Phase 2b)

Per-owner feed safety-relevant copy events — destination tripping rejection breaker, account-protection
lub prop-rule breach, panic flatten. **On domyślnie** (`App:Copy:NotificationsEnabled`, domyślnie `true`); set false aby silence. Own concept w Copy context, separate od market/AI `AlertRule` aggregate.

## Jak to działa

Ten sam out-of-band host→sink→drainer pattern co execution-transparency log:

```
CopyEngineHost ──Notify(record)──▶ ICopyNotificationSink
                                     │
             (notifications off) NullCopyNotificationSink   → discards (no-op; unchanged engine)
             (notifications on)  ChannelCopyNotificationSink → bounded DropOldest channel
                                     │
                                     ▼
                            CopyNotificationDrainer (BackgroundService)
                                     │  resolves każdy profile's owner, batches
                                     ▼
                            CopyNotification feed  ◀── GET /api/copy/notifications
```

- Host `Notify(...)` non-blocking, nigdy throws — nigdy touches DB, nigdy delays copy.
- Drainer resolves owning `UserId` z każdy notification's profile; notification którego profile gone (owner unresolvable) dropped, nie orphaned.
- `CopyNotification` = append-only, per-row-acknowledgeable feed (nie aggregate).

## Co jest raised

| Kind | Severity | Gdy |
|------|----------|------|
| `DestinationTripped` | Warning | G8 rejection budget exhausted; new opens paused dla cooldown. |
| `AccountProtectionTriggered` | Critical | ZuluGuard equity floor/ceiling breached; opens latched (SellOut liquidates). |
| `PropRuleBreached` | Critical | Prop daily-loss / trailing-drawdown breached; destination flattened + locked out na day. |
| `FlattenAll` | Critical | Panic flatten executed; każdy destination closed + locked. |
| `TokenInvalidated` | (reserved) | Destination's token był invalidated; awaiting rotation. |

## API

- `GET /api/copy/notifications` (owner-scoped) — user's recent notifications (most recent 200) across wszystkie profiles, plus **unacknowledged** count.
- `POST /api/copy/notifications/{id}/acknowledge` — mark one read.

## Konfiguracja (`App:Copy`)

| Ustawienie | Domyślnie | Effect |
|---------|---------|--------|
| `NotificationsEnabled` | `true` | Emit safety notifications + run drainer. `false` → no-op sink. |

## Testy

- **Unit** (`CopyNotificationTests`) — tripped destination raises `DestinationTripped`; panic flatten raises profile-level `FlattenAll`. Via capturing sink.
- **Integracja** (`CopyNotificationDrainerTests`, real Postgres) — drainer resolves owner + persists; notification dla unknown profile dropped.
- **DST** — host emits fire-and-forget z no-op default sink, więc copy stress suite stays green (23/23).
