---
description: "Kontrariánské retailové pozicování — převádí % retail traderů long na kontrariánský bias (fade dav když je jednostranný), plus point-in-time signal value objekty, které chrání proti look-ahead bias."
---

# Kontrariánské retailové pozicování

Retail dav je jeden z mála skutečně užitečných sentimentových signálů v FX — jako **kontrariánský** indikátor. Když je drtivá většina retail traderů long, cena historicky měla tendenci klesat, a naopak. Tento nástroj převádí pozicování davu na actionable čtení.

Otevřete **cBots → Kontrariánské pozicování** (`/quant/positioning`).

## Co to dělá

Zadejte **% retail traderů long** (z sentimentové stránky vašeho brokera nebo feedu jako FXSSI) a vrátí:

- **Kontrariánský bias** — **Medvědí** když ≥ 60% je long (dav příliš long), **Býčí** když ≤ 40% je long (dav příliš short), **Neutrální** v 40–60% pásmu nerozhodnosti;
- **Síla** — jak jednostranný dav je (0 = vyvážený, 1 = zcela jednostranný), pro vážení signálu.

```http
POST /api/quant/positioning
{ "longPercent": 72 }
```

## Point-in-time podle konstrukce

Pod kapotou signal vrstva (`Core.Signals`) modeluje `PointInTimeSignal`, která je **označena časovým okamžikem, kdy byla zjistitelná** a odmítá být konstruována bez něj. Jakýkoliv backtest nebo autonomní agent, který konzumuje signál, kontroluje `IsKnownAt(decisionTime)` — takže budoucí data nikdy nemohou uniknout do historického rozhodnutí. Look-ahead bias je nejlepší zabíječ reprodukovatelnosti v kvantitativních financích; doménový model to činí strukturálně nemožným.

## Proč je to spolehlivé

Čistý, deterministický doménový kód bez závislosti na infrastruktuře — kontrariánské prahy a point-in-time ochrana jsou unit testovány, včetně 40/60 hranic a odmítnutí mimo rozsah.
