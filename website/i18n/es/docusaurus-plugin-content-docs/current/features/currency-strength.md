# AI macro currency strength & forward outlook

cMind envía un motor de fortaleza de divisa macro **asistido por IA, matemáticamente determinista**. Clasifica un universo configurable de divisas — las 8 principales más divisas de mercados emergentes y exóticas — por **fortaleza fundamental actual**, y proyecta una **perspectiva direccional prospectiva** para cada par en un horizonte elegido (1M / 3M / 6M / 12M). Cada rango, cada sesgo de par y cada número se calcula mediante pura matemática determinista en el núcleo del dominio; el LLM solo *recopila* las entradas prospectivas que los datos no pueden publicar y *explica* el resultado en inglés simple. Nunca inventa un rango, una dirección o un número.

> **Limitación honesta.** Los fundamentos predicen bien el valor a medio-largo plazo y mal el valor a corto plazo. Trata esto como un filtro de posicionamiento / confluencia, **no** una señal de sincronización a corto plazo. Las lecturas cerca de lanzamientos de alto impacto (NFP/CPI/banco central) son ruidosas. No es asesoramiento financiero.

## Cómo funciona

1. **Los fundamentos actuales vienen del Calendario Económico, no del LLM.** Los números duros — tasas de política, IPC vs objetivo, PIB, empleo, balance comercial — y sus **puntuaciones z de sorpresa** se obtienen **punto-en-tiempo** del [módulo calendario económico](./economic-calendar.md) (FRED/BLS/BEA/ECB y cronogramas de banco central). Una instantánea histórica nunca filtra look-ahead.
2. **El LLM recopila solo lo que el calendario no puede publicar** — por divisa: la **trayectoria prospectiva** (ruta esperada de tasa de política en bp, tendencia de inflación vs objetivo, impulso de crecimiento) y una **perspectiva geopolítica** (riesgo-on/off, aranceles, fiscal/deuda, elecciones), más cifras actuales de EM/exóticas que el calendario no tiene. JSON estricto, validación consciente de tier, búsqueda web activada.
3. **El dominio calcula la clasificación y la matriz prospectiva determinísticamente.** Cada impulsor se calcula como una **puntuación z dentro de tier** (por lo que una inflación exótica del 50% nunca distorsiona las principales), winsorizada, sumada ponderada en un compuesto, y clasificada más fuerte→ más débil con un desempate ISO estable. La capa prospectiva lleva cada compuesto a lo largo de su trayectoria — `proyectado = actual + escala de horizonte · Σ impulsor de trayectoria·peso` — y mapea cada diferencial de par proyectado a un **sesgo direccional** (▲ apreciar / ▬ neutral / ▼ depreciar) con una convicción.
4. **El LLM explica** la clasificación y las principales llamadas de par en lenguaje natural.

## Los impulsores

| Impulsor | Efecto en fortaleza | Notas |
|---|---|---|
| Tasa de política y trayectoria | Mayor / alcista ⇒ más fuerte | Mayor peso; la divergencia del banco central impulsa las brechas más grandes. |
| Inflación (IPC vs objetivo) | Por encima del objetivo ⇒ más débil | Puntuado inversamente (arrastre de poder adquisitivo). |
| Crecimiento del PIB | Crecimiento relativo más alto ⇒ más fuerte | Diferencial vs el panel. |
| Empleo | Trabajo más fuerte ⇒ más fuerte | Alimenta la ruta de política. |
| Balance comercial / cuenta actual | Superávit ⇒ más fuerte | Demanda estructural. |
| Postura de política | Alcista ⇒ más fuerte | El impulsor a largo plazo primario. |
| Impulso de sorpresa | Golpes recientes ⇒ más fuerte | De las puntuaciones z de sorpresa del calendario. |
| Geopolítico / riesgo | Riesgo-off ⇒ puertos seguros (USD/JPY/CHF) más fuerte | Delta de riesgo prospectivo acotado. |
| Rendimiento real / carry *(EM/exótica)* | Tasa real positiva ⇒ más fuerte | Impulsor EM dominante en regímenes tranquilos. |
| Vulnerabilidad externa *(EM/exótica)* | Déficits / bajas reservas / deuda en USD ⇒ más débil | Presión de depreciación estructural. |
| Términos de comercio *(exportadores de materias primas)* | Precios de exportación al alza ⇒ más fuerte | BRL, ZAR, CLP, NOK, AUD, CAD. |
| Riesgo político / institucional *(EM/exótica)* | Inestabilidad ⇒ más débil | Zona muerta más amplia, convicción limitada. |

## Universo en capas (principales + EM + exóticas)

El universo es **configurable en el despliegue** (`App:CurrencyStrength:Universe`) — agregar una divisa es configuración, no código. Cada divisa lleva un **tier** (`Major` / `EmergingMarket` / `Exotic`) que sintoniza ponderación, ancho de zona muerta y límite de convicción:

- **Principales** — USD, EUR, GBP, JPY, AUD, NZD, CAD, CHF (liderado por nivel de tasa).
- **Mercados emergentes** — CNH, INR, BRL, MXN, ZAR, KRW, SGD, PLN (+ Escandinavia NOK/SEK); carry + riesgo + vulnerabilidad externa ponderada arriba, confianza media.
- **Exóticas** — TRY, HUF, CZK, más HKD/SAR vinculadas a USD; baja confianza, zona muerta más amplia, convicción limitada. Las divisas **vinculadas / fuertemente gestionadas** (HKD, SAR, CNH) se marcan, su trayectoria se reduce ponderada, y su perspectiva de par se aprieta hacia `Neutral` para que una vinculación nunca se lea como una señal de libre flotación.

Porque las estadísticas oficiales de EM/exóticas son de menor frecuencia, revisadas y a veces opacas, las cifras recopiladas por IA llevan una **confianza por tier** mostrada como una insignia de confiabilidad.

## Degradación elegante

| Calendario | IA | Resultado |
|---|---|---|
| ✅ | ✅ | Clasificación completa + proyección prospectiva + narrativa (`CalendarAndAi`). |
| ✅ | ❌ | Clasificación actual solo de calendario, sin proyección prospectiva (`CalendarOnly`). |
| ❌ | ✅ | Figuras actuales recopiladas por IA + prospectivo, confianza más baja (`AiOnly`). |
| ❌ | ❌ | Sin instantánea — el widget se oculta y la página muestra un estado vacío. |

La app funciona sin cambios de cualquier forma. IA se gate en la clave de IA; la parte de calendario respeta su propia puerta de white-label + alternancia en tiempo de ejecución.

## Usándolo

- **Habilitar IA** (Configuración → IA) y **activar el widget** desde tu propio panel de control **Personalizar** diálogo ("Fortaleza de divisa" — opt-in, oculto por defecto). El widget muestra las divisas fuertes/débiles principales y la llamada de par 3M principal; se vincula a la página completa.
- **Página completa** — `/ai/currency-strength`: un selector de horizonte (1M/3M/6M/12M), un filtro de tier (Todas/Principales/EM/Exóticas), la clasificación actual, el pronóstico prospectivo, la matriz de perspectiva de par (sesgo + convicción, vinculada/baja confianza marcada), y la narrativa de IA. Presiona **Actualizar ahora** (propietario) para regenerar. Un worker de fondo (`App:CurrencyStrength:RefreshEnabled`, **por defecto `true`**) se actualiza en un cronograma para que la página se rellene fuera de la caja; un despliegue o el propietario lo desactiva (o desactiva la característica IA / calendario económico, que el actualizador honra degradando a sin instantánea).

## Acceso programático

Un modelo de lectura compartido (`ICurrencyStrengthQuery`) es alcanzable de tres formas:

- **IA en aplicación** — inyectada directamente (en proceso) en características de IA.
- **MCP** — la herramienta `currency_strength` (parámetros `horizon`, `tier`) para clientes/agentes de IA.
- **REST de cBot** — `GET /api/market/v1/currency-strength/{latest,history,pair/{base}/{quote}}`, asegurada por la **misma** maquinaria `CalendarJwt` que la [API cBot de calendario](./calendar-cbot-api.md) con un scope **`market:read`** agregado. Un cBot registra un cliente de API con `market:read`, intercambia su id + secreto por un JWT de corta vida en `POST /api/calendar/v1/token`, y llama a los endpoints con un token `Bearer`. Ningún segundo esquema JWT, ningún segundo secreto — un token filtrado es solo lectura, market-scoped, corta vida y revocable.

Ver la [API cBot de calendario](./calendar-cbot-api.md) para el flujo de token y una muestra copy-paste.
