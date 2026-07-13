---
description: "Regime Lab — etiqueta una serie de rendimientos en regímenes de volatilidad Calm / Normal / Turbulent y reporta el rendimiento por régimen, más el exponente de Hurst (persistencia de tendencia vs reversión a la media). Determinista."
---

# Regime Lab

Un solo ratio de Sharpe oculta la verdad de que la mayoría de las ventajas son condicionales: great in calm, trending markets and dead in turbulence (or the reverse). Regime Lab rompe el historial de una estrategia en regímenes de volatilidad y muestra cómo le fue en cada uno — para que sepas *cuándo* tu ventaja realmente funciona.

Abre **cBots → Regime Lab** (`/quant/regimes`).

## Qué hace

Dada una serie de rendimientos (o curva de equidad, de la más antigua a la más reciente):

- calcula una **volatilidad realizada trailing** en cada punto y divide el historial en regímenes **Calm**, **Normal** y **Turbulent** por los terciles de esa volatilidad;
- reporta **rendimiento por régimen** — observaciones, rendimiento medio, volatilidad y Sharpe — para que puedas ver dónde vive la ventaja;
- estima el **exponente de Hurst** mediante análisis de rango reescalado (R/S): por encima de ~0.55 la serie es **tendencial / persistente**, por debajo de ~0.45 es **reversora a la media**, y alrededor de 0.5 está cerca de una caminata aleatoria.

```http
POST /api/quant/regimes
{ "returns": [...], "window": 10 }   // o { "equity": [...] }
```

## Por qué es fiable

Código de dominio puro y determinista (`Core.Regimes`) sin dependencia de infraestructura y sin llamadas externas — unit-test para la separación de regímenes (calm vs turbulento) y para la dirección del Hurst (una serie anti-persistente puntúa por debajo de 0.5, una tendencia persistente puntúa por encima). La misma señal de régimen alimenta el bucle de reflexión de los agentes autónomos, para que un agente pueda inclinarse hacia los regímenes donde su ventaja es real.
