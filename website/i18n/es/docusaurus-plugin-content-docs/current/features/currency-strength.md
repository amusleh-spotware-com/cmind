---
slug: currency-strength
sidebar_label: Currency strength
---

# AI macro currency strength y perspectiva forward

cMind incluye un motor de **fuerza macroeconómica de divisas orientado por IA y matemáticamente determinista**. Clasifica un universo configurable de divisas — los 8 majors más divisas de mercados emergentes y exóticos — por fuerza fundamental **actual**, y proyecta una **perspectiva direccional forward** para cada par en un horizonte elegido (1M / 3M / 6M / 12M). Cada ranking, cada sesgo de par y cada número se calcula mediante matemática pura determinista en el núcleo del dominio; el LLM solo *recopila* los datos que no pueden publicarse y *explica* el resultado en inglés llano. Nunca inventa un ranking, una dirección ni un número.

> **Limitación honesta.** Los fundamentales predicen bien el valor a medio-largo plazo y mal el valor a corto plazo. Trátalo como un filtro de posicionamiento / confluencia, **no** como una señal de temporización a corto plazo. Las lecturas cercanas a publicaciones de alto impacto (NFP/CPI/bancos centrales) son ruidosas. No es asesoramiento financiero.

## Cómo funciona

1. **Los fundamentales actuales provienen del calendario económico, no del LLM.** Los números duros — tasas de política, CPI vs objetivo, PIB, empleo, balanza comercial — y sus **z-scores de sorpresa** se obtienen **en un momento dado** del módulo de [calendario económico](./economic-calendar.md) (FRED/BLS/BEA/ECB y calendarios de bancos centrales). Un snapshot histórico nunca filtra información anticipada.
2. **El LLM recopila solo lo que el calendario no puede publicar** — por divisa: la trayectoria **forward** (ruta esperada de tasas de política en pb, tendencia inflación vs objetivo, impulso de crecimiento) y una perspectiva **geopolítica** (risk-on/off, aranceles, fiscal/deuda, elecciones), más cualquier cifra EM/exótica actual que falte al calendario. JSON estricto, validación por nivel, búsqueda web activada.
3. **El dominio calcula el ranking y la matriz forward de forma determinista.** Cada impulsor se puntúa como un **z-score por nivel** (para que una inflación del 50% en un exótico nunca distorsione los majors), se winsoriza, se suma ponderada en un compuesto y se clasifica de más fuerte a más débil con un criterio de desempate ISO estable. La capa forward transporta cada compuesto a lo largo de su trayectoria —
   `projected = current + horizonScale · Σ trajectoryDriver·weight` — y mapea el diferencial proyectado de cada par a un **sesgo direccional** (▲ apreciar / ▬ neutral / ▼ depreciar) con una convicción.
4. **El LLM explica** el ranking y las principales llamadas de par en lenguaje llano.

## Los impulsores

| Impulsor | Efecto en la fuerza | Notas |
|---|---|---|
| Tasa de política y trayectoria | Mayor / hawkish ⇒ más fuerte | Mayor ponderación; la divergencia de bancos centrales genera los mayores diferencial. |
| Inflación (CPI vs objetivo) | Por encima del objetivo ⇒ más débil | Puntúa inversamente (arrastre de poder adquisitivo). |
| Crecimiento del PIB | Mayor crecimiento relativo ⇒ más fuerte | Diferencial frente al panel. |
| Empleo | Mercado laboral más fuerte ⇒ más fuerte | Alimenta la ruta de política. |
| Balanza comercial / cuenta corriente | Superávit ⇒ más fuerte | Demanda estructural. |
| Postura de política | Hawkish ⇒ más fuerte | El principal impulsor a largo plazo. |
| Momento de sorpresa | Sorpresas recientes ⇒ más fuerte | A partir de los z-scores de sorpresa del calendario. |
| Geopolítico / riesgo | Risk-off ⇒ paraísos seguros (USD/JPY/CHF) más fuertes | Delta de riesgo forward acotado. |
| Rendimiento real / carry *(EM/exóticos)* | Tipo real positivo ⇒ más fuerte | Impulsor EM dominante en regímenes平静. |
| Vulnerabilidad externa *(EM/exóticos)* | Déficits / reservas bajas / deuda USD ⇒ más débil | Presión depreciatoria estructural. |
| Términos de intercambio *(exportadores de commodities)* | Precios de exportación crecientes ⇒ más fuerte | BRL, ZAR, CLP, NOK, AUD, CAD. |
| Riesgo político / institucional *(EM/exóticos)* | Inestabilidad ⇒ más débil | Banda muerta más amplia, convicción limitada. |

## Universo por niveles (majors + EM + exóticos)

El universo es **configurable por despliegue** (`App:CurrencyStrength:Universe`) — añadir una divisa es configuración, no código. Cada divisa lleva un **nivel** (`Major` / `EmergingMarket` / `Exotic`) que ajusta la ponderación, el ancho de banda muerta y el tope de convicción:

- **Majors** — USD, EUR, GBP, JPY, AUD, NZD, CAD, CHF (liderados a nivel de tasas).
- **Mercados emergentes** — CNH, INR, BRL, MXN, ZAR, KRW, SGD, PLN (+ Nórdicos NOK/SEK); carry + riesgo + vulnerabilidad externa ponderados al alza, confianza media.
- **Exóticos** — TRY, HUF, CZK, más HKD/SAR atados al USD; baja confianza, banda muerta más amplia, convicción limitada. Las divisas **atadas / fuertemente gestionadas** (HKD, SAR, CNH) se marcan, su trayectoria se descuenta y su perspectiva de par se fija hacia `Neutral` para que un atado nunca se lea como una señal de libre flotación.

Como las estadísticas EM/exóticas oficiales son de menor frecuencia, revisadas y a veces opacas, las cifras recopiladas por IA llevan una **confianza por nivel** mostrada como distintivo de fiabilidad.

## Degradación elegante

| Calendario | IA | Resultado |
|---|---|---|
| ✅ | ✅ | Ranking completo + proyección forward + narrativa (`CalendarAndAi`). |
| ✅ | ❌ | Ranking actual solo calendario, sin proyección forward (`CalendarOnly`). |
| ❌ | ✅ | Cifras actuales recopiladas por IA + forward, menor confianza (`AiOnly`). |
| ❌ | ❌ | Sin snapshot — el widget se oculta y la página muestra un estado vacío. |

La aplicación funciona igual en cualquier caso. IA está condicionada a la clave de IA; la rama del calendario respeta su propia compuerta white-label + interruptor runtime.

## Uso

- **Habilitar IA** (Settings → AI) y **activar el widget** desde tu propio diálogo de personalización del dashboard ("Currency strength" — opt-in, oculto por defecto). El widget muestra las principales divisas fuertes/débiles y la llamada de par 3M principal; enlaza a la página completa.
- **Página completa** — `/ai/currency-strength`: un selector de horizonte (1M/3M/6M/12M), un filtro por nivel (All/Majors/EM/Exotics), el ranking actual, el pronóstico forward, la matriz de perspectiva de pares (sesgo + convicción, marcados los atados/baja confianza), y la narrativa de IA. Pulsa **Refresh now** (owner) para regenerar. Un worker en segundo plano (`App:CurrencyStrength:RefreshEnabled`, **por defecto `true`**) actualiza en una programación para que la página esté poblada desde el primer momento; un despliegue o el owner lo desactiva (o deshabilita la IA / la funcionalidad del calendario económico, que el actualizador respeta degradando a sin snapshot).

## Acceso programático

Un modelo de solo lectura compartido (`ICurrencyStrengthQuery`) es accesible de tres formas:

- **IA en la app** — inyectado directamente (en proceso) en las funcionalidades de IA.
- **MCP** — la herramienta `currency_strength` (parámetros `horizon`, `tier`) para clientes/agentes de IA.
- **cBot REST** — `GET /api/market/v1/currency-strength/{latest,history,pair/{base}/{quote}}`, asegurado con la **misma** maquinaria de `CalendarJwt` que la [API REST del cBot de calendario](./calendar-cbot-api.md) con un alcance adicional **`market:read`**. Un cBot registra un cliente API con `market:read`, intercambia su id + secreto por un JWT de corta duración en `POST /api/calendar/v1/token`, y llama a los endpoints con un token `Bearer`. Sin un segundo esquema JWT, sin un segundo secreto — un token filtrado es de solo lectura, con alcance de mercado, de corta duración y revocable.

Ver la [API REST del cBot de calendario](./calendar-cbot-api.md) para el flujo de tokens y un ejemplo copiable.