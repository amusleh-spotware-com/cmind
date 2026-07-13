---
description: "Regime Lab — etiqueta una serie de rendimientos en regímenes de volatilidad Tranquilo / Normal / Turbulento e informa desempeño por régimen, más el exponente de Hurst (persistencia de tendencia vs reversión a la media). Determinista."
---

# Regime Lab

Un único ratio de Sharpe oculta la verdad de que la mayoría de bordes son condicionales: excelentes en mercados tranquilos y con tendencia y muertos en turbulencia (o al revés). El Regime Lab divide el historial de una estrategia en regímenes de volatilidad y muestra cómo se desempeñó en cada uno — para que sepas *cuándo* tu borde realmente funciona.

Abre **cBots → Regime Lab** (`/quant/regimes`).

## Qué hace

Dada una serie de rendimientos (o curva de capital, la más antigua primero), lo hace:

- calcula una **volatilidad realizada del rezago** en cada punto y divide el historial en regímenes **Tranquilo**, **Normal** y **Turbulento** por los tercios de esa volatilidad;
- reporta **desempeño por régimen** — observaciones, rendimiento promedio, volatilidad y Sharpe — para que puedas ver dónde vive el borde;
- estima el **exponente de Hurst** vía análisis rescalado-rango (R/S): por encima de ~0.55 la serie es **con tendencia / persistente**, por debajo de ~0.45 es **reversión a la media**, y alrededor de 0.5 es cercana a un paseo aleatorio.

```http
POST /api/quant/regimes
{ "returns": [...], "window": 10 }   // o { "equity": [...] }
```

## Por qué es confiable

Código de dominio puro y determinista (`Core.Regimes`) sin dependencia de infraestructura y sin llamadas externas — probado por unidad para separación de regímenes (volatilidad tranquila vs turbulenta) y para dirección de Hurst (serie anti-persistente puntúa por debajo de 0.5, una tendencia persistente puntúa arriba). La misma señal de régimen alimenta el bucle de reflexión de los agentes autónomos, por lo que un agente puede inclinarse hacia los regímenes donde su borde es real.
