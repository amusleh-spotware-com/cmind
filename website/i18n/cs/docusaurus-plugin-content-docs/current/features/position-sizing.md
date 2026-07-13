---
description: "Institutional position sizing pro retail — volatility targeting a fractional-Kelly expozice pro jednu strategii, plus inverse-volatility risk-parity alokace s korelační maticí napříč knihou strategií."
---

# Position Sizing & Portfolio

"How big should this trade be?" je otázka která rozhoduje zda edge kompenzuje nebo vybuchne.
Institutiony na ni odpovídají **volatility targeting** a **Kelly criterion**, a staví knihu s
**risk parity** spíše než rovnými dolary. cMind přináší oboje na retail — deterministická matematika na a
strategy's return series, s plain-English doporučením.

Otevřete **cBots → Position Sizing** (`/quant/sizing`).

## Single-strategy sizing

Given a strategy's returns (or equity curve), a target annual volatility, a Kelly fraction and a
leverage cap, sizer reportuje:

- **Realized annual volatility** — strategy's own volatility, annualizovaná druhou odmocninou času
  rule.
- **Volatility-target sizing** — expozice která činí realized volatility meet your target
  (`target ÷ realized vol`), capped at your leverage limit. Nižší-vol strategie earns more size.
- **Full Kelly** — growth-optimal fraction `f* = μ / σ²` (mean over variance of the returns).
- **Fractional Kelly** — `f*` scaled by your Kelly fraction. Half-Kelly (0.5) je běžná bezpečná volba;
  full Kelly je famously too aggressive for real, uncertain edges.
- **Recommended exposure** — **menší** (bezpečnější) z volatility-target a fractional-Kelly
  sizing, capped. Strategie s žádným pozitivním edge (full Kelly ≤ 0) je sized to **zero**.

```http
POST /api/quant/sizing
{ "returns": [...], "targetVolatility": 0.10, "kellyFraction": 0.5, "leverageCap": 3 }
```

## Portfolio allocation

Dejte mu dvě nebo více strategií (aligned return series) a postaví knihu **inverse-volatility
risk parity** — každá strategie weighted by `1 / volatility`, normalized — takže risk, ne dolary, je sdílen
evenly. Vrací také:

- **korelační matici** across your strategies (zjistěte které jsou tajně stejná sázka);
- **projektovanou portfolio volatility** při těchto vahách, from the sample covariance;
- **leverage** factor that scales the whole book toward your target volatility (capped).

```http
POST /api/quant/portfolio
{ "strategies": [[...], [...]], "targetVolatility": 0.10, "leverageCap": 3 }
```

## Proč je to spolehlivé

Všechno je pure, deterministic domain code (`Core.Portfolio`) s žádnou infrastrukturou závislostí a žádnými
externími voláními — unit-tested for vol-target scaling, Kelly formula, equal-risk property of
inverse-volatility weights, and the correlation matrix. Advisory by default: čísla jsou a
recommendation, never an automatic order.
