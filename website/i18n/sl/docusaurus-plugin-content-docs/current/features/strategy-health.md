---
description: "Zdravje strategije in razpad alfa — deterministična zaznava razpada, ki primerja nedavni Sharpe strategije s starejšim zapisom in locira največji premik povprečja (CUSUM sprememba točke), ki vrne Healthy / Degrading / Decayed vrdnost."
---

# Zdravje strategije in razpad alfa

Vsak rob se razpada — raziskava je jasna, da se polovični čas strategije kvantitativnega trgovanja je skrčil od let na mesece, zato se *adaptacija bolj izplača kot odkrivanje*. Monitor zdravja strategije vam pove neposredno iz zgodovine donosov strategije, ali je ta rob še vedno prisoten.

Odprite **cBots → Strategy Health** (`/quant/health`).

## Kaj počne

Glede na vrsto donosov (ali krivuljo kapitala, od najstarejšega naprej) to počne:

- deli zgodovino na **starejšo** in **nedavno** polovico ter primerja njuni Sharpe razmerji;
- požene **CUSUM spremembo točke** skeniranja, da locira opazovanje, kjer se je povprečje najbolj jasno spremenilo (režimski preobrat), poročano samo, ko je odstopanje statistično opazno;
- vrne vrdnost:

| Vrdnost | Pomen |
|---|---|
| **Healthy** | Nedavna uspešnost je v skladu z (ali boljša od) starejšim zapisom. |
| **Degrading** | Nedavni Sharpe je materijalno šibkejši kot starejši zapis — pozorno spremljajte. |
| **Decayed** | Rob je praktično izginil v nedavnem oknu — razmislite o premoru. |
| **Unknown** | Premalo zgodovine za oceno. |

- **Neposredno iz backtesta — brez kopiranja in lepljenja.** Vsak zaključeni backtest razkrije ikono **Preverite zdravje strategije** na vrstici seznama **Backtest** in v njegovem detaljnem pogledu primerka; en klik zažene monitor na shranjeni krivulji kapitala te vožnje in prikaže vrdnost v dialogu. Ikona je onemogočena, dokler se backtest ni zaključil in je ustvaril poročilo, zato nikoli ni neopustljiv nadzor. V ozadju je to `POST /api/quant/health/backtest/{instanceId}`, ki bere shranjeno krivuljo kapitala poročila.

```http
POST /api/quant/health
{ "returns": [...] }   // or { "equity": [...] }
```

## Zakaj je zanesljivo

Je čist, determinističen domenski kod (`Core.Health`) brez odvisnosti infrastrukture in brez zunanjih klicev — enočno preizkušen za razpadene, degradirane, zdrave primere in primere s prekratek zgodovino ter za lokalizacijo spremembe točke. Je ročni sopotnik k vedno vključenim preveroam zdravja, ki podpirajo avtonomne agente: enaka statistika poganja prekidač, ki zmanjšuje tveganje aktivne strategije, katere rob bledí.
