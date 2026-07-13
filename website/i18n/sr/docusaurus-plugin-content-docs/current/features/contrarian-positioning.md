---
description: "Kontra pozicioniranje retail tradera — pretvara procenat retail tradera koji su long u kontra bias (fade the crowd kada je jednosmerna), plus point-in-time signal value objects koji čuvaju od look-ahead bias-a."
---

# Kontra pozicioniranje retail tradera

Retail crowd je jedan od retkih zaista korisnih sentiment signala u FX — kao **kontra**
indikator. Kada je velika većina retail tradera long, cena je istorijski imala tendenciju da padne,
i obrnuto. Ovaj alat pretvara crowd pozicioniranje u akcioni read.

Otvorite **cBots → Contrarian Positioning** (`/quant/positioning`).

## Šta radi

Unesite **% retail tradera long** (sa stranice sentiment-a vašeg brokera ili feed-a kao što je FXSSI) i
vraća:

- **Kontra bias** — **Bearish** kada je ≥ 60% long (crowd previše long), **Bullish** kada je ≤ 40% je
  long (crowd previše short), **Neutral** u 40–60% band-u neodlučnosti;
- **Strength** — koliko je crowd jednostran (0 = balansirano, 1 = potpuno jednosmerno), da weight-uje signal.

```http
POST /api/quant/positioning
{ "longPercent": 72 }
```

## Point-in-time by construction

Ispod haube signal sloj (`Core.Signals`) modeluje `PointInTimeSignal` koji je **pečatiran sa
trenutkom kada je bio saznatljiv** i odbija da se konstruiše bez njega. Svaki backtest ili autonomni agent koji
konzumira signal proverava `IsKnownAt(decisionTime)` — tako da budući podaci nikad ne mogu da procure u istorijsku
odluku. Look-ahead bias je top reprodukcijsk killer u quant finansijama; domen model ga čini
strukturalno nemogućim.

## Zašto je pouzdano

Čist, deterministički domen kod bez infrastrukturnih zavisnosti — kontra thresholds i
point-in-time guard su unit-testirani, uključujući 40/60 granice i out-of-range odbijanje.
