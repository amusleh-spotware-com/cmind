---
description: "Strategy Health & Alpha Decay — deterministic decay detection that compares a strategy's recent Sharpe to its earlier record and locates the biggest mean-shift (CUSUM change-point), returning a Healthy / Degrading / Decayed verdict."
---

# Strategy Health & Alpha Decay

Every edge decays — the research is blunt that the half-life of a quant strategy has collapsed from years
to months, so *adaptation beats discovery*. The Strategy Health monitor tells you, from a strategy's own
return history, whether the edge is still there.

Open **cBots → Strategy Health** (`/quant/health`).

## What it does

Given a return series (or equity curve, oldest first), it:

- splits the history into an **earlier** and a **recent** half and compares their Sharpe ratios;
- runs a **CUSUM change-point** scan to locate the observation where the mean most clearly shifted (a
  regime break), reported only when the deviation is statistically notable;
- returns a verdict:

| Verdict | Meaning |
|---|---|
| **Healthy** | Recent performance is in line with (or better than) the earlier record. |
| **Degrading** | Recent Sharpe is materially weaker than the earlier record — watch closely. |
| **Decayed** | The edge has effectively disappeared in the recent window — consider pausing. |
| **Unknown** | Not enough history to judge. |

```http
POST /api/quant/health
{ "returns": [...] }   // or { "equity": [...] }
```

## Why it is reliable

It is pure, deterministic domain code (`Core.Health`) with no infrastructure dependency and no external
calls — unit-tested for the decayed, degrading, healthy and too-short cases and for change-point
localization. It is the manual companion to the always-on health checks that back the autonomous agents:
the same statistics drive the circuit breaker that de-risks a live strategy whose edge is fading.

<!-- [ZH-HANS] Translation needed -->
