---
description: "Tamaño de posición institucional para retail — objetivo de volatilidad y exposición fraccional de Kelly para una estrategia única, más asignación de paridad de riesgo de volatilidad inversa con una matriz de correlación en un libro de estrategias."
---

# Tamaño de posición y portafolio

"¿Qué tan grande debe ser esta operación?" es la pregunta que decide si una ventaja se compound o estalla. Las instituciones la responden con **objetivo de volatilidad** y el **criterio de Kelly**, y construyen un libro con **paridad de riesgo** en lugar de dólares iguales. cMind trae ambas al retail — matemática determinista sobre la serie de rendimientos de una estrategia, con una recomendación en inglés llano.

Abre **cBots → Position Sizing** (`/quant/sizing`).

## Tamaño de estrategia única

Dada la serie de rendimientos de una estrategia (o curva de equidad), una volatilidad anual objetivo, una fracción de Kelly y un tope de apalancamiento, el dimensionador reporta:

- **Volatilidad anual realizada** — la propia volatilidad de la estrategia, anualizada por la regla de raíz cuadrada del tiempo.
- **Dimensionamiento por objetivo de volatilidad** — la exposición que hace que la volatilidad realizada cumpla tu objetivo (`objetivo ÷ vol realizada`), topeada en tu límite de apalancamiento. Estrategias de menor volatilidad reciben más tamaño.
- **Kelly completo** — la fracción óptima para el crecimiento `f* = μ / σ²` (media sobre varianza de los rendimientos).
- **Kelly fraccional** — `f*` escalado por tu fracción de Kelly. Half-Kelly (0.5) es la elección segura común; Kelly completo es famoso por ser demasiado agresivo para ventajas reales e inciertas.
- **Exposición recomendada** — la **más pequeña** (más segura) entre el dimensionamiento por objetivo de volatilidad y Kelly fraccional, topeada. Una estrategia sin ventaja positiva (Kelly completo ≤ 0) se dimensiona a **cero**.

```http
POST /api/quant/sizing
{ "returns": [...], "targetVolatility": 0.10, "kellyFraction": 0.5, "leverageCap": 3 }
```

## Asignación de portafolio

Dale dos o más estrategias (series de rendimientos alineadas) y construye un libro por **paridad de riesgo de volatilidad inversa** — cada estrategia ponderada por `1 / volatilidad`, normalizada — para que el riesgo, no los dólares, se comparta equitativamente. También devuelve:

- la **matriz de correlación** entre tus estrategias (identifica las que son secretamente la misma apuesta);
- la **volatilidad proyectada del portafolio** a esos pesos, desde la covarianza muestral;
- un factor de **apalancamiento** que escala todo el libro hacia tu volatilidad objetivo (topado).

```http
POST /api/quant/portfolio
{ "strategies": [[...], [...]], "targetVolatility": 0.10, "leverageCap": 3 }
```

## Por qué es fiable

Todo es código de dominio puro y determinista (`Core.Portfolio`) sin dependencia de infraestructura y sin llamadas externas — unit-test para el escalado por objetivo de volatilidad, la fórmula de Kelly, la propiedad de igual riesgo de los pesos de volatilidad inversa, y la matriz de correlación. Asesórico por defecto: los números son una recomendación, nunca una orden automática.
