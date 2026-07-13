---
description: "Posicionamiento retail contrarian — convierte el % de traders retail que están largos en un sesgo contrarian (fader al rebaño cuando está desequilibrado), más objetos de valor de señal en un momento dado que protegen contra el sesgo de anticipación."
---

# Posicionamiento retail contrarian

La multitud retail es una de las pocas señales de sentimiento genuinamente útiles en FX — como indicador **contrarian**. Cuando la gran mayoría de traders retail están largos, históricamente el precio ha tendido a caer, y viceversa. Esta herramienta convierte el posicionamiento de la multitud en una lectura accionable.

Abre **cBots → Contrarian Positioning** (`/quant/positioning`).

## Qué hace

Ingresa el **% de traders retail que están largos** (desde la página de sentimiento de tu broker o un feed como FXSSI) y devuelve:

- **Sesgo contrarian** — **Bearish** cuando ≥ 60% están largos (multitud demasiado larga), **Bullish** cuando ≤ 40% están largos (multitud demasiado corta), **Neutral** en la banda de indecisión 40–60%;
- **Intensidad** — cuán desequilibrada está la multitud (0 = equilibrada, 1 = completamente en un lado), para ponderar la señal.

```http
POST /api/quant/positioning
{ "longPercent": 72 }
```

## Punto en el tiempo por construcción

Bajo el capó, la capa de señal (`Core.Signals`) modela un `PointInTimeSignal` que tiene **marca de tiempo del momento en que era cognoscible** y se niega a construirse sin ella. Cualquier backtest o agente autónomo que consuma una señal verifica `IsKnownAt(decisionTime)` — por lo que los datos futuros nunca pueden filtrarse en una decisión histórica. El sesgo de anticipación es el mayor asesino de reproducibilidad en finanzas cuantitativas; el modelo de dominio lo hace estructuralmente imposible.

## Por qué es fiable

Código de dominio puro y determinista sin dependencia de infraestructura — los umbrales contrarian y la guarda de punto en el tiempo son unit-test, incluyendo los límites 40/60 y el rechazo fuera de rango.
