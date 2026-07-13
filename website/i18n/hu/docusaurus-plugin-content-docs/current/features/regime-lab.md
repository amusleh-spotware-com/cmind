---
title: Rezim Lab
description: "Regime detekcio es vizualizacio a backtest historian - azonositsd a trend es sideways periodusokat az era Strategic szamara."
---

# Rezim Lab

# Regime Lab

Regime detection and visualization on your backtest history — identify the trending and sideways periods for the era Strategy.

Open **cBots → Regime Lab** (`/quant/regime`).

## What it does

Enter a backtest instance ID or paste a return series and it identifies:

- **Trending vs. ranging** periods using a rolling volatility/zscore filter.
- **Average length** of each regime.
- **Return distribution** per regime (did your strategy actually make money in trends, or only in range-bounces?).

```http
POST /api/quant/regime
{ "returns": [0.006, 0.004, -0.002, ...] }
```

## Why it is reliable

Pure deterministic domain code (`Core.Quant.RegimeLab`) with no infrastructure dependency — unit-tested for regime boundary detection, edge cases (all-trending, all-range, single data point), and output consistency.
