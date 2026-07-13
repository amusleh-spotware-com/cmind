---
description: "Institutional position sizing dla retail — volatility targeting i fractional-Kelly exposure dla single strategy, plus inverse-volatility risk-parity allocation z correlation matrix across book z strategies."
---

# Position Sizing & Portfolio

"Jak duży powinien być ten trade?" to question które decides czy edge compounds lub blows up.
Institutions answer to z **volatility targeting** i **Kelly criterion**, i build book
z **risk parity** rather niż equal dollars. cMind brings oba do retail — deterministyczne math na
strategy's return series, z plain-English recommendation.

Otwórz **cBots → Position Sizing** (`/quant/sizing`).

## Single-strategy sizing

Biorąc strategy's returns (lub equity curve), target annual volatility, Kelly fraction i
leverage cap, sizer reports:

- **Realized annual volatility** — strategy's own volatility, annualizowany przez square-root-of-time
  rule.
- **Volatility-target sizing** — exposure które makes realized volatility meet Twój target
  (`target ÷ realized vol`), capped na Twój leverage limit. Lower-vol strategies earn more size.
- **Full Kelly** — growth-optimal fraction `f* = μ / σ²` (mean over variance z returns).
- **Fractional Kelly** — `f*` scaled przez Twój Kelly fraction. Half-Kelly (0.5) to common safe choice;
  full Kelly to famously too aggressive dla real, uncertain edges.
- **Recommended exposure** — **smaller** (safer) z volatility-target i fractional-Kelly
  sizings, capped. Strategy z no positive edge (full Kelly ≤ 0) to sized do **zero**.

```http
POST /api/quant/sizing
{ "returns": [...], "targetVolatility": 0.10, "kellyFraction": 0.5, "leverageCap": 3 }
```

## Portfolio allocation

Give to dwa lub więcej strategies (aligned return series) i builds book przez **inverse-volatility
risk parity** — każdy strategy weighted przez `1 / volatility`, normalized — więc risk, nie dollars, to shared
evenly. To także returns:

- **correlation matrix** across Twoje strategies (spot ones które są secretly same bet);
- **projected portfolio volatility** na te weights, z sample covariance;
- **leverage** factor które scales całą book toward Twój target volatility (capped).

```http
POST /api/quant/portfolio
{ "strategies": [[...], [...]], "targetVolatility": 0.10, "leverageCap": 3 }
```

## Dlaczego jest niezawodny

Wszystko to to pure, deterministyczne domain code (`Core.Portfolio`) z żadną infrastrukturą dependency i no
external calls — unit-tested dla vol-target scaling, Kelly formula, equal-risk property z
inverse-volatility weights, i correlation matrix. Advisory domyślnie: numbers to recommendation,
nigdy automatic order.
