---
description: "Backtest Integrity Lab — estadísticas deterministas y de grado institucional (Sharpe Probabilístico y Deflacionado, t-stat) que convierten un backtest bruto en un veredicto Robusto / Frágil / Sobreajustado, corrigiendo por cuántas configuraciones probaste."
---

# Backtest Integrity Lab

Las plataformas minoristas te muestran el Sharpe de un backtest o la ganancia neta y se detienen. Las instituciones nunca confían en un backtest bruto — preguntan si el resultado sobrevive a la **corrección por sesgo de selección y la cantidad de configuraciones probadas**. El Backtest Integrity Lab trae esa verificación a cMind. Es **matemática determinista** (sin IA, sin llamadas externas), por lo que el veredicto es reproducible y cada número es explicable.

Abrelo en **cBots → Integrity** (`/quant/integrity`).

## Qué calcula

Dado una serie de rendimientos (o una curva de capital/balance) y la cantidad de conjuntos de parámetros que probaste para llegara ella, el analizador reporta:

- **Ratio de Sharpe** — por período y anualizado (raíz cuadrada del tiempo).
- **Ratio de Sharpe Probabilístico (PSR)** — la confianza de que el *Sharpe verdadero* supere el punto de referencia, teniendo en cuenta la longitud del historial, asimetría y curtosis (Bailey & López de Prado, 2012). Un registro corto o de cola gorda lo reduce.
- **Ratio de Sharpe Deflacionado (DSR)** — PSR medido contra un **punto de referencia deflacionado**: el Sharpe que esperarías del *mejor de N ensayos aleatorios* bajo la hipótesis nula (el Teorema de Estrategia Falsa). Cuantas más configuraciones probaste, más alto es el umbral — esto es lo que detecta el sobreajuste.
- **Estadístico t** del rendimiento medio. Siguiendo a Harvey, Liu & Zhu, un borde genuino debe superar **t ≥ 3.0**, no el 2.0 del libro de texto.
- **Asimetría / curtosis** de los rendimientos, que alimentan las correcciones PSR/DSR.

## El veredicto

| Veredicto | Significado | Regla |
|---|---|---|
| **Robusto** | El borde sobrevive a los ensayos que realizaste. | DSR ≥ 95% **y** PSR ≥ 95% **y** \|t\| ≥ 3.0 |
| **Frágil** | Estadísticamente vivo pero no convincentemente — no aumentes el tamaño basándote solo en esto. | entre los dos |
| **Sobreajustado** | Probablemente un artefacto del sesgo de selección, no un borde real. | DSR < 90% |

Todo resultado incluye una explicación en inglés claro para que el "por qué" nunca esté oculto.

## Probabilidad de Sobreajuste en Backtest (entre ensayos)

Alimentar un *recuento* de ensayos es bueno; alimentar la **serie real fuera de muestra de cada configuración que probaste** es mejor. Pégalos en la **cuadrícula de ensayos** opcional (una serie por línea) y cMind ejecuta **Validación Cruzada Simétricamente Combinatoria** (Bailey, Borwein, López de Prado & Zhu, 2015): divide las observaciones en grupos, y para cada forma de elegir la mitad como en muestra, selecciona la mejor configuración en muestra y verifica si ese ganador termina en la mitad **fuera de muestra**. La **Probabilidad de Sobreajuste en Backtest (PBO)** es la fracción de divisiones donde el ganador no logró generalizar. Un PBO cercano a 0 significa que la mejor configuración es genuinamente la mejor; un PBO de 0.5 o más significa que tu proceso de selección está recogiendo ruido — el veredicto se convierte en **Sobreajustado** sin importar qué tan bueno se veía el ganador.

```http
POST /api/quant/pbo
{ "trials": [[...], [...], ...] }
```

Cuando llegue el optimizador nativo de cTrader Console, cMind alimentará automáticamente su superficie de ensayos completa aquí.

## Ensayos — el número que importa

`Trials` es **cuántos conjuntos de parámetros probaste** antes de elegir este. Probar una estrategia y probar diez mil y quedarse con la mejor son cosas completamente diferentes: la segunda manufactura un Sharpe en muestra alto por casualidad. Alimentar el recuento honesto de ensayos es el punto completo — eleva la deflación y puede mover un backtest "excelente" a **Sobreajustado**. Cuando llegue el optimizador nativo de cTrader Console, cMind alimenta automáticamente el tamaño de cuadrícula real del barrido.

## Entradas

- **Rendimientos periódicos** — un número por período (ej. `0.01` = +1%). Al menos dos.
- **Curva de capital / balance** — cMind deriva los rendimientos simples consecutivos por ti.
- O ejecútalo directamente en un backtest completado: `POST /api/quant/integrity/backtest/{instanceId}` lee la curva de capital del reporte almacenado.

## API

```http
POST /api/quant/integrity
{ "returns": [0.006, 0.004, 0.006, ...], "trials": 250 }
```

Retorna el veredicto, todas las métricas y la explicación. `POST /api/quant/integrity/backtest/{id}` ejecuta el mismo análisis en un backtest completado que posees.

## Por qué es confiable

Las estadísticas son funciones puras en el núcleo del dominio (`Core.Quant`) sin dependencias de infraestructura — no pueden ser derribadas por un parpadeo de red, y están fijadas por pruebas unitarias de vector dorado contra las fórmulas publicadas. El CDF normal/inverso son aproximaciones de forma cerrada (Abramowitz-Stegun / Acklam), por lo que las mismas entradas siempre producen el mismo veredicto.
