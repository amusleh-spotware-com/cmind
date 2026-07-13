---
description: "Na-lastnik dovodijo od varnostno-relevant kopiranje dogodkov — odredišče potovanja zavrnitev breaker, račun-zaščita ali prop-pravilo kršitev, panika razreči. Na po…"
---

# Kopiranje operacijske obvestila (Faza 2b)

Na-lastnik dovodijo od varnostno-relevant kopiranje dogodkov — odredišče potovanja zavrnitev breaker, račun-zaščita ali prop-pravilo kršitev, panika razreči. **Na privzeto** (`App:Copy:NotificationsEnabled`, privzeto `true`); nastavite napačno za umilka. Lastni koncept v Kopiranje kontekstu, ločiti od trg/AI `AlertRule` agregat.

## Kako deluje

Isti izven-pasu gostitelj→umivalnik→drainer vzorec kot izvedbe-prosojnosti dnevnik:

```
CopyEngineHost ──Notify(record)──▶ ICopyNotificationSink
                                     │
             (obvestila izključeno) NullCopyNotificationSink   → zavrženi (brez-op; nespremenjeno motor)
             (obvestila na)  ChannelCopyNotificationSink → omejena DropOldest kanal
                                     │
                                     ▼
                            CopyNotificationDrainer (BackgroundService)
                                     │  resolves vsaka profil lastnik, serije
                                     ▼
                            CopyNotification dovodijo  ◀── GET /api/copy/notifications
```

- Gostitelj `Notify(...)` ne-blokiranje, nikoli vrže — nikoli dotakne DB, nikoli zakasnitev kopiranje.
- Drainer resolves posedovanje `UserId` iz vsaka obvestila profil; obvestila čigar profil gone (lastnik nerazrešljiv) padla, ne sirota.
- `CopyNotification` = dodajanje-samo, na-vrstico-acknowledgeable dovodijo (ne agregat).

## Kaj je dvignjena

| Vrsta | Resnost | Ko |
|------|----------|------|
| `DestinationTripped` | Opozorilo | G8 zavrnitev proračun izčrpan; nove odprte pauzirana za cooldown. |
| `AccountProtectionTriggered` | Kritični | ZuluGuard kapitala tla/strop kršen; odprte zaplatili (SellOut likvidira). |
| `PropRuleBreached` | Kritični | Prop dnevni-izguba / zaostaje-padec kršen; odredišče razreči + zaklenjena za dan. |
| `FlattenAll` | Kritični | Panika razreči izvršena; vsaka odredišče zaprta + zaklenjena. |
| `TokenInvalidated` | (rezervirano) | Odredišče-ov žeton je bil razveljavljen; čakaj rotacija. |

## API

- `GET /api/copy/notifications` (lastnik-obsežen) — uporabnik-ove nedavne obvestila (najbolj nedavne 200) čez vsi profile, plus **neacknowledged** štet.
- `POST /api/copy/notifications/{id}/acknowledge` — označiti ena branja.

## Konfiguracija (`App:Copy`)

| Nastavitev | Privzeto | Učinek |
|---------|---------|--------|
| `NotificationsEnabled` | `true` | Oddajati varnostni obvestila + teči drainer. `false` → brez-op umivalnik. |
