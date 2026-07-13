---
description: "Contrarian Retail Positioning — zmienia % retail traderów long w contrarian bias (fade crowd gdy lopsided), plus point-in-time signal value objects które strażnicy przed look-ahead bias."
---

# Contrarian Retail Positioning

Retail crowd to jeden z niewielu genuinely useful sentiment signals w FX — jako **contrarian**
indykator. Gdy wielka większość retail traders są long, cena historycznie tendowała do spadku,
i vice-versa. Ten tool zmienia crowd positioning w actionable read.

Otwórz **cBots → Contrarian Positioning** (`/quant/positioning`).

## Co robi

Wprowadź **% retail traderów long** (z broker's sentiment page lub feed taki jak FXSSI) i
zwraca:

- **Contrarian bias** — **Bearish** gdy ≥ 60% są long (crowd za długo), **Bullish** gdy ≤ 40% są
  long (crowd za krótko), **Neutral** w 40–60% indecision band;
- **Strength** — jak lopsided crowd jest (0 = balanced, 1 = fully one-sided), aby weight signal.

```http
POST /api/quant/positioning
{ "longPercent": 72 }
```

## Point-in-time przez konstrukcję

Pod spodem signal layer (`Core.Signals`) modele `PointInTimeSignal` które jest **stamped moment
była knowable** i odmawia být constructed bez niego. Każdy backtest lub autonomous agent które
consumes signal checks `IsKnownAt(decisionTime)` — więc future data nigdy może leak do historical
decision. Look-ahead bias to top reproducibility killer w quant finance; domain model czyni to
strukturalnie impossible.

## Dlaczego jest niezawodny

Pure, deterministyczne domain code z żadną infrastrukturą dependency — contrarian thresholds i
point-in-time guard są unit-tested, Including 40/60 boundaries i out-of-range rejection.
