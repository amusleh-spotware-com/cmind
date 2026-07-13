---
description: "Kontrarian-Retail-Positionierung — verwandelt den % der Retail-Trader, die Long sind, in eine kontrarian-Voreingenommenheit (blende die Menge ab, wenn sie unausgeglichen ist), plus Point-in-Time-Signalwert-Objekte, die gegen Look-Ahead-Bias schützen."
---

# Kontrarian-Retail-Positionierung

Die Retail-Menge ist eines der wenigen wirklich nützlichen Sentiment-Signale in FX — als ein **kontrarian** Indikator. Wenn die große Mehrheit der Retail-Trader Long ist, ist der Preis historisch tendiert zu fallen, und umgekehrt. Dieses Tool verwandelt Crowd-Positionierung in einen umsetzbaren Read.

Öffnen Sie **cBots → Contrarian Positioning** (`/quant/positioning`).

## Was es tut

Geben Sie den **% der Retail-Trader, die Long sind** ein (von der Sentiment-Seite Ihres Brokers oder einem Feed wie FXSSI) und es gibt Ihnen zurück:

- **Kontrarian-Voreingenommenheit** — **Bearish**, wenn ≥ 60% Long sind (Menge zu Long), **Bullish**, wenn ≤ 40% Long sind (Menge zu Short), **Neutral** im 40–60%-Unentschlossenheits-Band;
- **Stärke** — wie unausgeglichen die Menge ist (0 = ausgewogen, 1 = vollständig einseitig), um das Signal zu wiegen.

```http
POST /api/quant/positioning
{ "longPercent": 72 }
```

## Point-in-Time durch Konstruktion

Unter der Haube modelliert die Signal-Schicht (`Core.Signals`) ein `PointInTimeSignal`, das **mit dem Moment gestempelt ist, in dem es bekannt war** und sich weigert, ohne es konstruiert zu werden. Jeder Backtest oder autonomer Agent, der ein Signal verbraucht, überprüft `IsKnownAt(decisionTime)` — sodass zukünftige Daten niemals in eine historische Entscheidung durchsickern können. Look-Ahead-Bias ist der Top-Reproduzierbarkeitskiller in der quantitativen Finanzierung; das Domänenmodell macht es strukturell unmöglich.

## Warum es zuverlässig ist

Reiner, deterministischer Domänen-Code ohne Infrastruktur-Abhängigkeit — die kontrarian-Schwellen und der Point-in-Time-Guard werden Unit-getestet, einschließlich der 40/60-Grenzen und Out-of-Range-Ablehnung.
