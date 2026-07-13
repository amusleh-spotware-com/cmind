---
description: "Transaction Cost Analysis — meri kvalitet izvršenja (proklizanje u basis points i implementation shortfall) narudžbine naspram njene arrival price, kompounding execution edge koji banke žive od. Deterministački."
---

# Transaction Cost Analysis (TCA)

Execution alpha je sićušan po trgovini i ogroman preko hiljada njih — to je veliki deo kako banke
i prop desks održavaju svoj edge. TCA meri koliko se cena koju ste zapravo postigli razlikuje od
cene kada ste *odlučili* da trgujete.

Otvorite **cBots → Execution Cost** (`/quant/tca`).

## Šta meri

S obzirom na **arrival (decision) price**, **side**, i vaše **fills** (price × quantity), raportuje:

- **Average fill price (VWAP)** — volumeno-weighted cena koju ste zapravo dobili.
- **Proklizanje (bps)** — drift od arrival do VWAP u basis points, **signed tako da je pozitivan broj je
  trošak** (kupovina iznad arrival ili prodaja ispod njega) i negativan broj je poboljšanje cene.
- **Implementation shortfall** — taj trošak izražen u price × quantity terminima: novac koji vam je drift
  koštao na ovoj narudžbini.

```http
POST /api/quant/tca
{ "arrivalPrice": 1.1000, "side": "Buy",
  "fills": [ { "price": 1.1010, "quantity": 100 }, { "price": 1.1020, "quantity": 100 } ] }
```

## Pametno slicing (Almgren-Chriss)

Pored merenja troška, cMind može planirati veliku narudžbinu da *minimizuje* ga. **cBots → Execution Schedule**
(`/quant/execution`) gradi **Almgren-Chriss optimal-execution schedule**: s obzirom na total quantity,
broj slice-ova, vaš risk aversion, volatilnost i temporary market impact, vraća veličinu za
trading u svakom slice-u. Viši risk aversion **front-loads** schedule (smanjuje timing risk); nula risk
aversion spljošćuje na ravnomerni **TWAP**. Slice-ovi se uvek sabiraju do total-a.

```http
POST /api/quant/execution-schedule
{ "totalQuantity": 100, "slices": 5, "riskAversion": 2, "volatility": 0.02, "temporaryImpact": 0.1 }
```

## Zašto je pouzdano

Čist, deterministički domen kod (`Core.Execution`) bez infrastrukturnih zavisnosti i bez eksternih
poziva — unit-testiran za buy/sell cost sign, price improvement, zero-slippage, VWAP agregaciju, i
input guards. Ovo je merenje polovine execution quality; to je isti shortfall metric koji copy
engine koristi da sudi (i, sa smart slicing, smanji) trošak repliciranih narudžbina.
