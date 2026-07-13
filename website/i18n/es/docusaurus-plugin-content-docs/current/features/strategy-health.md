---
description: "Strategy Health & Alpha Decay — detección determinista de decaimiento que compara el Sharpe reciente de una estrategia con su registro anterior y localiza el cambio medio más grande (CUSUM change-point), devolviendo un veredicto Saludable / Degradándose / Decaído."
---

# Strategy Health & Alpha Decay

Todo borde decae — la investigación es contundente de que la vida media de una estrategia quant se ha desplomado de años a meses, por lo que *la adaptación supera al descubrimiento*. El monitor de Strategy Health te dice, desde el historial de rendimientos propio de una estrategia, si el borde todavía está allí.

Abre **cBots → Strategy Health** (`/quant/health`).

## Qué hace

Dada una serie de rendimientos (o curva de capital, la más antigua primero), lo hace:

- divide el historial en una mitad **anterior** y una **reciente** y compara sus ratios de Sharpe;
- ejecuta un escaneo de **CUSUM change-point** para localizar la observación donde la media cambió más claramente (una ruptura de régimen), reportado solo cuando la desviación es estadísticamente notable;
- devuelve un veredicto:

| Veredicto | Significado |
|---|---|
| **Saludable** | El desempeño reciente está en línea con (o mejor que) el registro anterior. |
| **Degradándose** | El Sharpe reciente es materialmente más débil que el registro anterior — monitorea de cerca. |
| **Decaído** | El borde ha desaparecido efectivamente en la ventana reciente — considera pausa. |
| **Desconocido** | No hay suficiente historial para juzgar. |

```http
POST /api/quant/health
{ "returns": [...] }   // o { "equity": [...] }
```

## Por qué es confiable

Es código de dominio puro y determinista (`Core.Health`) sin dependencia de infraestructura y sin llamadas externas — probado por unidad para los casos decaído, degradándose, saludable y muy-corto y para localización de change-point. Es el compañero manual de las verificaciones de salud siempre activas que respaldan los agentes autónomos: la misma estadística impulsa el interruptor de circuito que reduce el riesgo de una estrategia en vivo cuyo borde se desvanece.
