---
description: "Pro-Owner-Feed von sicherheitsrelevanten Copy-Ereignissen — Ziel-Triplung Ablehnungs-Breaker, Kontoschutz- oder Prop-Regelverstoß, Panik-Flatten. An von…"
---

# Copy-Betriebsmitteilungen (Phase 2b)

Pro-Owner-Feed von sicherheitsrelevanten Copy-Ereignissen — Ziel-Triplung Ablehnungs-Breaker, Kontoschutz- oder Prop-Regelverstoß, Panik-Flatten. **Standard an** (`App:Copy:NotificationsEnabled`, Standard `true`); auf Falsch setzen zum Stummschalten. Eiges Konzept im Copy-Kontext, separat von Markt/KI `AlertRule` Aggregate.

## Wie es funktioniert

Dasselbe Out-of-Band Host→Sink→Drainer Muster wie Ausführungs-Transparenz-Log:

```
CopyEngineHost ──Notify(record)──▶ ICopyNotificationSink
                                     │
             (notifications off) NullCopyNotificationSink   → verworfen (no-op; unverändert Engine)
             (notifications on)  ChannelCopyNotificationSink → gebundener DropOldest Kanal
                                     │
                                     ▼
                            CopyNotificationDrainer (BackgroundService)
                                     │  löst jeden Profil-Besitzer auf, batches
                                     ▼
                            CopyNotification Feed  ◀── GET /api/copy/notifications
```

- Host `Notify(...)` nicht-blockierend, wirft nie — berührt nie DB, verzögert nie Copy.
- Drainer löst besitzende `UserId` aus jeder Benachrichtigung-Profil auf; Benachrichtigung deren Profil weg (Besitzer nicht lösbar) gelöscht, nicht verwaist.
- `CopyNotification` = append-only, Pro-Reihe-Bestätigbar Feed (kein Aggregate).

## Was wird erhöht

| Art | Schweregrad | Wann |
|------|----------|------|
| `DestinationTripped` | Warnung | G8-Ablehungs-Budget erschöpft; neue Opens paused für den Cooldown. |
| `AccountProtectionTriggered` | Kritisch | ZuluGuard Eigenkapital-Boden/Decke verletzt; Opens verriegelt (SellOut liquidiert). |
| `PropRuleBreached` | Kritisch | Prop täglich-Verlust / Trailing-Drawdown verletzt; Ziel geflacht + eine Tag gesperrt. |
| `FlattenAll` | Kritisch | Panic Flatten ausgeführt; jedes Ziel geschlossen + verriegelt. |
| `TokenInvalidated` | (reserviert) | Token eines Ziels wurde ungültig; wartet auf Rotation. |

## API

- `GET /api/copy/notifications` (Owner-Umfang) — Benachrichtigungen des Benutzers (die letzten 200) über alle Profile, plus **unbestätigte** Anzahl.
- `POST /api/copy/notifications/{id}/acknowledge` — eine als gelesen markieren.

## Konfiguration (`App:Copy`)

| Einstellung | Standard | Effekt |
|---------|---------|--------|
| `NotificationsEnabled` | `true` | Emit Sicherheitsmitteilungen + führe Drainer aus. `false` → no-op Sink. |

## Tests

- **Unit** (`CopyNotificationTests`) — Ziel-Triplung erhöht `DestinationTripped`; Panik Flatten erhöht Profil-Ebenen `FlattenAll`. Via erfassender Sink.
- **Integration** (`CopyNotificationDrainerTests`, real Postgres) — Drainer löst Besitzer auf + persistiert; Benachrichtigung für unbekanntes Profil gelöscht.
- **DST** — Host emittiert Fire-and-Forget mit no-op Standard-Sink, daher Copy-Stress-Suite bleibt grün (23/23).
