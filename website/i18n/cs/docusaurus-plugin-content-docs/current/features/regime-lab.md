---
description: "Regime Lab — labels a return series into Calm / Normal / Turbulent volatility regimes and reports per-regime performance, plus the Hurst exponent (trend-persistence vs mean-reversion). Deterministic."
---

# Regime Lab

A single Sharpe ratio hides the truth that most edges are conditional: great in calm, trending markets
and dead in turbulence (or the reverse). The Regime Lab breaks a strategy's history into volatility
regimes and shows how it did in each — so you know *when* your edge actually works.

Open **cBots → Regime Lab** (`/quant/regimes`).

## What it does

Given a return series (or equity curve, oldest first), it:

- computes a **trailing realized volatility** at each point and splits the history into **Calm**,
  **Normal** and **Turbulent** regimes by the terciles of that volatility;
- reports **per-regime performance** — observations, mean return, volatility and Sharpe — so you can see
  where the edge lives;
- estimates the **Hurst exponent** via rescaled-range (R/S) analysis: above ~0.55 the series is
  **trending / persistent**, below ~0.45 it is **mean-reverting**, and around 0.5 it is close to a
  random walk.

```http
POST /api/quant/regimes
{ "returns": [...], "window": 10 }   // or { "equity": [...] }
```

## Why it is reliable

Pure, deterministic domain code (`Core.Regimes`) with no infrastructure dependency and no external calls
— unit-tested for regime separation (calm vs turbulent volatility) and for the Hurst direction
(anti-persistent series score below 0.5, a persistent trend scores above). The same regime signal feeds
the autonomous agents' reflection loop, so an agent can lean into the regimes where its edge is real.
