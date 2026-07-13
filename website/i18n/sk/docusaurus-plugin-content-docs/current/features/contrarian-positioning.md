---
description: "Contrarian Retail Positioning — premieňa % retailových obchodníkov long na kontrariánsky bias (fade the crowd keď je jednostranný), plus point-in-time signal value objects, ktoré chránia proti look-ahead bias."
---

# Contrarian Retail Positioning

Retailový dav je jeden z mála skutočne užitočných sentimentových signálov vo FX — ako **kontrariánsky**
indikátor. Keď je veľká väčšina retailových obchodníkov long, cena historicky mala tendenciu klesať,
a naopak. Tento nástroj premieňa poziciovanie davu na akčný read.

Otvorte **cBots → Contrarian Positioning** (`/quant/positioning`).

## Čo to robí

Zadajte **% retailových obchodníkov long** (z brokerovej sentiment stránky alebo feed ako FXSSI) a
vráti:

- **Kontrariánsky bias** — **Bearish** keď ≥ 60% sú long (dav príliš long), **Bullish** keď ≤ 40% sú
  long (dav príliš short), **Neutral** v 40–60% pásme nerozhodnosti;
- **Sila** — ako jednostranný je dav (0 = vyvážený, 1 = úplne jednostranný), na váženie signálu.

```http
POST /api/quant/positioning
{ "longPercent": 72 }
```

## Point-in-time by construction

Pod kapotou signal layer (`Core.Signals`) modeluje `PointInTimeSignal`, ktorý je **označený
momentom, kedy bol znateľný** a odmieta byť skonštruovaný bez neho. Akýkoľvek backtest alebo autonómny agent, ktorý
konzumuje signal, kontroluje `IsKnownAt(decisionTime)` — takže budúce dáta nikdy neuniknú do historického
rozhodnutia. Look-ahead bias je top reprodukovateľný zabijak v kvantitatívnych financiách; doménový model to robí
štrukturálne nemožné.

## Prečo je spoľahlivý

Čistý, deterministický doménový kód bez infraštruktúrnej závislosti — kontrariánske prahy a
point-in-time guard sú unit-testované, vrátane 40/60 hraníc a odmietnutia out-of-range.
