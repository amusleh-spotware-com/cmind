---
description: "Transaction Cost Analysis — measures execution quality (slippage w basis points i implementation shortfall) z order contra arrival price, compounding execution edge które banks żyją na. Deterministyczne."
---

# Transaction Cost Analysis (TCA)

Execution alpha to tiny per trade i enormous over thousands z nich — to large part z jak banks
i prop desks keep ich edge. TCA measures jak daleko price którą rzeczywiście achieved drifted z
price gdy *zdecydowałeś* do trade.

Otwórz **cBots → Execution Cost** (`/quant/tca`).

## Co measures

Biorąc **arrival (decision) price**, **side**, i Twoje **fills** (price × quantity), to reports:

- **Average fill price (VWAP)** — volume-weighted price którą rzeczywiście got.
- **Slippage (bps)** — drift z arrival do VWAP w basis points, **signed więc positive number to cost**
  (buying above arrival lub selling below to) i negative number to price improvement.
- **Implementation shortfall** — ten cost expressed w price × quantity terms: pieniądze które drift cost
  na ten order.

```http
POST /api/quant/tca
{ "arrivalPrice": 1.1000, "side": "Buy",
  "fills": [ { "price": 1.1010, "quantity": 100 }, { "price": 1.1020, "quantity": 100 } ] }
```

## Smart slicing (Almgren-Chriss)

Beyond measuring cost, cMind może plan large order do *minimize* to. **cBots → Execution Schedule**
(`/quant/execution`) builds **Almgren-Chriss optimal-execution schedule**: biorąc total quantity,
number z slices, Twoja risk aversion, volatility i temporary market impact, zwraca size do
trade w każdy slice. Wyższy risk aversion **front-loads** schedule (cutting timing risk); zero risk
aversion flattens do even **TWAP**. Slices zawsze sum do total.

```http
POST /api/quant/execution-schedule
{ "totalQuantity": 100, "slices": 5, "riskAversion": 2, "volatility": 0.02, "temporaryImpact": 0.1 }
```

## Dlaczego jest niezawodny

Pure, deterministyczne domain code (`Core.Execution`) z żadną infrastrukturą dependency i no external
calls — unit-tested dla buy/sell cost sign, price improvement, zero-slippage, VWAP aggregation, i
input guards. To measurement half z execution quality; to jest same shortfall metric które copy
engine uses do judge (i, z smart slicing, reduce) cost z mirrored orders.
