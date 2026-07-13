---
description: "Backtest Integrity Lab — estadísticas de sobreajuste deterministas y de grado institucional (Sharpe Probabilístico y Deflacionado, t-stat) que convierten un backtest en un veredicto Robusto / Frágil / Sobreajustado, corrigiendo cuántas configuraciones probaste."
---

# Backtest Integrity Lab

Las plataformas minoristas te muestran el Sharpe o beneficio neto de un backtest y se detienen ahí. Las instituciones nunca confían en un backtest en bruto — preguntan si el resultado sobrevive a la **corrección por sesgo de selección y el número de configuraciones probadas**. El Backtest Integrity Lab trae esa verificación a cMind. Es **matemática determinista** (sin IA, sin llamadas externas), así que el veredicto es reproducible y cada número es explicable.

Abrirlo en **cBots → Integrity** (`/quant/integrity`).

## Qué calcula

Dado una serie de rendimientos (o una curva de capital/balance) y el número de conjuntos de parámetros que probaste para llegar a ella, el analizador reporta:

- **Ratio de Sharpe** — por período y anualizado (raíz cuadrada del tiempo).
- **Probabilistic Sharpe Ratio (PSR)** — la confianza de que el *verdadero* Sharpe supera al benchmark,
  considerando la longitud del historial, asimetría y curtosis (Bailey & López de Prado, 2012). Un registro corto o con colas pesadas lo reduce.
- **Deflated Sharpe Ratio (DSR)** — PSR medido contra un benchmark **deflacionado**: el Sharpe que esperarías del *mejor de N ensayos aleatorios* bajo la hipótesis nula (False Strategy Theorem). Cuantas más configuraciones probaste, más alta es la barra — esto es lo que detecta el sobreajuste.
- **t-estadístico** del rendimiento medio. Siguiendo a Harvey, Liu & Zhu, un borde genuino debería superar **t ≥ 3.0**, no el 2.0 del libro de texto.
- **Asimetría / curtosis** de los rendimientos, que alimentan las correcciones PSR/DSR.

## El veredicto

| Veredicto | Significado | Regla |
|---|---|---|
| **Robust** | El borde sobrevive las pruebas que ejecutaste. | DSR ≥ 95% **y** PSR ≥ 95% **y** \|t\| ≥ 3.0 |
| **Fragile** | Estadísticamente vivo pero no convincentemente — no aumentes el tamaño basándote solo en esto. | entre los dos |
| **Overfit** | Muy probablemente un artefacto del sesgo de selección, no un borde real. | DSR < 90% |

Cada resultado incluye una justificación en lenguaje claro para que el "por qué" nunca esté oculto.

## Probabilidad de Sobreajuste del Backtest (entre ensayos)

Alimentar un *conteo* de ensayos está bien; alimentar la **serie real fuera de muestra de cada configuración que probaste** es mejor. Pégalas en la **cuadrícula de ensayos** opcional (una serie por línea) y cMind ejecuta **Validación Cruzada Simétrica Combinatorial** (Bailey, Borwein, López de Prado & Zhu, 2015): divide las observaciones en grupos, y por cada forma de elegir la mitad como muestra dentro, elige la mejor configuración de esa muestra y verifica si ese ganador cae en la mitad inferior **fuera de muestra**. La **Probabilidad de Sobreajuste del Backtest (PBO)** es la fracción de divisiones donde el ganador没能 generalize. Un PBO cerca de 0 significa que la mejor configuración es genuinamente la mejor; un PBO de 0.5 o más significa que tu proceso de selección está eligiendo ruido — el veredicto se convierte en **Overfit** sin importar qué tan bien se veía el ganador.

```http
POST /api/quant/pbo
{ "trials": [[...], [...], ...] }
```

Cuando el optimizador nativo de cTrader Console aterrice, cMind alimentará su superficie completa de pruebas aquí automáticamente.

## Trials — el número que importa

`Trials` es **cuántos conjuntos de parámetros probaste** antes de elegir este. Probar una estrategia y probar diez mil y quedarse con la mejor son cosas totalmente diferentes: la segunda fabrica un alto Sharpe dentro de la muestra por puro azar. Alimentar el conteo honesto de ensayos es el punto central — eleva la deflación y puede mover un backtest "excelente" a **Overfit**. Cuando el optimizador nativo de cTrader Console aterrice, cMind lo alimenta con el tamaño real de la cuadrícula del sweep automáticamente.

## Insumos

- **Rendimientos periódicos** — un número por período (p. ej. `0.01` = +1%). Al menos dos.
- **Curva de capital / balance** — cMind deriva los rendimientos simples consecutivos por ti.
- O ejecútalo directamente sobre un backtest completado: `POST /api/quant/integrity/backtest/{instanceId}` lee la curva de capital del reporte almacenado.

## API

```http
POST /api/quant/integrity
{ "returns": [0.006, 0.004, 0.006, ...], "trials": 250 }
```

Devuelve el veredicto, todas las métricas y la justificación. `POST /api/quant/integrity/backtest/{id}` ejecuta el mismo análisis sobre un backtest completado que te pertenece.

## Por qué es confiable

Las estadísticas son funciones puras en el núcleo del dominio (`Core.Quant`) con cero dependencias de infraestructura — no pueden caer por un glitch de red, y están pinned por tests unitarios con vectores dorados contra las fórmulas publicadas. La normal CDF/inversa son aproximaciones de forma cerrada (Abramowitz-Stegun / Acklam), así que los mismos insumos siempre producen el mismo veredicto.
