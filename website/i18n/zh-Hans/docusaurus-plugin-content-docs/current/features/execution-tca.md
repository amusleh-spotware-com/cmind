---
description: "Transaction Cost Analysis — measures execution quality (slippage in basis points and implementation shortfall) of an order against its arrival price, the compounding execution edge that banks live on. Deterministic."
---

# Transaction Cost Analysis (TCA)

Execution alpha is tiny per trade and enormous over thousands of them — it is a large part of how banks
and prop desks keep their edge. TCA measures how far the price you actually achieved drifted from the
price when you *decided* to trade.

Open **cBots → Execution Cost** (`/quant/tca`).

## What it measures

Given the **arrival (decision) price**, the **side**, and your **fills** (price × quantity), it reports:

- **Average fill price (VWAP)** — the volume-weighted price you actually got.
- **Slippage (bps)** — the drift from arrival to VWAP in basis points, **signed so a positive number is a
  cost** (buying above arrival or selling below it) and a negative number is price improvement.
- **Implementation shortfall** — that cost expressed in price × quantity terms: the money the drift cost
  you on this order.

```http
POST /api/quant/tca
{ "arrivalPrice": 1.1000, "side": "Buy",
  "fills": [ { "price": 1.1010, "quantity": 100 }, { "price": 1.1020, "quantity": 100 } ] }
```

## Smart slicing (Almgren-Chriss)

Beyond measuring cost, cMind can plan a large order to *minimise* it. **cBots → Execution Schedule**
(`/quant/execution`) builds an **Almgren-Chriss optimal-execution schedule**: given the total quantity,
a number of slices, your risk aversion, volatility and temporary market impact, it returns the size to
trade in each slice. Higher risk aversion **front-loads** the schedule (cutting timing risk); zero risk
aversion flattens to an even **TWAP**. The slices always sum to the total.

```http
POST /api/quant/execution-schedule
{ "totalQuantity": 100, "slices": 5, "riskAversion": 2, "volatility": 0.02, "temporaryImpact": 0.1 }
```

## Why it is reliable

Pure, deterministic domain code (`Core.Execution`) with no infrastructure dependency and no external
calls — unit-tested for the buy/sell cost sign, price improvement, zero-slippage, VWAP aggregation, and
input guards. This is the measurement half of execution quality; it is the same shortfall metric the copy
engine uses to judge (and, with smart slicing, reduce) the cost of mirrored orders.

<!-- [ZH-HANS] Translation needed -->
