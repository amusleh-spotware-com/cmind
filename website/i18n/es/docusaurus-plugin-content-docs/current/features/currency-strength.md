---
description: "Strength ranking macro de IA + outlook direccional forward — ranking determinista de fortaleza fundamental por moneda, matrix de outlook de pares con bias direccional y convicción."
---

# Strength macro de IA y outlook forward de divisas

cMind incorpora un motor macro de fortaleza de divisas **asistido por IA, matemáticamente determinista**. Clasifica un universo configurable de divisas — las 8 principales más monedas de mercados emergentes y exóticas — por fortaleza fundamental **actual**, y proyecta un **outlook direccional forward** para cada par sobre un horizonte elegido (1M / 3M / 6M / 12M). Cada rank, cada bias de par y cada número se calculan mediante matemática determinista pura en el núcleo del dominio; el LLM solo *recolecta* los insumos forward-looking que los datos no pueden publicar y *explica* el resultado en inglés llano. Nunca inventa un rank, una dirección o un número.

> **Limitación honesta.** Los fundamentos predicen bien el valor de medio a largo plazo y mal el valor a corto plazo. Trátalo como un filtro de posicionamiento / confluencia, **no** como una señal de temporización a corto plazo. Las lecturas cerca de publicaciones de alto impacto (NFP/CPI/bancos centrales) son ruidosas. No es consejo financiero.

## Cómo funciona

1. **Los fundamentos actuales vienen del Calendario Económico, no del LLM.** Los números duros — tasas de política, CPI vs objetivo, PIB, empleo, balanza comercial — y sus **z-scores de sorpresa** se obtienen **punto-en-tiempo** del módulo del [calendario económico](./economic-calendar.md) (FRED/BLS/BEA/ECB y calendarios de bancos centrales). Una instantánea histórica nunca filtra look-ahead.
2. **El LLM recolecta solo lo que el calendario no puede publicar** — por divisa: la trayectoria **forward** (ruta esperada de tasas de política en pb, tendencia inflación-vs-objetivo, momento de crecimiento) y un outlook **geopolítico** (risk-on/off, aranceles, fiscal/deuda, elecciones), más cualquier figura EM/exótica actual que falte al calendario. JSON estricto, validación por tier, búsqueda web activada.
3. **El dominio calcula el ranking y la matriz forward deterministamente.** Cada impulsor se puntúa como un **z-score dentro del tier** (para que una inflación del 50% en un activo exótico nunca distorsione a los principales), winsorizado, suma ponderada en un compuesto, y clasificado de más fuerte → más débil con un tie-break ISO estable. La capa forward lleva cada compuesto a lo largo de su trayectoria —
   `projected = current + horizonScale · Σ trajectoryDriver·weight` — y mapea el diferencial proyectado de cada par a un **bias direccional** (▲ apreciar / ▬ neutral / ▼ depreciar) con una convicción.
4. **El LLM explica** el ranking y las principales llamadas de pares en lenguaje claro.

## Los impulsores

| Impulsor | Efecto en fortaleza | Notas |
|---|---|---|
| Tasa de política y trayectoria | Mayor / hawkish ⇒ más fuerte | Mayor peso; la divergencia de bancos centrales impulsa las brechas más grandes. |
| Inflación (CPI vs objetivo) | Sobre el objetivo ⇒ más débil | Puntaje inverso (arrastre de poder adquisitivo). |
| Crecimiento del PIB | Mayor crecimiento relativo ⇒ más fuerte | Diferencial vs el panel. |
| Empleo | Mercado laboral más fuerte ⇒ más fuerte | Alimenta la ruta de política. |
| Balanza comercial / cuenta corriente | Superávit ⇒ más fuerte | Demanda estructural. |
| Postura de política | Hawkish ⇒ más fuerte | El impulsor primario de largo plazo. |
| Momento de sorpresa | Sorpresas recientes ⇒ más fuerte | De los z-scores de sorpresa del calendario. |
| Geopolítico / riesgo | Risk-off ⇒ monedas refugio (USD/JPY/CHF) más fuertes | Delta de riesgo forward acotado. |
| Rendimiento real / carry *(EM/exótico)* | Tasa real positiva ⇒ más fuerte | Impulsor dominante de EM en regímenes tranquilos. |
| Vulnerabilidad externa *(EM/exótico)* | Déficits / bajas reservas / deuda USD ⇒ más débil | Presión depreciatoria estructural. |
| Términos de intercambio *(exportadores de commodities)* | Precios de exportación crecientes ⇒ más fuerte | BRL, ZAR, CLP, NOK, AUD, CAD. |
| Riesgo político / institucional *(EM/exótico)* | Inestabilidad ⇒ más débil | Banda muerta más amplia, convicción acotada. |

## Universo por tiers (mayores + EM + exóticos)

El universo es **configurable por despliegue** (`App:CurrencyStrength:Universe`) — agregar una divisa es config, no código. Cada divisa lleva un **tier** (`Major` / `EmergingMarket` / `Exotic`) que ajusta la ponderación, el ancho de la banda muerta y el tope de convicción:

- **Mayores** — USD, EUR, GBP, JPY, AUD, NZD, CAD, CHF (liderados a nivel de tasas).
- **Mercados emergentes** — CNH, INR, BRL, MXN, ZAR, KRW, SGD, PLN (+ escandinavos NOK/SEK); carry + riesgo + vulnerabilidad externa ponderados al alza, confianza media.
- **Exóticos** — TRY, HUF, CZK, más USD-pegged HKD/SAR; baja confianza, banda muerta más amplia, convicción acotada. Las divisas **pegged / fuertemente gestionadas** (HKD, SAR, CNH) se marcan, su trayectoria se reduce, y su outlook de par se clamp hacia `Neutral` para que un peg nunca se lea como una señal de libre flotación.

Dado que las estadísticas oficiales de EM/exóticos son de menor frecuencia, revisadas y a veces opacas, las figuras recolectadas por IA llevan una **confianza por tier** mostrada como insignia de fiabilidad.

## Degradación elegante

| Calendario | IA | Resultado |
|---|---|---|
| ✅ | ✅ | Ranking completo + proyección forward + narrativa (`CalendarAndAi`). |
| ✅ | ❌ | Ranking actual solo del calendario, sin proyección forward (`CalendarOnly`). |
| ❌ | ✅ | Figuras actuales recolectadas por IA + forward, menor confianza (`AiOnly`). |
| ❌ | ❌ | Sin instantánea — el widget se oculta y la página muestra un estado vacío. |

La app corre sin cambios de cualquier manera. La IA tiene su propia puerta en la clave de IA; la rama del calendario respeta su propia puerta white-label + toggle de runtime.

## Cómo usarlo

- **Habilita IA** (Settings → AI) y **activa el widget** desde el diálogo de **Personalizar** de tu propio dashboard ("Currency strength" — opt-in, oculto por defecto). El widget muestra las principales divisas fuertes/débiles y la llamada de par 3M principal; enlaza a la página completa.
- **Página completa** — `/ai/currency-strength`: un selector de horizonte (1M/3M/6M/12M), un filtro por tier (All/Mayores/EM/Exóticos), el ranking actual, el forecast forward, la matriz de outlook de pares (bias + convicción, marcados pegged/low-confidence), y la narrativa de IA. Presiona **Refresh now** (owner) para regenerar. Un worker en segundo plano (`App:CurrencyStrength:RefreshEnabled`, **por defecto `true`**) actualiza en una programación para que la página esté poblada desde el primer uso; un despliegue o el owner lo desactiva (o deshabilita la IA / la característica del calendario económico, que el actualizador respeta degradando a sin instantánea).

## Acceso programático

Un modelo de lectura compartido (`ICurrencyStrengthQuery`) es accesible de tres maneras:

- **IA en la app** — inyectado directamente (in-process) en las características de IA.
- **MCP** — la herramienta `currency_strength` (params `horizon`, `tier`) para clientes/agentes de IA.
- **cBot REST** — `GET /api/market/v1/currency-strength/{latest,history,pair/{base}/{quote}}`, protegido por el **mismo** mecanismo `CalendarJwt` que la [API cBot del calendario](./calendar-cbot-api.md) con un alcance adicional **`market:read`**. Un cBot registra un cliente API con `market:read`, intercambia su id + secret por un JWT de corta vida en `POST /api/calendar/v1/token`, y llama los endpoints con un token `Bearer`. Sin segundo esquema JWT, sin segunda clave secreta — un token filtrado es de solo lectura, con alcance de mercado, de corta vida y revocable.

Ver la [API cBot del calendario](./calendar-cbot-api.md) para el flujo de tokens y un ejemplo copy-paste.
