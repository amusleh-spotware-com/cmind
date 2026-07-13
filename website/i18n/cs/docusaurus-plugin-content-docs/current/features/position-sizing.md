---
description: "Institucionální position sizing pro retail — volatility targeting a fractional-Kelly expozice pro jednu strategii, plus inverse-volatility risk-parity alokace s korelační maticí napříč knihou strategií."
---

# Position Sizing & Portfolio

"Jak velký by měl být tento obchod?" je otázka, která rozhoduje, zda výhoda složí nebo vybuchne.
Instituce na ni odpovídají **volatility targeting** a **Kellyho kritériem**, a staví knihu s **risk parity** spíše než s rovnými dolary. cMind přináší oboje na retail — deterministickou matematiku na řadě výnosů strategie, s doporučením v plain-English.

Otevřete **cBots → Position Sizing** (`/quant/sizing`).

## Position sizing pro jednu strategii

Vzhledem k výnosům strategie (nebo křivce equity), cílové roční volatilitě, Kellyho frakci a limitu páky, sizer hlásí:

- **Realizovaná roční volatilita** — vlastní volatilita strategie, anualizovaná pravidlem druhé odmocniny času.
- **Volatility-target sizing** — expozice, která činí realizovanou volatilitu rovnou vašemu cíli (`cíl ÷ realizovaná vol`), omezená vaším limitem páky. Strategie s nižší volatilitou získávají větší velikost.
- **Plný Kelly** — růstově optimální frakce `f* = μ / σ²` (průměr nad rozptylem výnosů).
- **Frakční Kelly** — `f*` škálovaný vaší Kellyho frakcí. Half-Kelly (0,5) je běžná bezpečná volba; plný Kelly je proslule příliš agresivní pro skutečné, nejisté výhody.
- **Doporučená expozice** — **menší** (bezpečnější) z volatility-target a fractional-Kelly sizingů, omezená. Strategie bez pozitivní výhody (plný Kelly ≤ 0) je sizingována na **nulu**.

```http
POST /api/quant/sizing
{ "returns": [...], "targetVolatility": 0.10, "kellyFraction": 0.5, "leverageCap": 3 }
```

## Alokace portfolia

Dejte mu dvě nebo více strategií (zarovnané řady výnosů) a postaví knihu **inverse-volatility risk parity** — každá strategie vážená `1 / volatilita`, normalizovaná — takže riziko, ne dolary, je rovnoměrně sdíleno. Vrací také:

- **korelační matici** napříč vašimi strategiemi (odhalte ty, které jsou tajně stejnou sázkou);
- **projektovanou volatilitu portfolia** při těchto vahách, ze vzorkové kovariance;
- **pákový** faktor, který škáluje celou knihu směrem k vaší cílové volatilitě (omezený).

```http
POST /api/quant/portfolio
{ "strategies": [[...], [...]], "targetVolatility": 0.10, "leverageCap": 3 }
```

## Proč je to spolehlivé

Všechno je čistý, deterministický doménový kód (`Core.Portfolio`) bez závislosti na infrastruktuře a bez externích volání — unit testováno pro vol-target škálování, Kellyho formuli, vlastnost stejného rizika inverse-volatility vah a korelační matici. Advisory defaultně: čísla jsou doporučením, nikdy automatickým příkazem.
