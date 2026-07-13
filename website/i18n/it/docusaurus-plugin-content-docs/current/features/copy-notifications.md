---
description: "Feed per-owner di eventi copy safety-rilevanti — destinazione che fa scattare il rejection breaker, account-protection o prop-rule breach, panic flatten. Acceso per default."
---

# Copy operational notifications (Phase 2b)

Feed per-owner di eventi copy safety-rilevanti — destinazione che fa scattare rejection breaker,
account-protection o prop-rule breach, panic flatten. **Acceso per default** (`App:Copy:NotificationsEnabled`,
default `true`); impostare false per silenziare. Own concept nel Copy context, separato da market/AI
`AlertRule` aggregate.

## Come funziona

Stesso pattern out-of-band host→sink→drainer del log di execution-transparency:

```
CopyEngineHost ──Notify(record)──▶ ICopyNotificationSink
                                     │
             (notifications off) NullCopyNotificationSink   → scarta (no-op; engine unchanged)
             (notifications on)  ChannelCopyNotificationSink → bounded DropOldest channel
                                     │
                                     ▼
                            CopyNotificationDrainer (BackgroundService)
                                     │  risolve ogni profile owner, batcha
                                     ▼
                            CopyNotification feed  ◀── GET /api/copy/notifications
```

- Host `Notify(...)` non-blocking, never throws — mai tocca DB, mai ritarda copy.
- Drainer risolve owning `UserId` da ogni notification del profile; notification il cui profile è andato
  (owner unresolvable) droppata, non orphaned.
- `CopyNotification` = append-only, per-row-acknowledgeable feed (not aggregate).

## Cosa è sollevato

| Kind | Severity | Quando |
|------|----------|------|
| `DestinationTripped` | Warning | G8 rejection budget esaurito; nuove aperture in pausa per il cooldown. |
| `AccountProtectionTriggered` | Critical | ZuluGuard equity floor/ceiling breached; aperture latched (SellOut liquidates). |
| `PropRuleBreached` | Critical | Prop daily-loss / trailing-drawdown breached; destinazione flattened + locked out per il giorno. |
| `FlattenAll` | Critical | Panic flatten eseguito; ogni destinazione closed + locked. |
| `TokenInvalidated` | (reserved) | Il token di una destinazione è stato invalidato; in attesa di rotazione. |

## API

- `GET /api/copy/notifications` (owner-scoped) — notifiche recenti dell'utente (più recenti 200) attraverso
  tutti i profile, più count **unacknowledged**.
- `POST /api/copy/notifications/{id}/acknowledge` — marca una come letta.

## Configurazione (`App:Copy`)

| Impostazione | Default | Effetto |
|---------|---------|--------|
| `NotificationsEnabled` | `true` | Emetti notifiche safety + run the drainer. `false` → no-op sink. |

## Test

- **Unit** (`CopyNotificationTests`) — destinazione tripped solleva `DestinationTripped`; panic flatten
  solleva `FlattenAll` a level profile. Via capturing sink.
- **Integration** (`CopyNotificationDrainerTests`, Postgres reale) — drainer risolve owner + persiste;
  notification per profile sconosciuto droppata.
- **DST** — host emette fire-and-forget con no-op default sink, così la copy stress suite resta green
  (23/23).
