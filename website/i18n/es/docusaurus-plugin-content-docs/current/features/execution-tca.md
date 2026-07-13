---
description: "Análisis de costos de transacción — mide la calidad de ejecución (slippage en puntos básicos y shortfall de implementación) de una orden contra su precio de llegada, el edge de ejecución compuesto del que viven los bancos. Determinista."
---

# Análisis de costos de transacción (TCA)

El alpha de ejecución es diminuto por operación y enorme a lo largo de miles — es una gran parte de cómo los bancos y los desks propietarios mantienen su ventaja. TCA mide cuán lejos llegó el precio que realmente lograste del precio cuando *decidiste* operar.

Abre **cBots → Execution Cost** (`/quant/tca`).

## Qué mide

Dado el **precio de llegada (decisión)**, el **lado**, y tus **rellenos** (precio × cantidad), reporta:

- **Precio de relleno promedio (VWAP)** — el precio ponderado por volumen que realmente obtuviste.
- **Slippage (bps)** — la deriva de llegada a VWAP en puntos básicos, **con signo para que un número positivo sea un costo** (comprar por encima de la llegada o vender por debajo) y un número negativo sea mejora de precio.
- **Shortfall de implementación** — ese costo expresado en términos de precio × cantidad: el dinero que la deriva te costó en esta orden.

```http
POST /api/quant/tca
{ "arrivalPrice": 1.1000, "side": "Buy",
  "fills": [ { "price": 1.1010, "quantity": 100 }, { "price": 1.1020, "quantity": 100 } ] }
```

## División inteligente (Almgren-Chriss)

Más allá de medir el costo, cMind puede planificar una orden grande para *minimizarlo*. **cBots → Execution Schedule** (`/quant/execution`) construye una **programación de ejecución óptima Almgren-Chriss**: dada la cantidad total, un número de slices, tu aversión al riesgo, la volatilidad y el impacto de mercado temporal, devuelve el tamaño a operar en cada slice. Mayor aversión al riesgo **carga la programación al frente** (reduciendo el riesgo de temporización); aversión al riesgo cero aplana a un TWAP **uniforme**. Los slices siempre suman el total.

```http
POST /api/quant/execution-schedule
{ "totalQuantity": 100, "slices": 5, "riskAversion": 2, "volatility": 0.02, "temporaryImpact": 0.1 }
```

## Por qué es fiable

Código de dominio puro y determinista (`Core.Execution`) sin dependencia de infraestructura y sin llamadas externas — unit-test para el signo de costo compra/venta, la mejora de precio, slippage cero, la agregación VWAP y las guardias de entrada. Esta es la mitad de medición de la calidad de ejecución; es la misma métrica de shortfall que el motor de copy usa para juzgar (y, con la división inteligente, reducir) el costo de las órdenes reflejadas.
