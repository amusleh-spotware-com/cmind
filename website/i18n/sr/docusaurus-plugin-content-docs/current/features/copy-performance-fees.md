---
description: "Naknada za učinak menadžera na visokoj vodenoj oznaci, standardni model kopiranja trgovanja (cTrader Copy, Darwinex, ZuluTrade profit-share): provajder naplaćuje procenat nove dobiti iznad svakog pratiočevog peak equity-a."
---

# Naknade za učinak pri kopiranju (Faza 4)

Naknada za učinak menadžera na visokoj vodenoj oznaci, standardni model kopiranja trgovanja (cTrader Copy,
Darwinex, ZuluTrade profit-share): provajder naplaćuje procenat *nove* dobiti iznad svakog pratiočevog
peak equity-a — nikad na opening balance, i nikad dva puta za isto tlo koje je već povraćeno. **Opt-in** preko
`App:Copy:FeesEnabled` (podrazumevano isključeno).

## Model (visoka vodena oznaka)

Po destinaciji (pratiočev račun), svako poravnanje:

1. **Prvo poravnanje** seje visoku vodenu oznaku (HWM) na tekući equity → bez naplate (pratilac nikad
   ne plaća na svoj depozit).
2. **Novi maksimum** (equity > HWM): `fee = performanceFeePercent × (equity − HWM)`, zatim `HWM ← equity`.
3. **Na ili ispod peak-a**: bez naplate, HWM nepromenjen — pratilac mora prvo da se oporavi iznad starog peak-a, tako da
   nikad ne plaća dva puta za iste dobitke.

Matematika naknade je domen invariant na `CopyDestination.SettleFee(equity)` — agregat je owns; settlement
servis samo snabdeva anketirani equity i zapisuje vraćeni iznos. `PerformanceFee` je value object ograničen na 50% tako da
pogrešna konfiguracija ne može naplatiti celu pratiočevu dobit.

## Kako se poravnava

```
CopyFeeSettlementService (BackgroundService, samo kada je FeesEnabled)
   │  svaki App:Copy:FeeSettlementInterval
   ├─ učitaj pokrenute profile sa fee-konfigurisanom destinacijom
   ├─ ICopyEquityReader.ReadEquityAsync(ctid)   ← OpenApiCopyEquityReader otvara sesiju,
   │                                               računa balance + floating P&L (PropFirmEquityCalculator)
   ├─ destination.SettleFee(equity)             ← HWM logika na agregatu
   └─ perzistiraj unapređeni HWM + append CopyFeeAccrual (samo na novom maksimumu)
```

- `ICopyEquityReader` je Core apstrakcija; live implementacija (`OpenApiCopyEquityReader`) je jedini
  infra deo — tako da settlement + HWM logika se vežba u testovima sa fake reader-om, bez live brokera.
- `CopyFeeAccrual` je append-only log (HWM-pre, equity, fee %, fee iznos, settled-at) — fact log za
  izveštaj o naknadama i fakturisanje, ne agregat.

## Konfiguracija i API

| `App:Copy` postavka | Podrazumevano | Efekat |
|--------------------|---------|--------|
| `FeesEnabled` | `false` | Pokreni settlement servis. |
| `FeeSettlementInterval` | `1h` | Koliko često se anketira equity i settle-uju naknade. |

Po-destinaciji: `PerformanceFeePercent` (0–50) se postavlja na destinaciji (add/edit destination request).

- `GET /api/copy/profiles/{id}/fees` — fee accruals profila + ukupno naplaćeno.

## Testovi

- **Unit** (`CopyPerformanceFeeTests`) — HWM invariant: prvo poravnanje seje + ne naplaćuje ništa; novi
  maksimum naplaćuje samo dobitak iznad peak-a; na/ispod peak-a ne naplaćuje ništa i peak se nikad ne vraća;
  nakon drawdown-a samo oporavak iznad starog peak-a se naplaćuje; 0% nikad ne naplaćuje; VO
  odbija out-of-range procente.
- **Integration** (`CopyFeeSettlementTests`, real Postgres, fake equity reader) — seed→10k (bez naplate, mark
  sejvan), 12k (naplaćuje 400, mark napreduje), 11k (bez naplate, mark zadržan); accrual perzistiran sa pravim
  owner/iznosom.

Copy host je nedodirnut naknadama (settlement je odvojeni DB posao), tako da copy DST stress suite nije
afektiran (23/23).
