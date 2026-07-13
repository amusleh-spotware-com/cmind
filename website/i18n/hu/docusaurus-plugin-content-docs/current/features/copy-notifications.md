---
title: Másolási értesítések
description: "Per-tulajdonos feed a biztonság szempontjából releváns másolási eseményekről — célú túllépés rejection breaker, számlavédelem vagy prop-szabály breach, pánik flatten. Alapértelmezés szerint be."
---

# Másolási operatív értesítések (2b fázis)

Per-tulajdonos feed a biztonság szempontjából releváns másolási eseményekről — célú túllépés rejection breaker, számlavédelem vagy prop-szabály breach, pánik flatten. **Alapértelmezés szerint be** (`App:Copy:NotificationsEnabled`, alapértelmezés `true`); állítsd false-ra a csendesítéshez. Saját koncepció a Copy kontextusban, elkülönítve a market/AI `AlertRule` aggregátumtól.

## Hogyan működik

Ugyanaz az out-of-band host→sink→drainer minta, mint az execution-transparency log:

```
CopyEngineHost ──Notify(record)──▶ ICopyNotificationSink
                                     │
             (notifications off) NullCopyNotificationSink   → eldob (no-op; változatlan motor)
             (notifications on)  ChannelCopyNotificationSink → bounded DropOldest csatorna
                                     │
                                     ▼
                            CopyNotificationDrainer (BackgroundService)
                                     │  feloldja minden profil tulajdonosát, batch-eli
                                     ▼
                            CopyNotification feed  ◀── GET /api/copy/notifications
```

- A Host `Notify(...)` non-blocking, soha nem dob — soha nem nyúl az DB-hez, nem késlelteti a másolást.
- A drainer feloldja az owning `UserId`-t minden notification profiljából; az a notification, amelynek profilja eltűnt (tulajdonos feloldhatatlan) el van dobva, nem orphaned.
- `CopyNotification` = append-only, per-sor-acknowledgeable feed (nem aggregátum).

## Mi van kibocsátva

| Fajta | Súlyosság | Mikor |
|------|----------|------|
| `DestinationTripped` | Warning | G8 rejection budget kimerült; az új nyitások szünetelnek a cooldown-ra. |
| `AccountProtectionTriggered` | Critical | ZuluGuard equity floor/ceiling megsértve; a nyitások latched (SellOut liquidál). |
| `PropRuleBreached` | Critical | Prop napi veszteség / követő drawdown megsértve; a cél flatten-elve + kizárva a napra. |
| `FlattenAll` | Critical | Pánik flatten végrehajtva; minden cél bezárva + zárolva. |
| `TokenInvalidated` | (fenntartva) | Egy cél token-je érvénytelenítve lett; rotációra vár. |

## API

- `GET /api/copy/notifications` (tulajdonos-scoped) — a felhasználó legutóbbi értesítései (legutóbbi 200) minden profilból, plusz **nem nyknowledged** count.
- `POST /api/copy/notifications/{id}/acknowledge` — jelölj egyet olvasottként.

## Konfiguráció (`App:Copy`)

| Beállítás | Alapértelmezés | Hatás |
|---------|---------|--------|
| `NotificationsEnabled` | `true` | Kibocsátja a biztonsági értesítéseket + futtatja a drainer-t. `false` → no-op sink. |

## Tesztek

- **Egység** (`CopyNotificationTests`) — a tripped cél emeli a `DestinationTripped`-et; a pánik flatten emeli a profil-szintű `FlattenAll`-t. Capturing sink-en keresztül.
- **Integráció** (`CopyNotificationDrainerTests`, valódi Postgres) — a drainer feloldja a tulajdonost + perzisztál; az ismeretlen profil notification-je el van dobva.
- **DST** — a host fire-and-forget-et bocsát ki no-op default sink-kel, így a másolási stress suite zölden marad (23/23).
