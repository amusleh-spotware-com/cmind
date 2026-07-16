---
description: "Backtest Integrity Lab — estadísticas de sobreajuste de grado institucional deterministas (Sharpe Probabilística & Deflacionada, t-stat) que convierten un backtest crudo en un veredicto Robusto / Frágil / Sobreajustado, corrigiendo cuántas configuraciones probaste."
---

# Backtest Integrity Lab

Las plataformas minoristas te muestran el Sharpe o la ganancia neta de un backtest y ahí se detienen. Las instituciones nunca confían en un backtest crudo — preguntan si el resultado sobrevive a la **corrección por sesgo de selección y el número de configuraciones probadas**. El Backtest Integrity Lab trae esa verificación a cMind. Es **matemática determinista** (sin IA, sin llamadas externas), por lo que el veredicto es reproducible y cada número es explicable.

Ábrelo en **cBots → Integrity** (`/quant/integrity`).

## Qué calcula

Dado una serie de retornos (o una curva de capital/saldo) y el número de conjuntos de parámetros que probaste para llegar a él, el analizador reporta:

- **Ratio de Sharpe** — por período y anualizado (raíz cuadrada del tiempo).
- **Probabilistic Sharpe Ratio (PSR)** — la confianza de que el *verdadero* Sharpe supere el benchmark, contando la longitud del historial, sesgo y curtosis (Bailey & López de Prado, 2012). Un registro corto o con cola gorda lo baja.
- **Deflated Sharpe Ratio (DSR)** — PSR medido contra un **benchmark deflacionado**: el Sharpe que esperarías de lo *mejor de N ensayos aleatorios* bajo la hipótesis nula (el Teorema de Estrategia Falsa). Cuantas más configuraciones probaste, más alto es el umbral — esto es lo que atrapa el sobreajuste.
- **t-statistic** de la media de retornos. Siguiendo a Harvey, Liu & Zhu, un verdadero edge debería superar **t ≥ 3.0**, no el 2.0 del libro de texto.
- **Sesgo / curtosis** de los retornos, que alimentan las correcciones de PSR/DSR.

## El veredicto

| Veredicto | Significado | Regla |
|---|---|---|
| **Robusto** | El edge sobrevive los ensayos que ejecutaste. | DSR ≥ 95% **y** PSR ≥ 95% **y** \|t\| ≥ 3.0 |
| **Frágil** | Estadísticamente vivo pero no convincentemente — no aumentes el tamaño solo por esto. | entre los dos |
| **Sobreajustado** | Muy probablemente un artefacto del sesgo de selección, no un edge real. | DSR < 90% |

Cada resultado lleva una justificación en inglés claro para que el "por qué" nunca esté oculto.

## Probability of Backtest Overfitting (en los ensayos)

Alimentar un *conteo* de ensayos es bueno; alimentar la **serie real out-of-sample de cada configuración que probaste** es mejor. Pégalos en la **cuadrícula de ensayos** opcional (una serie por línea) y cMind ejecuta **Validación Cruzada Combinatoriamente Simétrica** (Bailey, Borwein, López de Prado & Zhu, 2015): divide las observaciones en grupos, y para cada forma de elegir la mitad como in-sample elige la mejor configuración in-sample y verifica si ese ganador cae en la mitad inferior **out-of-sample**. La **Probability of Backtest Overfitting (PBO)** es la fracción de divisiones donde el ganador falló en generalizar. Un PBO cerca de 0 significa que la mejor configuración es genuinamente la mejor; un PBO de 0.5 o más significa que tu proceso de selección está eligiendo ruido — el veredicto se convierte en **Sobreajustado** sin importar cuán bien se vea el ganador.

```http
POST /api/quant/pbo
{ "trials": [[...], [...], ...] }
```

Cuando llegue el optimizador nativo de cTrader Console, cMind alimentará su superficie de ensayos completa aquí automáticamente.

## Trials — el número que importa

`Trials` es **cuántos conjuntos de parámetros probaste** antes de elegir este. Probar una estrategia y probar diez mil y quedarse con la mejor son cosas salvajemente diferentes: lo segundo fabrica un Sharpe in-sample alto por casualidad. Alimentar el conteo honesto de ensayos es el punto completo — eleva la deflación y puede mover un backtest "excelente" a **Sobreajustado**. Cuando llegue el optimizador nativo de cTrader Console, cMind alimenta el tamaño real de la cuadrícula del barrido automáticamente.

## Inputs

- **Retornos periódicos** — un número por período (p.ej. `0.01` = +1%). Al menos dos. El campo se valida mientras escribes: cuenta los números válidos, marca cualquier token que no sea un número, y solo habilita **Analyze** una vez que al menos dos valores limpios estén presentes (la cuadrícula de ensayos habilita **Assess overfitting** una vez que dos series de cuatro o más números cada una estén listas).
- **Curva de capital / saldo** — cMind deriva los retornos simples consecutivos para ti.
- **Directo desde una ejecución de backtest — sin copiar y pegar.** Cada backtest completado expone un icono de escudo **Check backtest integrity** en la fila de lista **Backtest** y en la vista de detalle de su instancia; un clic ejecuta el Lab en la curva de capital almacenada de esa ejecución y muestra el veredicto en un diálogo. El icono está deshabilitado hasta que el backtest se haya completado y producido un informe, por lo que nunca es un control muerto. Bajo el capó esto es `POST /api/quant/integrity/backtest/{instanceId}`, que lee la curva de capital del informe almacenado.

## API

```http
POST /api/quant/integrity
{ "returns": [0.006, 0.004, 0.006, ...], "trials": 250 }
```

Devuelve el veredicto, todas las métricas y la justificación. `POST /api/quant/integrity/backtest/{id}` ejecuta el mismo análisis en un backtest completado que posees.

## Por qué es confiable

Las estadísticas son funciones puras en el núcleo del dominio (`Core.Quant`) con cero dependencias de infraestructura — no pueden ser derribadas por un parpadeo de red, y están fijadas por pruebas unitarias de vector dorado contra las fórmulas publicadas. La CDF normal/inversa son aproximaciones de forma cerrada (Abramowitz-Stegun / Acklam), por lo que las mismas entradas siempre producen el mismo veredicto.
