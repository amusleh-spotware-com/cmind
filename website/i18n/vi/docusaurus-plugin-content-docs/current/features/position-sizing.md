---
description: "Institutional position sizing cho retail — volatility targeting và fractional-Kelly exposure cho một strategy, cộng inverse-volatility risk-parity allocation với correlation matrix across a book of strategies."
---

# Position Sizing & Portfolio

"Cỡ nào trade này nên là?" là câu hỏi quyết định liệu một edge compounds hay blows up.
Institutions trả lời nó với **volatility targeting** và **Kelly criterion**, và họ build a book
với **risk parity** hơn là equal dollars. cMind mang cả hai đến retail — deterministic math on a
strategy's return series, với plain-English recommendation.

Mở **cBots → Position Sizing** (`/quant/sizing`).

## Single-strategy sizing

Cho a strategy's returns (hoặc equity curve), a target annual volatility, a Kelly fraction và a
leverage cap, sizer reports:

- **Realized annual volatility** — strategy's own volatility, annualized by square-root-of-time
  rule.
- **Volatility-target sizing** — exposure that makes realized volatility meet your target
  (`target ÷ realized vol`), capped at your leverage limit. Lower-vol strategies earn more size.
- **Full Kelly** — growth-optimal fraction `f* = μ / σ²` (mean over variance của returns).
- **Fractional Kelly** — `f*` scaled by your Kelly fraction. Half-Kelly (0.5) là common safe choice;
  full Kelly famously too aggressive for real, uncertain edges.
- **Recommended exposure** — **smaller** (safer) của volatility-target và fractional-Kelly
  sizings, capped. Strategy với no positive edge (full Kelly ≤ 0) sized to **zero**.

```http
POST /api/quant/sizing
{ "returns": [...], "targetVolatility": 0.10, "kellyFraction": 0.5, "leverageCap": 3 }
```

## Portfolio allocation

Give nó hai hoặc nhiều strategies (aligned return series) và nó builds a book by **inverse-volatility
risk parity** — mỗi strategy weighted by `1 / volatility`, normalized — vì vậy risk, không phải dollars, shared
evenly. Nó cũng returns:

- **correlation matrix** across strategies (spot những cái secretly same bet);
- **projected portfolio volatility** at those weights, from sample covariance;
- **leverage** factor that scales whole book toward your target volatility (capped).

```http
POST /api/quant/portfolio
{ "strategies": [[...], [...]], "targetVolatility": 0.10, "leverageCap": 3 }
```

## Tại sao nó đáng tin cậy

Tất cả là pure, deterministic domain code (`Core.Portfolio`) với no infrastructure dependency và no
external calls — unit-tested cho vol-target scaling, Kelly formula, equal-risk property của
inverse-volatility weights, và correlation matrix. Advisory by default: numbers là a
recommendation, never an automatic order.
