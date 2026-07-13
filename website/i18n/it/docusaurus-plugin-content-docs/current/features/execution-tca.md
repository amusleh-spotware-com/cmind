---
description: "Transaction Cost Analysis — misura la qualità di esecuzione (slippage in basis points e implementation shortfall) di un ordine contro il suo arrival price, il compounding execution edge su cui le banche vivono. Deterministic."
---

# Transaction Cost Analysis (TCA)

L'alpha di esecuzione è tiny per trade ed enormous su migliaia di essi — è una grande parte di come banche
e prop desk mantengono il loro edge. TCA misura quanto il prezzo che hai effettivamente ottenuto si è
allontanato dal prezzo quando hai *deciso* di tradare.

Apri **cBots → Execution Cost** (`/quant/tca`).

## Cosa misura

Dato il **arrival (decision) price**, il **side**, e i tuoi **fills** (price × quantity), riporta:

- **Average fill price (VWAP)** — il prezzo volume-weighted che hai effettivamente ottenuto.
- **Slippage (bps)** — il drift da arrival a VWAP in basis points, **signed così un numero positivo è un
  costo** (comprare sopra arrival o vendere sotto) e un numero negativo è price improvement.
- **Implementation shortfall** — quel costo espresso in termini price × quantity: i soldi che il drift ti è
  costato su questo ordine.

```http
POST /api/quant/tca
{ "arrivalPrice": 1.1000, "side": "Buy",
  "fills": [ { "price": 1.1010, "quantity": 100 }, { "price": 1.1020, "quantity": 100 } ] }
```

## Smart slicing (Almgren-Chriss)

Oltre a misurare il costo, cMind può pianificare un ordine grande per *minimizzarlo*. **cBots → Execution Schedule**
(`/quant/execution`) costruisce uno **Almgren-Chriss optimal-execution schedule**: data la quantità totale,
un numero di slice, la tua risk aversion, volatilità e impatto di mercato temporaneo, restituisce la size
da tradare in ogni slice. Higher risk aversion **front-loads** lo schedule (tagliando timing risk); zero risk
aversion appiattisce a un TWAP uniforme. Le slice sommano sempre al totale.

```http
POST /api/quant/execution-schedule
{ "totalQuantity": 100, "slices": 5, "riskAversion": 2, "volatility": 0.02, "temporaryImpact": 0.1 }
```

## Perché è affidabile

Codice domain puro, deterministico (`Core.Execution`) senza dipendenza infrastructure e senza chiamate esterne
— unit-tested per il buy/sell cost sign, price improvement, zero-slippage, VWAP aggregation, e input guards.
Questa è la metà di misurazione della qualità di esecuzione; è la stessa shortfall metric che il copy
engine usa per giudicare (e, con smart slicing, ridurre) il costo degli ordini mirrorati.
