---
description: "Strategy Health & Alpha Decay — deterministička detekcija decay-a koja upoređuje Sharpe strategije u skorijem periodu sa njenim ranijim rekordom i locira najveći mean-shift (CUSUM change-point), vraćajući Healthy / Degrading / Decayed presudu."
---

# Strategy Health & Alpha Decay

Svaki edge decay-uje — istraživanje je direktno da je half-life kvant strategije kolabriralo sa godina
na mesece, tako da *adaptacija pobeđuje otkriće*. Strategy Health monitor vam govori, iz strategijine sopstvene
istorije return-a, da li je edge još tu.

Otvorite **cBots → Strategy Health** (`/quant/health`).

## Šta radi

S obzirom na return series (ili equity curve, najstariji prvo), on:

- deli istoriju na **raniji** i **skoriji** polovinu i upoređuje njihove Sharpe ratios;
- pokreće **CUSUM change-point** sken da locira opservaciju gde se srednja najjasnije pomerala (a
  regime break), prijavljenu samo kada je devijacija statistički primetna;
- vraća presudu:

| Presuda | Značenje |
|---|---|
| **Healthy** | Skorija performansa je u skladu sa (ili bolja od) ranijeg rekorda. |
| **Degrading** | Skorija Sharpe je materijalno slabija od ranijeg rekorda — pažljivo pratite. |
| **Decayed** | Edge je efektivno nestao u skorijem prozoru — razmislite o pauziranju. |
| **Unknown** | Nedovoljno istorije za prosudbu. |

```http
POST /api/quant/health
{ "returns": [...] }   // ili { "equity": [...] }
```

## Zašto je pouzdano

To je čista, deterministička domen kod (`Core.Health`) bez infrastrukturnih zavisnosti i bez eksternih
poziva — unit-testiran za decayed, degrading, healthy i too-short slučajeve i za change-point
lokalizaciju. To je manuelni pratilac always-on health checks koji backup-uju autonomne agente:
 iste statistike pokreću circuit breaker koji de-riskuje live strategiju čiji edge bledi.
