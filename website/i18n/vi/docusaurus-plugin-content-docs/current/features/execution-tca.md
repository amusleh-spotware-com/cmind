---
description: "Transaction Cost Analysis — measures execution quality (slippage in basis points và implementation shortfall) của một order đối với arrival price, compounding execution edge mà banks sống trên. Deterministic."
---

# Transaction Cost Analysis (TCA)

Execution alpha là tiny per trade và enormous over thousands of them — nó là một phần lớn của cách banks
và prop desks giữ edge của họ. TCA measures how far price bạn thực sự đạt được drift từ
price khi bạn *decided* to trade.

Mở **cBots → Execution Cost** (`/quant/tca`).

## Nó đo gì

Cho **arrival (decision) price**, **side**, và **fills** (price × quantity), nó reports:

- **Average fill price (VWAP)** — volume-weighted price bạn thực sự nhận được.
- **Slippage (bps)** — drift từ arrival to VWAP in basis points, **signed sao một positive number là a
  cost** (buying above arrival hoặc selling below it) và một negative number là price improvement.
- **Implementation shortfall** — cost đó expressed in price × quantity terms: money drift cost
  bạn on this order.

```http
POST /api/quant/tca
{ "arrivalPrice": 1.1000, "side": "Buy",
  "fills": [ { "price": 1.1010, "quantity": 100 }, { "price": 1.1020, "quantity": 100 } ] }
```

## Smart slicing (Almgren-Chriss)

Beyond measuring cost, cMind có thể plan a large order to *minimise* nó. **cBots → Execution Schedule**
(`/quant/execution`) builds an **Almgren-Chriss optimal-execution schedule**: cho total quantity,
a number of slices, your risk aversion, volatility và temporary market impact, nó returns size to
trade in each slice. Higher risk aversion **front-loads** schedule (cutting timing risk); zero risk
aversion flattens to even **TWAP**. Slices luôn sum to total.

```http
POST /api/quant/execution-schedule
{ "totalQuantity": 100, "slices": 5, "riskAversion": 2, "volatility": 0.02, "temporaryImpact": 0.1 }
```

## Tại sao nó đáng tin cậy

Pure, deterministic domain code (`Core.Execution`) với no infrastructure dependency và no external
calls — unit-tested cho buy/sell cost sign, price improvement, zero-slippage, VWAP aggregation, và
input guards. Đây là measurement half của execution quality; nó là same shortfall metric copy
engine uses to judge (và, với smart slicing, reduce) cost của mirrored orders.
