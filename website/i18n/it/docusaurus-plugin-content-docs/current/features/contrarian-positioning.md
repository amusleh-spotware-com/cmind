---
description: "Contrarian Retail Positioning — trasforma la % di trader retail long in un bias contrarian (fade the crowd quando è lopsided), più value object di segnale point-in-time che proteggono contro look-ahead bias."
---

# Contrarian Retail Positioning

La folla retail è uno dei pochi segnali di sentiment genuinamente utili nel FX — come indicatore
**contrarian**. Quando la grande maggioranza dei trader retail sono long, il prezzo storicamente ha
tendente a cadere, e viceversa. Questo strumento trasforma il positioning della folla in un read
actionable.

Apri **cBots → Contrarian Positioning** (`/quant/positioning`).

## Cosa fa

Inserisci la **% di trader retail long** (dalla pagina sentiment del tuo broker o un feed come
FXSSI) e restituisce:

- **Bias contrarian** — **Bearish** quando ≥ 60% sono long (folla troppo long), **Bullish** quando ≤ 40%
  sono long (folla troppo short), **Neutral** nella banda indecisione 40–60%;
- **Strength** — quanto lopsided è la folla (0 = bilanciata, 1 = completamente un-sided), per
  pesare il segnale.

```http
POST /api/quant/positioning
{ "longPercent": 72 }
```

## Point-in-time by construction

Sotto il cofano il layer di segnale (`Core.Signals`) modella un `PointInTimeSignal` che è **timbrare
con il momento in cui era conoscibile** e rifiuta di essere costruito senza di esso. Qualsiasi
backtest o agente autonomo che consuma un segnale controlla `IsKnownAt(decisionTime)` — così i dati
futuri non possono mai leakare in una decisione storica. Il look-ahead bias è il killer
top reproducibility nel quant finance; il domain model lo rende strutturalmente impossibile.

## Perché è affidabile

Codice domain puro, deterministico senza dipendenza infrastructure — le soglie contrarian e la guard
point-in-time sono unit-tested, incluse le boundary 40/60 e il rifiuto out-of-range.
