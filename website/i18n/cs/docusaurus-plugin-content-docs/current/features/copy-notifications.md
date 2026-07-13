---
description: "Per-owner feed bezpečnostně-relevantních copy událostí — destinace tripping rejection breaker, account-protection nebo prop-rule breach, panic flatten. Defaultně zapnuto…"
---

# Copy provozní notifikace (Fáze 2b)

Per-owner feed bezpečnostně-relevantních copy událostí — destinace tripping rejection breaker, account-protection nebo prop-rule breach, panic flatten. **Defaultně zapnuto** (`App:Copy:NotificationsEnabled`, default `true`); nastavte false pro umlčení. Vlastní koncept v Copy kontextu, oddělený od market/AI `AlertRule` aggregate.

## Jak to funguje

Stejný out-of-band host→sink→drainer pattern jako execution-transparency log:

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

- Host `Notify(...)` non-blocking, nikdy nehazuje — nikdy se nedotýká DB, nikdy nezpomaluje copy.
- Drainer resolveruje owning `UserId` z každé notifikace; notifikace jejíž profil zmizel (owner unresolvable) dropped, not orphaned.
- `CopyNotification` = append-only, per-row-acknowledgeable feed (not aggregate).

## Co se vyvolává

| Kind | Severity | Kdy |
|------|----------|------|
| `DestinationTripped` | Warning | G8 rejection budget vyčerpán; nová otevření pozastavena na cooldown. |
| `AccountProtectionTriggered` | Critical | ZuluGuard equity floor/ceiling breached; otevření latch (SellOut liquiduje). |
| `PropRuleBreached` | Critical | Prop daily-loss / trailing-drawdown breached; destinace flattenována + locked out na zbytek dne. |
| `FlattenAll` | Critical | Panic flatten provedena; každá destinace uzavřena + locked. |
| `TokenInvalidated` | (rezervováno) | Token destinace byl invalidován; čeká na rotaci. |

## API

- `GET /api/copy/notifications` (owner-scoped) — user's recent notifications (nejnovějších 200) napříč všemi profily, plus **unacknowledged** count.
- `POST /api/copy/notifications/{id}/acknowledge` — označ jednu jako přečtenou.

## Konfigurace (`App:Copy`)

| Nastavení | Default | Efekt |
|---------|---------|--------|
| `NotificationsEnabled` | `true` | Emit safety notifications + běž drainer. `false` → no-op sink. |

## Testy

- **Unit** (`CopyNotificationTests`) — tripped destination raises `DestinationTripped`; panic flatten raises profile-level `FlattenAll`. Via capturing sink.
- **Integration** (`CopyNotificationDrainerTests`, real Postgres) — drainer resolves owner + persists; notification for unknown profile dropped.
- **DST** — host emits fire-and-forget s no-op default sink, takže copy stress suite zůstává green (23/23).
