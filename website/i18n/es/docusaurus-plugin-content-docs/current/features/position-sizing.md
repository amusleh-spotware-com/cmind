---
description: "Dimensionamiento institucional de posiciones para retail — volatilidad objetivo y exposición fraccionada de Kelly para una estrategia única, más asignación de paridad de riesgo de volatilidad inversa con una matriz de correlación en un libro de estrategias."
---

# Position Sizing & Portfolio

"¿Cuán grande debe ser esta operación?" es la pregunta que decide si un borde se compone o explota. Las instituciones la responden con **volatilidad objetivo** y el **criterio de Kelly**, y construyen un libro con **paridad de riesgo** en lugar de dólares iguales. cMind trae ambos a retail — matemática determinista en la serie de rendimientos de una estrategia, con una recomendación en inglés simple.

Abre **cBots → Position Sizing** (`/quant/sizing`).

## Dimensionamiento de estrategia única

Dada una serie de rendimientos de estrategia (o curva de capital), una volatilidad anual objetivo, una fracción de Kelly y un límite de apalancamiento, el dimensionador reporta:

- **Volatilidad anual realizada** — la propia volatilidad de la estrategia, anualizada por la regla de raíz cuadrada del tiempo.
- **Dimensionamiento de volatilidad objetivo** — la exposición que hace que la volatilidad realizada cumpla tu objetivo (`objetivo ÷ vol realizada`), limitada a tu límite de apalancamiento. Las estrategias de menor vol ganan más tamaño.
- **Kelly completo** — la fracción óptima de crecimiento `f* = μ / σ²` (media sobre varianza de los rendimientos).
- **Kelly fraccionado** — `f*` escalado por tu fracción de Kelly. Half-Kelly (0.5) es la opción segura común; Kelly completo es notoriamente demasiado agresivo para bordes reales e inciertos.
- **Exposición recomendada** — el **menor** (más seguro) de los dimensionamientos de volatilidad objetivo y Kelly fraccionado, limitado. Una estrategia sin borde positivo (Kelly completo ≤ 0) se dimensiona a **cero**.

```http
POST /api/quant/sizing
{ "returns": [...], "targetVolatility": 0.10, "kellyFraction": 0.5, "leverageCap": 3 }
```

## Asignación de cartera

Dale dos o más estrategias (series de rendimientos alineadas) y construye un libro por **paridad de riesgo de volatilidad inversa** — cada estrategia ponderada por `1 / volatility`, normalizada — por lo que el riesgo, no los dólares, se comparte equitativamente. También devuelve:

- la **matriz de correlación** en todas tus estrategias (descubre las que son secretamente la misma apuesta);
- la **volatilidad de cartera proyectada** en esos pesos, desde la covarianza de muestra;
- un factor de **apalancamiento** que escala el libro completo hacia tu volatilidad objetivo (limitada).

```http
POST /api/quant/portfolio
{ "strategies": [[...], [...]], "targetVolatility": 0.10, "leverageCap": 3 }
```

## Por qué es confiable

Todo es código de dominio puro y determinista (`Core.Portfolio`) sin dependencia de infraestructura y sin llamadas externas — probado por unidad para escalado de vol-target, fórmula de Kelly, propiedad de igual riesgo de pesos de volatilidad inversa, y matriz de correlación. Asesor por defecto: los números son una recomendación, nunca una orden automática.
