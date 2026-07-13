---
description: "Per-owner feed bezpečnostne-relevántnych copy udalostí — destinácia tripping rejection breaker, account-protection alebo prop-rule porušenie, panic flatten. Zapnuté predvolene…"
---

# Copy operational notifications (Fáza 2b)

Per-owner feed bezpečnostne-relevántnych copy udalostí — destinácia tripping rejection breaker,
account-protection alebo prop-rule porušenie, panic flatten. **Zapnuté predvolene** (`App:Copy:NotificationsEnabled`,
predvolené `true`); nastavte false pre umlčanie. Vlastný koncept v Copy kontexte, oddelený od
market/AI `AlertRule` aggregate.

## Ako to funguje

Rovnaký out-of-band host→sink→drainer vzor ako execution-transparency log:

```
CopyEngineHost ──Notify(record)──▶ ICopyNotificationSink
                                     │
             (notifications off) NullCopyNotificationSink   → zahadzuje (no-op; nezmenený engine)
             (notifications on)  ChannelCopyNotificationSink → bounded DropOldest channel
                                     │
                                     ▼
                            CopyNotificationDrainer (BackgroundService)
                                     │  resolvuje owner UserId z každej notifikácie, batchuje
                                     ▼
                            CopyNotification feed  ◀── GET /api/copy/notifications
```

- Host `Notify(...)` non-blocking, nikdy nehodí — nikdy sa nedotýka DB, nikdy neodkladá copy.
- Drainer resolvuje owning `UserId` z každej notifikácie; notifikácia, ktorej profil zmizol (owner
  nerozpakovateľný) dropnutá, nie orphaned.
- `CopyNotification` = append-only, per-row-acknowledgeable feed (nie aggregate).

## Čo sa zvyšuje

| Kind | Závažnosť | Kedy |
|------|----------|------|
| `DestinationTripped` | Warning | G8 rejection budget vyčerpaný; nové opens pozastavené na cooldown. |
| `AccountProtectionTriggered` | Critical | ZuluGuard equity floor/ceiling porušený; opens latched (SellOut likviduje). |
| `PropRuleBreached` | Critical | Prop daily-loss / trailing-drawdown porušený; destinácia flattened + locked out na zvyšok dňa. |
| `FlattenAll` | Critical | Panic flatten vykonaná; každá destinácia uzavretá + zamknutá. |
| `TokenInvalidated` | (rezervované) | Token destinácie bol invalidovaný; čaká sa na rotáciu. |

## API

- `GET /api/copy/notifications` (owner-scoped) — nedávne notifikácie používateľa (najnovších 200) naprieč všetkými profilmi, plus **unacknowledged** count.
- `POST /api/copy/notifications/{id}/acknowledge` — označ jednu ako prečítanú.

## Konfigurácia (`App:Copy`)

| Nastavenie | Predvolené | Efekt |
|---------|---------|--------|
| `NotificationsEnabled` | `true` | Emit safety notifikácie + bež drainer. `false` → no-op sink. |

## Testy

- **Jednotka** (`CopyNotificationTests`) — tripped destinácia vyvolá `DestinationTripped`; panic flatten vyvolá
  profile-level `FlattenAll`. Cez capturing sink.
- **Integrácia** (`CopyNotificationDrainerTests`, reálny Postgres) — drainer resolvuje owner + perzistuje; notifikácia pre
  neznámy profil dropnutá.
- **DST** — host emituje fire-and-forget s no-op default sink, takže copy stress suite zostáva zelená (23/23).
