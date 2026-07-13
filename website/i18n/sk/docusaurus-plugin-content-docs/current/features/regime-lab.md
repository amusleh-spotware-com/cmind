---
description: "Regime Lab — označuje sériu výnosov do Calm / Normal / Turbulent volatility režimov a hlási per-režim performance, plus Hurst exponent (trend-persistence vs mean-reversion). Deterministic."
---

# Regime Lab

Jeden Sharpe ratio skrýva pravdu, že väčšina výhod je podmienečná: skvelá v pokojných, trending trhoch
a mŕtva v turbulencii (alebo naopak). Regime Lab rozloží históriu stratégie do volatility
režimov a ukáže, ako si viedla v každom — takže viete, *kedy* vaša výhoda skutočne funguje.

Otvorte **cBots → Regime Lab** (`/quant/regimes`).

## Čo to robí

Pri danej sérii výnosov (alebo equity curve, najstaršie prvé),:

- počíta **trailing realizovanú volatilitu** v každom bode a rozdeľuje históriu do **Calm**,
  **Normal** a **Turbulent** režimov podľa terciálov tejto volatility;
- hlási **per-režim performance** — observations, mean return, volatility a Sharpe — takže môžete vidieť,
  kde výhoda žije;
- odhaduje **Hurst exponent** cez rescaled-range (R/S) analýzu: nad ~0.55 séria je
  **trending / persistent**, pod ~0.45 je **mean-reverting** a okolo 0.5 je blízko náhodnému wish.

```http
POST /api/quant/regimes
{ "returns": [...], "window": 10 }   // alebo { "equity": [...] }
```

## Prečo je spoľahlivý

Čistý, deterministický doménový kód (`Core.Regimes`) bez infraštruktúrnej závislosti a bez externých volaní
— unit-testovaný pre režimovú separáciu (calm vs turbulent volatility) a pre Hurst direction
(anti-persistentná séria skóruje pod 0.5, persistentný trend skóruje nad). Rovnaký režimový signál
napája reflection loop autonómnych agentov, takže agent sa môže nakloniť do režimov, kde jeho výhoda je reálna.
