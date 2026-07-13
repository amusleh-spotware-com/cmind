---
description: "Regime Lab — labels return series do Calm / Normal / Turbulent volatility regimes i reports per-regime performance, plus Hurst exponent (trend-persistence vs mean-reversion). Deterministyczne."
---

# Regime Lab

Single Sharpe ratio hides truth że most edges są conditional: great w calm, trending markets
i dead w turbulence (lub reverse). Regime Lab breaks strategy's history do volatility
regimes i shows jak to robiło w każdy — więc wiesz *gdy* Twoja edge rzeczywiście works.

Otwórz **cBots → Regime Lab** (`/quant/regimes`).

## Co robi

Biorąc return series (lub equity curve, oldest first), to:

- computes **trailing realized volatility** na każdy point i splits history do **Calm**,
  **Normal** i **Turbulent** regimes przez terciles tego volatility;
- reports **per-regime performance** — observations, mean return, volatility i Sharpe — więc możesz see
  gdzie edge lives;
- estimates **Hurst exponent** via rescaled-range (R/S) analysis: above ~0.55 series to
  **trending / persistent**, below ~0.45 to **mean-reverting**, i around 0.5 to close do random walk.

```http
POST /api/quant/regimes
{ "returns": [...], "window": 10 }   // lub { "equity": [...] }
```

## Dlaczego jest niezawodny

Pure, deterministyczne domain code (`Core.Regimes`) z żadną infrastrukturą dependency i no external calls
— unit-tested dla regime separation (calm vs turbulent volatility) i dla Hurst direction
(anti-persistent series score poniżej 0.5, persistent trend scores above). Same regime signal feeds
autonomous agents' reflection loop, więc agent może lean do regimes gdzie jego edge to real.
