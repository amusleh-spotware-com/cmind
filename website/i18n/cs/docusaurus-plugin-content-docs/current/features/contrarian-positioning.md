---
description: "Contrarian Retail Positioning — změní % maloobchodních traderů long na contrarian bias (fade dav když je lopsided), plus point-in-time signal value objekty které hlídají proti look-ahead bias."
---

# Contrarian Retail Positioning

Maloobchodní dav je jeden z mála skutečně užitečných sentiment signálů v FX — jako **contrarian** indikátor. Když veliká většina maloobchodních traderů je long, cena historicky měla tendenci padat, a naopak. Tento nástroj změní crowd positioning na actionable čtení.

Otevřít **cBots → Contrarian Positioning** (`/quant/positioning`).

## Co to dělá

Zadejte **% maloobchodních traderů long** (z vašeho broker sentiment stránky nebo feed jako FXSSI) a vrátí:

- **Contrarian bias** — **Bearish** když ≥ 60% jsou long (dav příliš long), **Bullish** když ≤ 40% jsou long (dav příliš short), **Neutral** v 40–60% indecision pasu;
- **Strength** — jak lopsided dav je (0 = balanced, 1 = fully one-sided), k váze signál.

```http
POST /api/quant/positioning
{ "longPercent": 72 }
```

## Point-in-time podle konstrukce

Pod kapotou signal vrstva (`Core.Signals`) modeluje `PointInTimeSignal`, která je **razítko s okamžikem kdy byl znám** a odmítá se staví bez něj. Jakýkoliv backtest nebo autonomous agent který konzumuje signál kontroly `IsKnownAt(decisionTime)` — takže budoucí data nemůže nikdy uniknout do historické rozhodnutí. Look-ahead bias je top reproducibility zabijáka v quant financích; doménový model dělá to strukturálně nemožné.

## Proč to je spolehlivé

Čistý, deterministický doménový kód bez infra závislosti — contrarian prahů a point-in-time stráže jsou unit-testovány, včetně 40/60 hranic a out-of-range odmítnutí.
