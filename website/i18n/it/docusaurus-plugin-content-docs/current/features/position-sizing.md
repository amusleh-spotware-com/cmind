---
description: "Position sizing istituzionale per retail — volatility targeting e esposizione fractional-Kelly per una singola strategia, più allocazione risk-parity inverse-volatility attraverso un book di strategie."
---

# Position Sizing & Portfolio

"Quanto grande dovrebbe essere questo trade?" è la domanda che decide se un edge si compound o esplode.
Le istituzioni rispondono con **volatility targeting** e la **Kelly criterion**, e costruiscono un book
con **risk parity** piuttosto che equal dollars. cMind porta entrambi a retail — matematica deterministica
sulla serie di rendimenti di una strategia, con una raccomandazione in inglese semplice.

Apri **cBots → Position Sizing** (`/quant/sizing`).

## Sizing singola-strategia

Data una serie di rendimenti di una strategia (o equity curve), una target annual volatility, una Kelly
fraction e un leverage cap, il sizer riporta:

- **Realized annual volatility** — la volatilità propria della strategia, annualizzata dalla regola
  square-root-of-time.
- **Volatility-target sizing** — l'esposizione che rende la realized volatility meet your target
  (`target ÷ realized vol`), capped al tuo leverage limit. Strategie lower-vol guadagnano più size.
- **Full Kelly** — la fraction `f* = μ / σ²` (media su varianza dei rendimenti) ottimale per crescita.
- **Fractional Kelly** — `f*` scalato dalla tua Kelly fraction. Half-Kelly (0.5) è la scelta sicura
  comune; Full Kelly è famoso come troppo aggressivo per edge reali, incerti.
- **Recommended exposure** — il **più piccolo** (più sicuro) tra volatility-target e fractional-Kelly
  sizing, capped. Una strategia senza edge positivo (full Kelly ≤ 0) è sized a **zero**.

```http
POST /api/quant/sizing
{ "returns": [...], "targetVolatility": 0.10, "kellyFraction": 0.5, "leverageCap": 3 }
```

## Allocazione portfolio

Dagli due o più strategie (serie di rendimenti allineate) e costruisce un book per **inverse-volatility
risk parity** — ogni strategia pesata da `1 / volatility`, normalizzata — così il rischio, non i dollari,
è condiviso uniformemente. Restituisce anche:

- la **correlation matrix** attraverso le tue strategie (individua quelle che sono segretamente la stessa scommessa);
- la **projected portfolio volatility** a quei pesi, dalla sample covariance;
- un **leverage** factor che scala l'intero book verso la tua target volatility (capped).

```http
POST /api/quant/portfolio
{ "strategies": [[...], [...]], "targetVolatility": 0.10, "leverageCap": 3 }
```

## Perché è affidabile

Tutto è codice domain puro, deterministico (`Core.Portfolio`) senza dipendenza infrastructure e senza chiamate
esterne — unit-tested per il vol-target scaling, la formula Kelly, la proprietà equal-risk degli inverse-volatility
weights, e la correlation matrix. Advisory by default: i numeri sono una raccomandazione, mai un ordine
automatico.
