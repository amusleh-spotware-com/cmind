---
description: "Transakcija stroški analiza — meri izvedbe kakovost (zdrsa v osnovno točke in izvršenja premalo) od reda proti njegovo prihod cena, na povezanost izvedbe rob, ki банкам živo na. Deterministična."
---

# Transakcija stroški analiza (TCA)

Izvedbe alfa je majhna na trgovanju in ogromna čez tisočke od njih — to je a velika del kako банк
in prop plošče obdrži njihove rob. TCA meri kako daleč cena ti dejansko doseči zasedel od na
cena ko si *odločil* da trgovati.

Odprite **cBots → Izvedbe stroški** (`/quant/tca`).

## Kaj je mere

Glede na na **prihod (odločitev) cena**, na **stran**, in tvoj **polni** (cena × količina), se poroča:

- **Povprečje polni cena (VWAP)** — na glasnost-teža cena ti dejansko dobiti.
- **Zdrsa (bps)** — na zasedba od prihod do VWAP v osnovno točke, **podpisan tako a pozitiven števila je a
  stroški** (nakupovanje nad prihod ali prodajanje spodaj ga) in a negativna števila je cena izboljšanja.
- **Izvedbe premalo** — ta stroški izražen v cena × količina izraziti: na denar na zasedba stroški
  ti na ta red.

```http
POST /api/quant/tca
{ "arrivalPrice": 1.1000, "side": "Buy",
  "fills": [ { "price": 1.1010, "quantity": 100 }, { "price": 1.1020, "quantity": 100 } ] }
```

## Pametna rezanje (Almgren-Chriss)

Onkraj merjenja stroški, cMind lahko je načrt a velik red do *minimizirati* ga. **cBots → Izvedbe razpored**
(`/quant/execution`) gradi a **Almgren-Chriss optimalnega-izvedbe razpored**: glede na celotni količina,
a števila od rezine, tvoj tveganja aversion, volatility in začasni trg vpliv, se vrne na velikost do
trgovati v vsak rezina. Višje tveganja aversion **spredaj-bremena** na razpored (rezanja časa tveganja); nič tveganja
aversion izplaši do a celo **TWAP**. Na rezine vedno seštejejo do na celotni.
