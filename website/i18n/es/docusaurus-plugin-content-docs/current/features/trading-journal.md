---
description: "Diario de trading y coach — analiza tus propias ejecuciones y backtests en busca de fugas de comportamiento (sobreconcentración, fallos repetidos, sesgo perdedor) y te coach sobre la estrategia que ya tienes. Determininista, con narrativa de IA opcional."
---

# Diario de trading y coach

La categoría más nueva y genuinamente útil de IA para trading no es predecir el mercado — es analizar *tu propio* comportamiento. El diario de trading convierte tu historial de ejecuciones y backtests en comentarios honestos para que puedas mejorar la estrategia que ya tienes.

Abre **AI → Trading Journal** (`/journal`).

## Qué revela

Desde tus instancias (ejecuciones y backtests) calcula, de forma determinista:

- **Recuentos de ganancia / pérdida / fallo y tasa de acierto** en todos tus backtests;
- **Perspectivas de comportamiento** — las fugas que silenciosamente cuestan dinero a los traders retail:
  - **Sobreconcentración** — la mayor parte de tu actividad está en un solo símbolo;
  - **Fallos repetidos** — una alta proporción de ejecuciones no lograron compilar o configurarse;
  - **Sesgo perdedor** — más backtests perdedores que ganadores (con una sugerencia de ejecutar el Laboratorio de Integridad y verificar que el edge es real);
  - un certificado de buena salud cuando no aplica ninguno de los anteriores.

```http
GET /api/journal
```

## Por qué es fiable

El análisis de comportamiento es código de dominio puro y determinista (`Core.Journal`) sin dependencia de infraestructura — unit-test para sobreconcentración, fallos repetidos, sesgo perdedor, el caso equilibrado y la cuenta vacía. Los datos van primero; el coach de IA (Portfolio Digest) es una capa narrativa opcional encima, condicionada a la clave de API de Anthropic, por lo que el diario funciona completamente sin IA configurada.
