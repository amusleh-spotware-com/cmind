---
description: "Denar-menedžer zmogljivost pristojbine na a visoko-vode-oznaka, standardna kopiranje-trgovinski model (cTrader Kopiranje, Darwinex, ZuluTrade dobička-deliti): ponudnik zaračuna…"
---

# Kopiranje zmogljivost pristojbine (Faza 4)

Denar-menedžer **zmogljivost pristojbine na a visoko-vode-oznaka**, standardna kopiranje-trgovinski model (cTrader Kopiranje,
Darwinex, ZuluTrade dobička-deliti): ponudnik zaračuna a odstotek od *nova* dobička nad vsaka sledilca-jev
vrhuncu kapitala — nikoli na odpiranje saldo, in nikoli dvakrat za tlo že obnovi. **Opt-in** preko
`App:Copy:FeesEnabled` (izključeno privzeto).

## Model (visoko-vode-oznaka)

Na odredišče (sledilca račun), vsak poravnava:

1. **Prvi poravnava** semena visoko-vode-oznaka (HWM) na trenutne kapitala → brez naboja (a sledilca je
   nikoli zaračunan na svojega depozita).
2. **Nova visoko** (kapitala > HWM): `fee = performanceFeePercent × (kapitala − HWM)`, nato `HWM ← kapitala`.
3. **Na ali spodaj vrhuncu**: brez pristojbine, HWM nespremenjeno — sledilca moram prvi obnoviti mimo starih vrhuncu, tako
   nikoli zaračunan dvakrat za isti dobički.

Pristojbina aritmetika je domeni invarianta na `CopyDestination.SettleFee(equity)` — agregat lastni ga; na
poravnava storitve samo oskrba polled kapitala in zapisi vrnjeni znesek. `PerformanceFee` je a
vrednosti objekta kapljico pri 50% tako a napačna konfiguracija ne more zaračunan preč sledilca-jev celoten dobička.

## Kako se poravnava

```
CopyFeeSettlementService (BackgroundService, samo ko je FeesEnabled)
   │  vsak App:Copy:FeeSettlementInterval
   ├─ naloži tečeče profile z fee-konfiguriran odredišče
   ├─ ICopyEquityReader.ReadEquityAsync(ctid)   ← OpenApiCopyEquityReader odpre sejo,
   │                                               izračuna saldo + plavajoče P&L (PropFirmEquityCalculator)
   ├─ destination.SettleFee(equity)             ← HWM logika na agregat
   └─ obstanek napredoval HWM + dodajanje CopyFeeAccrual (samo na nova visoko)
```

- `ICopyEquityReader` je a Core abstrakcija; živo izvršitev (`OpenApiCopyEquityReader`) je edini
  infra kos — zato poravnava + HWM logika je vježbana v testi z a fake bralec, brez živo posredovalstvo.
- `CopyFeeAccrual` je a dodajanje-samo dnevnik (HWM-prej, kapitala, pristojbina %, pristojbina znesek, poravnana-na) — a dejstvo dnevnik za
  pristojbina poročilo in obračun, ne agregat.

## Konfiguracija & API

| `App:Copy` nastavitev | Privzeto | Učinek |
|--------------------|---------|--------|
| `FeesEnabled` | `false` | Teči poravnava storitve. |
| `FeeSettlementInterval` | `1h` | Kako pogosto kapitala je polled in pristojbine poravnano. |

Na-odredišče: `PerformanceFeePercent` (0–50) je nastavljeni na odredišče (dodaj/urediti odredišče zahteva).
