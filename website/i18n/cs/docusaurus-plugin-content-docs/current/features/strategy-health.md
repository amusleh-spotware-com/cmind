---
description: "Strategy Health & Alpha Decay — deterministická detekce rozpadu, která porovnává Sharpe strategie z poslední doby s jejím dřívějším záznamem a lokalizuje největší posun střední hodnoty (CUSUM change-point), vrací verdikt Healthy / Degrading / Decayed."
---

# Strategy Health & Alpha Decay

Každá edge se rozpadá — výzkum je upřímný že half-life kvantitativní strategie se zhroutila z let
na měsíce, takže *adaptace poráží objev*. Strategy Health monitor vám říká, z vlastní historie returns strategie,
zda edge stále je.

Otevřete **cBots → Strategy Health** (`/quant/health`).

## Co dělá

Given a return series (or equity curve, oldest first), it:

- rozdělí historii na **dřívější** a **nedávnou** polovinu a porovná jejich Sharpe ratios;
- provede **CUSUM change-point** scan pro lokalizaci pozorování kde se střední hodnota nejjasněji posunula (a
  regime break), reported only when the deviation is statistically notable;
- vrací verdikt:

| Verdikt | Význam |
|---|---|
| **Healthy** | Recent performance is in line with (or better than) the earlier record. |
| **Degrading** | Recent Sharpe is materially weaker than the earlier record — watch closely. |
| **Decayed** | The edge has effectively disappeared in the recent window — consider pausing. |
| **Unknown** | Not enough history to judge. |

```http
POST /api/quant/health
{ "returns": [...] }   // or { "equity": [...] }
```

## Proč je to spolehlivé

Je to pure, deterministic domain code (`Core.Health`) s žádnou infrastrukturou závislostí a žádnými externími
voláními — unit-tested for decayed, degrading, healthy and too-short cases and for change-point
localization. Je to manuální companion k always-on health checks that back the autonomous agents:
stejné statistiky pohánějí circuit breaker that de-risks a live strategy whose edge is fading.
