---
description: "Salud de estrategia y decaimiento alfa — detección determinista de decaimiento que compara el Sharpe reciente de una estrategia con su historial anterior y ubica el mayor cambio de media (punto de cambio CUSUM), devolviendo un veredicto Healthy / Degrading / Decayed."
---

# Salud de estrategia y decaimiento alfa

Toda ventaja decae — la investigación es directa: la vida media de una estrategia cuant ha colapsado de años a meses, así que *la adaptación supera al descubrimiento*. El monitor de Salud de Estrategia te indica, a partir del historial propio de rendimiento de una estrategia, si la ventaja aún existe.

Abre **cBots → Strategy Health** (`/quant/health`).

## Qué hace

Dado una serie de rendimientos (o curva de equidad, de la más antigua a la más reciente):

- divide el historial en una mitad **anterior** y **reciente** y compara sus ratios de Sharpe;
- ejecuta un escaneo de **punto de cambio CUSUM** para localizar la observación donde la media cambió más claramente (una ruptura de régimen), reportado solo cuando la desviación es estadísticamente notable;
- devuelve un veredicto:

| Veredicto | Significado |
|---|---|
| **Healthy** | El rendimiento reciente está en línea con (o es mejor que) el registro anterior. |
| **Degrading** | El Sharpe reciente es materially más débil que el registro anterior — observa de cerca. |
| **Decayed** | La ventaja ha desaparecido efectivamente en la ventana reciente — considera pausar. |
| **Unknown** | No hay suficiente historial para juzgar. |

```http
POST /api/quant/health
{ "returns": [...] }   // o { "equity": [...] }
```

## Por qué es fiable

Es código de dominio puro y determinista (`Core.Health`) sin dependencia de infraestructura y sin llamadas externas — unit-test para los casos decaído, degradante, saludable y demasiado corto, y para la localización de puntos de cambio. Es el compañero manual de las verificaciones de salud siempre activas que respaldan a los agentes autónomos: las mismas estadísticas impulsan el disyuntor que reduce el riesgo de una estrategia viva cuya ventaja se desvanece.
