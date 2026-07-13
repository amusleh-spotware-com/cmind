---
description: "Per-owner feed bezbednosno-relevantnih copy događaja — destinacija aktivira rejection breaker, account-protection ili prop-rule povredu, panic flatten. Podrazumevano uključeno…"
---

# Operativna obaveštenja za kopiranje (Faza 2b)

Per-owner feed bezbednosno-relevantnih copy događaja — destinacija aktivira rejection breaker, account-protection ili prop-rule povredu, panic flatten. **Podrazumevano uključeno** (`App:Copy:NotificationsEnabled`, podrazumevano `true`); postavite na false da utišate. Sopstveni koncept u Copy kontekstu, odvojen od tržišnog/AI `AlertRule` agregata.

## Kako funkcioniše

Isti out-of-band host→sink→drainer pattern kao execution-transparency log:

```
CopyEngineHost ──Notify(record)──▶ ICopyNotificationSink
                                     │
             (obaveštenja isključena) NullCopyNotificationSink   → odbacuje (no-op; nepromenjen engine)
             (obaveštenja uključena)  ChannelCopyNotificationSink → ograničen DropOldest kanal
                                     │
                                     ▼
                            CopyNotificationDrainer (BackgroundService)
                                     │  resoluje owner-a svakog profila, batch-uje
                                     ▼
                            CopyNotification feed  ◀── GET /api/copy/notifications
```

- Host `Notify(...)` ne-blokirajuće, nikad ne baca — nikad ne dodiruje DB, nikad ne odlaže kopiranje.
- Drainer resoluje owning `UserId` iz svakog obaveštenja profila; obaveštenje čiji profil je nestao (owner ne-resolvable) je odbačeno, ne siroče.
- `CopyNotification` = append-only, per-row-acknowledgeable feed (ne agregat).

## Šta se podiže

| Vrsta | Severity | Kada |
|------|----------|---------|
| `DestinationTripped` | Warning | G8 rejection budget iscrpljen; novi otvori pauzirani za cooldown. |
| `AccountProtectionTriggered` | Critical | ZuluGuard equity floor/ceiling prekoračen; otvori latch-ovani (SellOut likvidira). |
| `PropRuleBreached` | Critical | Prop dnevni gubitak / trailing-drawdown prekoračen; destinacija spljoštena + zaključana za dan. |
| `FlattenAll` | Critical | Panic flatten izvršen; svaka destinacija zatvorena + zaključana. |
| `TokenInvalidated` | (rezervisano) | Token destinacije je poništen; čeka rotaciju. |

## API

- `GET /api/copy/notifications` (owner-scoped) — skorašnja obaveštenja korisnika (najskorijih 200) preko svih profila, plus **ne-priznati** broj.
- `POST /api/copy/notifications/{id}/acknowledge` — označi jedno kao pročitano.

## Konfiguracija (`App:Copy`)

| Postavka | Podrazumevano | Efekat |
|---------|---------|--------|
| `NotificationsEnabled` | `true` | Emituje bezbednosna obaveštenja + pokreće drainer. `false` → no-op sink. |

## Testovi

- **Unit** (`CopyNotificationTests`) — tripnuta destinacija podiže `DestinationTripped`; panic flatten podiže profile-level `FlattenAll`. Preko capturing sink-a.
- **Integration** (`CopyNotificationDrainerTests`, real Postgres) — drainer resoluje owner-a + perzistira; obaveštenje za nepoznati profil odbačeno.
- **DST** — host emituje fire-and-forget sa no-op podrazumevanim sink-om, tako da copy stress suite ostaje zelena (23/23).
