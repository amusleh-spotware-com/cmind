---
description: "Posicionamiento Contrarian de Retail — convierte el % de operadores retail largos en sesgo contrarian (desvanece la multitud cuando está sesgada), más objetos de valor de señal en el tiempo que guardan contra sesgo de anticipación."
---

# Posicionamiento Contrarian de Retail

La multitud retail es una de las pocas señales de sentimiento genuinamente útiles en FX — como una indicador
**contrarian**. Cuando la gran mayoría de operadores retail están largos, el precio históricamente ha tendido a caer,
y viceversa. Esta herramienta convierte posicionamiento de multitud en una lectura accionable.

Abre **cBots → Posicionamiento Contrarian** (`/quant/positioning`).

## Qué hace

Ingresa el **% de operadores retail largos** (desde página de sentimiento de tu broker o un feed como FXSSI) y
devuelve:

- **Sesgo contrarian** — **Bajista** cuando ≥ 60% están largos (multitud demasiado larga), **Alcista** cuando ≤ 40% están
  largos (multitud demasiado corta), **Neutral** en banda de indecisión 40–60%;
- **Fortaleza** — cuán sesgada está la multitud (0 = balanceada, 1 = completamente de un lado), para pesar la señal.

```http
POST /api/quant/positioning
{ "longPercent": 72 }
```

## Punto en el tiempo por construcción

Bajo el capó la capa de señal (`Core.Signals`) modela un `PointInTimeSignal` que es **marcado con el
momento en que fue conocible** y se niega a ser construido sin él. Cualquier backtest o agente autónomo que
consume una señal verifica `IsKnownAt(decisionTime)` — por lo que datos futuros nunca pueden filtrarse en
decisión histórica. El sesgo de anticipación es el asesino superior de reproducibilidad en finanzas cuant; el modelo
de dominio lo hace estructuralmente imposible.

## Por qué es confiable

Código de dominio puro, determinístico con ninguna dependencia de infraestructura — los umbrales contrarian y la
guardia de punto en el tiempo se unit-prueban, incluyendo límites 40/60 y rechazo fuera de rango.
