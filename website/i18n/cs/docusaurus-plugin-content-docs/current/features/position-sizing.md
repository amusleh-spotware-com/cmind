---
description: "Institutional position sizing for retail — volatility targeting and fractional-Kelly exposure for a single strategy, plus inverse-volatility risk-parity allocation with a correlation matrix across a book of strategies."
---

# Position Sizing & Portfolio

"How big should this trade be?" is the question that decides whether an edge compounds or blows up.
Institutions answer it with **volatility targeting** and the **Kelly criterion**, and they build a book
with **risk parity** rather than equal dollars. cMind brings both to retail — deterministic math on a
strategy's return series, with a plain-English recommendation.

Open **cBots → Position Sizing** (`/quant/sizing`).

## Single-strategy sizing

Given a strategy's returns (or equity curve), a target annual volatility, a Kelly fraction and a
leverage cap, the sizer reports:

- **Realized annual volatility** — the strategy's own volatility, annualized by the square-root-of-time
  rule.
- **Volatility-target sizing** — the exposure that makes realized volatility meet your target
  (`target ÷ realized vol`), capped at your leverage limit. Lower-vol strategies earn more size.
- **Full Kelly** — the growth-optimal fraction `f* = μ / σ²` (mean over variance of the returns).
- **Fractional Kelly** — `f*` scaled by your Kelly fraction. Half-Kelly (0.5) is the common safe choice;
  full Kelly is famously too aggressive for real, uncertain edges.
- **Recommended exposure** — the **smaller** (safer) of the volatility-target and fractional-Kelly
  sizings, capped. A strategy with no positive edge (full Kelly ≤ 0) is sized to **zero**.

```http
POST /api/quant/sizing
{ "returns": [...], "targetVolatility": 0.10, "kellyFraction": 0.5, "leverageCap": 3 }
```

## Portfolio allocation

Give it two or more strategies (aligned return series) and it builds a book by **inverse-volatility
risk parity** — each strategy weighted by `1 / volatility`, normalized — so risk, not dollars, is shared
evenly. It also returns:

- the **correlation matrix** across your strategies (spot the ones that are secretly the same bet);
- the **projected portfolio volatility** at those weights, from the sample covariance;
- a **leverage** factor that scales the whole book toward your target volatility (capped).

```http
POST /api/quant/portfolio
{ "strategies": [[...], [...]], "targetVolatility": 0.10, "leverageCap": 3 }
```

## Why it is reliable

All of it is pure, deterministic domain code (`Core.Portfolio`) with no infrastructure dependency and no
external calls — unit-tested for the vol-target scaling, the Kelly formula, the equal-risk property of
inverse-volatility weights, and the correlation matrix. Advisory by default: the numbers are a
recommendation, never an automatic order.
