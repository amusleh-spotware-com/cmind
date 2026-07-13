---
title: Stratégia Egeszseg
description: "Automatizalt strategi review - azonositja a gyakori cBot hibe mintakat (entry timing, risk reward, drawdown) es javaslatok a fejlesztesre."
---

# Stratégia Egeszseg

Automatizált stratégia áttekintés - azonosítja a gyakori cBot hiba mintákat (belépés időzítés, kockázat/nyereség arány, drawdown) és javaslatok a fejlesztésre.

Open **cBots → Strategy Health** (`/quant/health`).

## What it checks

From a completed backtest instance it computes:

- **Win rate, average R:R** — the basics, done deterministically from the instance result.
- **Drawdown profile** — max drawdown, average drawdown depth, recovery time.
- **Entry timing** — does the strategy tend to enter near the high/low of the bar? (a common mistake).
- **Risk rules** — was SL/TP hit first most often? (means the TP is unrealistic).

```http
GET /api/quant/health/{instanceId}
```

## Why it is reliable

Pure deterministic domain code (`Core.Quant.StrategyHealth`) with no infrastructure dependency — unit-tested for each check, boundary conditions, and empty instance path. Advisory by default: the findings are recommendations, never an automatic action.
