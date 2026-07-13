---
description: "Trading Journal & Coach — analiza tus propias ejecuciones y backtests para fugas conductuales (sobre-concentración, fallos repetidos, sesgo perdedor) y te entrena en la estrategia que ya tienes. Determinista, con narrativa de IA opcional."
---

# Trading Journal & Coach

La categoría más nueva genuinamente útil de IA-para-operaciones no es prediciendo el mercado — es analizando *tu propio* comportamiento. El Trading Journal convierte tu historial de ejecuciones y backtests en retroalimentación honesta para que puedas mejorar la estrategia que ya tienes.

Abre **IA → Trading Journal** (`/journal`).

## Qué superficializa

Desde tus instancias (ejecuciones y backtests) lo calcula, determinísticamente:

- **Conteos ganador / perdedor / fallo y tasa de ganancia** en todos tus backtests;
- **Perspectivas conductuales** — las fugas que silenciosamente cuestan a los operadores minoristas:
  - **Sobre-concentración** — la mayoría de tu actividad está en un símbolo;
  - **Fallos repetidos** — un alto porcentaje de ejecuciones no lograron construir o configurar;
  - **Sesgo perdedor** — más backtests perdedores que ganadores (con un codazo para ejecutar el Integrity Lab y verificar que el borde es real);
  - un certificado de buena salud cuando ninguno de lo anterior aplica.

```http
GET /api/journal
```

## Por qué es confiable

El análisis conductual es código de dominio puro y determinista (`Core.Journal`) sin dependencia de infraestructura — probado por unidad para sobre-concentración, fallos repetidos, sesgo perdedor, el caso equilibrado y la cuenta vacía. Los hechos vienen primero; el entrenador de IA (Portfolio Digest) es una capa narrativa opcional en la parte superior, gated en la clave de la API Anthropic, por lo que el diario funciona completamente sin IA configurada.
