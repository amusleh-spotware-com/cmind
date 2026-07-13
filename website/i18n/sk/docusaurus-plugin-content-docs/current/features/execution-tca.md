---
description: "Transaction Cost Analysis — meria kvalitu exekúcie (slippage v basis bodoch a implementation shortfall) objednávky voči jej arrival price, zložený execution edge, na ktorom banky žijú. Deterministic."
---

# Transaction Cost Analysis (TCA)

Execution alpha je malý per trade a obrovský cez tisíce z nich — je to veľká časť toho, ako banky
a prop desks udržujú svoju výhodu. TCA meria, ako ďaleko sa cena, ktorú ste skutočne dosiahli, odchýlila od
ceny, keď ste sa *rozhodli* obchodovať.

Otvorte **cBots → Execution Cost** (`/quant/tca`).

## Čo meria

Pri danej **arrival (rozhodovacej) cene**, **side** a vašich **fills** (cena × množstvo), hlási:

- **Priemerná fill cena (VWAP)** — vážená cena objemu, ktorú ste skutočne dostali.
- **Slippage (bps)** — drift od arrival k VWAP v basis bodoch, **signed takže kladné číslo je
  cost** (nákup nad arrival alebo predaj pod ním) a záporné číslo je zlepšenie ceny.
- **Implementation shortfall** — tento cost vyjadrený v termoch ceny × množstvo: peniaze, ktoré drift
  stál na tejto objednávke.

```http
POST /api/quant/tca
{ "arrivalPrice": 1.1000, "side": "Buy",
  "fills": [ { "price": 1.1010, "quantity": 100 }, { "price": 1.1020, "quantity": 100 } ] }
```

## Smart slicing (Almgren-Chriss)

Nad rámec merania nákladov môže cMind naplánovať veľkú objednávku na *minimalizáciu* nákladov. **cBots → Execution Schedule**
(`/quant/execution`) stavia **Almgren-Chriss optimal-execution schedule**: pri danom total quantity,
 počte slicov, vašej risk aversion, volatilite a dočasnom market impact vráti veľkosť na
obchodovanie v každom slice. Vyššia risk aversion **front-loaduje** schedule (znižuje timing risk);
zero risk aversion vyrovná na rovnaký **TWAP**. Slices vždy sčítavajú do total.

```http
POST /api/quant/execution-schedule
{ "totalQuantity": 100, "slices": 5, "riskAversion": 2, "volatility": 0.02, "temporaryImpact": 0.1 }
```

## Prečo je spoľahlivý

Čistý, deterministický doménový kód (`Core.Execution`) bez infraštruktúrnej závislosti a bez externých
volaní — unit-testovaný pre buy/sell cost sign, price improvement, zero-slippage, VWAP agregáciu a
input guards. Toto je meracia polovica execution quality; je to rovnaký shortfall metric, ktorý copy
engine používa na posudzovanie (a, so smart slicing, redukciu) nákladov zrkadlených objednávok.
