---
description: "Salud Estratégica y Decaimiento Alpha — detección de decaimiento determinista que compara el Sharpe reciente de una estrategia con su registro anterior y localiza el cambio de media más grande (cambio-punto CUSUM), devolviendo un veredicto Healthy / Degrading / Decayed / Unknown."
---

# Salud Estratégica y Decaimiento Alpha

Cada ventaja decae — la investigación es clara que la vida media de una estrategia cuantitativa se ha
desplomado de años a meses, por lo que *la adaptación supera al descubrimiento*. El monitor de Salud Estratégica
te dice, del historial de retornos de una estrategia, si la ventaja aún existe.

Abre **cBots → Strategy Health** (`/quant/health`).

## Qué hace

Dado una serie de retornos (o curva de equidad, del más antiguo al más reciente), realiza:

- divide el historial en una mitad **anterior** y una **reciente** y compara sus ratios de Sharpe;
- ejecuta un escaneo de **cambio-punto CUSUM** para localizar la observación donde la media se desplazó más
  claramente (un cambio de régimen), reportado solo cuando la desviación es estadísticamente notable;
- devuelve un veredicto:

| Veredicto | Significado |
|---|---|
| **Healthy** | El desempeño reciente está en línea con (o mejor que) el registro anterior. |
| **Degrading** | El Sharpe reciente es materialmente más débil que el registro anterior — observa de cerca. |
| **Decayed** | La ventaja ha desaparecido efectivamente en la ventana reciente — considera pausar. |
| **Unknown** | No hay suficiente historial para juzgar. |

- **Directamente desde una ejecución de backtest — sin copiar y pegar.** Cada backtest completado expone
  un icono de corazón **Check strategy health** en la fila de la lista de **Backtest** y en su vista de
  detalle de instancia; un clic ejecuta el monitor en la curva de equidad almacenada de esa ejecución y
  muestra el veredicto en un diálogo. El icono está deshabilitado hasta que el backtest se ha completado
  y ha producido un informe, por lo que nunca es un control muerto. Bajo el capó esto es
  `POST /api/quant/health/backtest/{instanceId}`, que lee la curva de equidad del informe almacenado.

```http
POST /api/quant/health
{ "returns": [...] }   // or { "equity": [...] }
```

## Por qué es confiable

Es código de dominio puro y determinista (`Core.Health`) sin dependencia de infraestructura y sin
llamadas externas — probado unitariamente para los casos decayed, degrading, healthy y too-short, y para
localización de cambio-punto. Es el acompañante manual de las verificaciones de salud siempre activas que
respaldan a los agentes autónomos: las mismas estadísticas impulsan el disyuntor que reduce el riesgo de
una estrategia viva cuya ventaja se desvanece.
