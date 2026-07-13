---
title: Másolási teljesítménydíjak
description: "Pénzkezelő teljesítménydíjak high-water-mark alapon, a standard másolási trading modell (cTrader Copy, Darwinex, ZuluTrade profit-share): a szolgáltató egy százalékot számít fel az új profitból a követő csúcs equity-je felett."
---

# Másolási teljesítménydíjak (4. fázis)

Pénzkezelő **teljesítménydíjak high-water-mark alapon**, a standard másolási kereskedési modell (cTrader Copy, Darwinex, ZuluTrade profit-share): a szolgáltató egy százalékot számít fel *új* profitból a követő equity csúcsa felett — soha a nyitó egyenlegen, és soha nem kétszer ugyanazért a már visszaszerzett földért. **Opcionális** az `App:Copy:FeesEnabled` révén (alapértelmezés ki).

## A modell (high-water-mark)

Per cél (követő számla), minden elszámolásnál:

1. **Első elszámolás** seed-eli a high-water-mark-ot (HWM) az aktuális equity-re → nincs díj (a követő soha nem számlázódik a letétjére).
2. **Új csúcs** (equity > HWM): `fee = performanceFeePercent × (equity − HWM)`, majd `HWM ← equity`.
3. **A csúcson vagy alatta**: nincs díj, HWM változatlan — a követőnek először vissza kell térnie a régi csúcs fölé, így soha nem számítjuk fel kétszer ugyanazért a nyereségért.

A díj aritmetika egy domain invariáns a `CopyDestination.SettleFee(equity)`-n — az aggregátum birtokolja; az elszámolási szolgáltatás csak a pollolt equity-t szállítja és rögzíti a visszaadott összeget. A `PerformanceFee` egy 50%-ban capped value object, így egy rossz konfiguráció nem terhelheti el a követő teljes nyereségét.

## Hogyan számol el

```
CopyFeeSettlementService (BackgroundService, only when FeesEnabled)
   │  every App:Copy:FeeSettlementInterval
   ├─ load running profiles with a fee-configured destination
   ├─ ICopyEquityReader.ReadEquityAsync(ctid)   ← OpenApiCopyEquityReader opens a session,
   │                                               computes balance + floating P&L (PropFirmEquityCalculator)
   ├─ destination.SettleFee(equity)             ← HWM logic on the aggregate
   └─ persist advanced HWM + append CopyFeeAccrual (only on a new high)
```

- `ICopyEquityReader` egy Core absztrakció; a live implementáció (`OpenApiCopyEquityReader`) az egyetlen infra darab — így az elszámolás + HWM logika tesztelhető fake reader-rel, nincs live broker.
- `CopyFeeAccrual` egy append-only log (HWM-before, equity, fee %, fee amount, settled-at) — egy fact log a díj report-hoz és számlázáshoz, nem aggregátum.

## Konfiguráció & API

| `App:Copy` beállítás | Alapértelmezés | Hatás |
|--------------------|---------|--------|
| `FeesEnabled` | `false` | Futtatja az elszámolási szolgáltatást. |
| `FeeSettlementInterval` | `1h` | Milyen gyakran poll-ol equity-t és számol el díjat. |

Per-destination: `PerformanceFeePercent` (0–50) be van állítva a destination-en (add/edit destination request).

- `GET /api/copy/profiles/{id}/fees` — a profil díj accrual-jei + összes felszámítva.

## Tesztek

- **Egység** (`CopyPerformanceFeeTests`) — a HWM invariáns: első elszámolás seed-el + nem számít díjat; egy új csúcs csak a csúcs feletti nyereséget számítja; a csúcson/alatta nem számít díjat és a csúcs soha nem megy vissza; egy drawdown után csak a régi csúcs feletti helyreállás van felszámítva; 0% soha nem számít; a VO elutasítja a tartományon kívüli százalékokat.
- **Integráció** (`CopyFeeSettlementTests`, valódi Postgres, fake equity reader) — seed→10k (nincs díj, mark advances), 12k (400-at számít, mark előre), 11k (nincs díj, mark held); accrual perzisztálva a megfelelő owner/amount-tal.

A copy host érintetlen a díjak által (az elszámolás egy külön DB munka), így a copy DST stress suite érintetlen (23/23).
