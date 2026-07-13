---
description: "Regime Lab — labelira return series u Calm / Normal / Turbulent volatilnost regime-e i raportuje per-regime performanse, plus Hurst exponent (trend-persistence vs mean-reversion). Deterministački."
---

# Regime Lab

Jedan Sharpe ratio skriva istinu da je većina edge-ova uslovna: odličan u mirnim, trending tržištima
i mrtav u turbulenciji (ili obrnuto). Regime Lab razbija strategijinu istoriju na volatilnost
regime-e i pokazuje kako je prolazila u svakom — tako da znate *kada* vaš edge zapravo funkcioniše.

Otvorite **cBots → Regime Lab** (`/quant/regimes`).

## Šta radi

S obzirom na return series (ili equity curve, najstariji prvo), on:

- računa **trailing realized volatilnost** na svakoj tački i deli istoriju na **Calm**,
  **Normal** i **Turbulent** regime-e po tercima te volatilnosti;
- raportuje **per-regime performanse** — opservacije, mean return, volatilnost i Sharpe — tako da možete videti
  gde edge živi;
- procenjuje **Hurst exponent** preko rescaled-range (R/S) analize: iznad ~0.55 series je
  **trending / persistent**, ispod ~0.45 je **mean-reverting**, i oko 0.5 je blizu random walk-a.

```http
POST /api/quant/regimes
{ "returns": [...], "window": 10 }   // ili { "equity": [...] }
```

## Zašto je pouzdano

Čist, deterministički domen kod (`Core.Regimes`) bez infrastrukturnih zavisnosti i bez eksternih poziva
— unit-testiran za regime separaciju (calm vs turbulent volatilnost) i za Hurst direction
(anti-persistent series score-uje ispod 0.5, persistent trend score-uje iznad). Isti regime signal hrani
autonomne agente' reflection loop, tako da agent može da se nagne u regime-e gde je njegov edge stvaran.
