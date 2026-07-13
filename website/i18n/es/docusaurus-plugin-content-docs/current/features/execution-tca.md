---
description: "Análisis de Costo de Transacción — mide calidad de ejecución (deslizamiento en puntos base e implementación faltante) de una orden contra su precio de llegada, el borde de ejecución compuesto en el que viven los bancos. Determinístico."
---

# Análisis de Costo de Transacción (TCA)

Alfa de ejecución es minúsculo por operación y enorme sobre miles de ellos — es una gran parte de cómo los bancos
y mesas prop mantienen su borde. TCA mide cuánto el precio que realmente lograste se desvió del
precio cuando *decidiste* operar.

Abre **cBots → Costo de Ejecución** (`/quant/tca`).

## Qué mide

Dado el **precio de llegada (decisión)**, el **lado**, y tus **rellenos** (precio × cantidad), reporta:

- **Precio de relleno promedio (VWAP)** — el precio ponderado por volumen que realmente obtuviste.
- **Deslizamiento (bps)** — la derivación de llegada a VWAP en puntos base, **firmada para que un número positivo sea un
  costo** (comprar por encima de llegada o vender por debajo de ella) y un número negativo es mejora de precio.
- **Implementación faltante** — ese costo expresado en términos de precio × cantidad: el dinero que la derivación te costó
  en esta orden.

```http
POST /api/quant/tca
{ "arrivalPrice": 1.1000, "side": "Buy",
  "fills": [ { "price": 1.1010, "quantity": 100 }, { "price": 1.1020, "quantity": 100 } ] }
```

## Corte inteligente (Almgren-Chriss)

Más allá de medir costo, cMind puede planificar una orden grande para *minimizarlo*. **cBots → Horario de Ejecución**
(`/quant/execution`) construye un **horario de ejecución óptimo Almgren-Chriss**: dado la cantidad total,
un número de cortes, tu aversión al riesgo, volatilidad e impacto de mercado temporal, devuelve el tamaño a
operar en cada corte. Mayor aversión al riesgo **carga por delante** el horario (reduciendo riesgo de tiempo); cero aversión al riesgo
aplana a un **TWAP** uniforme. Los cortes siempre suman al total.

```http
POST /api/quant/execution-schedule
{ "totalQuantity": 100, "slices": 5, "riskAversion": 2, "volatility": 0.02, "temporaryImpact": 0.1 }
```

## Por qué es confiable

Código de dominio puro, determinístico (`Core.Execution`) con ninguna dependencia de infraestructura y ningún
llamadas externas — unit-probado para el signo de costo compra/venta, mejora de precio, deslizamiento cero, agregación VWAP, y
guardas de entrada. Esta es la mitad de medida de calidad de ejecución; es la misma métrica de falta que el motor de
copia usa para juzgar (y, con corte inteligente, reducir) el costo de órdenes espejadas.
