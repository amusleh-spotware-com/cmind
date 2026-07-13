---
description: "Strategy Health & Alpha Decay — deterministyczne decay detection które compares strategy's recent Sharpe do jego earlier record i locates biggest mean-shift (CUSUM change-point), returning Healthy / Degrading / Decayed verdict."
---

# Strategy Health & Alpha Decay

Każdy edge decays — research to blunt że half-life z quant strategy collapsed z years
do months, więc *adaptation beats discovery*. Strategy Health monitor tells Ci, z strategy's own
return history, czy edge to wciąż tam.

Otwórz **cBots → Strategy Health** (`/quant/health`).

## Co robi

Biorąc return series (lub equity curve, oldest first), to:

- splits history do **earlier** i **recent** half i compares ich Sharpe ratios;
- runs **CUSUM change-point** scan do locate observation gdzie mean most clearly shifted (regime
  break), reported tylko gdy deviation to statistically notable;
- returns verdict:

| Verdict | Znaczenie |
|---|---|
| **Healthy** | Recent performance to w line z (lub lepszy niż) earlier record. |
| **Degrading** | Recent Sharpe to materially weaker niż earlier record — watch closely. |
| **Decayed** | Edge effectively disappeared w recent window — consider pausing. |
| **Unknown** | Nie enough history do judge. |

```http
POST /api/quant/health
{ "returns": [...] }   // lub { "equity": [...] }
```

## Dlaczego jest niezawodny

To to pure, deterministyczne domain code (`Core.Health`) z żadną infrastrukturą dependency i no external
calls — unit-tested dla decayed, degrading, healthy i too-short cases i dla change-point
localization. To to manual companion do always-on health checks że back autonomous agents:
same statistics drive circuit breaker że de-risks live strategy którego edge to fading.
