# Compromiso de Operadores (COT)

cMind incluye un reporte **Compromiso de Operadores** integrado — el desglose semanal de la CFTC de quién está largo y corto en el mercado de futuros estadounidenses (coberturistas comerciales, grandes especuladores, fondos), con gráficos históricos interactivos, un **índice COT** normalizado, una API REST autenticada para cBots y herramientas MCP para clientes de IA. Los datos provienen directamente de los **conjuntos de datos públicos de Socrata de la CFTC** — sin clave API, sin agregador. Como el calendario económico, es un módulo desacoplado que se puede desactivar sin efecto en el núcleo comercial.

## Lo que proporciona

- **Los tres tipos de reportes, solo futuros y futuros + opciones combinados:**
  - **Legado** — No Comercial (grandes especuladores), Comercial (coberturistas), No Reportable.
  - **Desagregado** — Productor/Comerciante, Intermediarios de Swaps, Dinero Administrado, Otros Reportables.
  - **Operadores en Futuros Financieros (TFF)** — Intermediario, Gestor de Activos, Fondos Apalancados, Otros Reportables.
- **Un catálogo de mercados curado** — Pares FX principales, oro/plata/cobre, petróleo crudo y gas natural, Bonos, índices de capital, criptos y los principales granos/productos blandos — cada uno asignado a su código de contrato CFTC estable y, cuando es inequívoco, a un símbolo comercializable (p. ej. Euro FX → `EURUSD`, Oro → `XAUUSD`).
- **El índice COT (0–100)** — dónde se sitúa la posición neta especuladora actual dentro de su rango histórico (por defecto ~3 años de retrospectiva). Las lecturas cerca de los extremos señalan posicionamiento abarrotado que a menudo precede a una reversión; el informe etiqueta un **extremo largo** (≥80) o **extremo corto** (≤20).
- **Exactitud punto en el tiempo.** Un informe semanal se mide un martes pero solo se hace público el viernes siguiente; cada lectura honra ese instante de lanzamiento, por lo que una señal de posicionamiento probada nunca ve un informe antes de ser publicado (sin adelanto).

## Uso de la página

Abra **Compromiso de Operadores** desde la navegación izquierda. Seleccione un **mercado**, un **tipo de informe** (Legado / Desagregado / Financiero) y active **Futuros + opciones** para cambiar entre solo futuros y la variante combinada. La página muestra:

- **Posicionamiento neto en el tiempo** — un gráfico de líneas interactivo de la posición neta (largo − corto) de cada categoría de operador en la ventana de historial.
- **Índice COT** — un gráfico de líneas del índice 0–100, con la lectura más reciente y su etiqueta extrema.
- **Última instantánea** — una tabla de largo / corto / neto / % de interés abierto por categoría de operador, más interés abierto total y la fecha del informe.

## Cómo fluyen los datos

Un trabajador de ingesta semanal extrae los seis conjuntos de datos de la CFTC para los mercados rastreados, actualiza el catálogo de mercados y agrega cada nuevo informe **idempotentemente** (re-ejecutar nunca duplica una instantánea). La primera ejecución rellena varios años de historial; las ejecuciones posteriores resincronizar las semanas más recientes para captar revisiones tardías. Todo se ejecuta fuera de la caja sin clave; un token de aplicación de Socrata opcional solo aumenta el límite de velocidad.

## Configuración

Todas las claves se encuentran bajo `App:Cot` (ver [alternadores de funciones](./feature-toggles.md) y [configuración del propietario de la etiqueta blanca](./white-label-owner-settings.md)):

| Clave | Predeterminado | Propósito |
|-----|---------|---------|
| `IngestionEnabled` | `true` | Si se ejecuta el trabajador de ingesta semanal. |
| `PollInterval` | `6h` | Con qué frecuencia el trabajador sondea los conjuntos de datos de la CFTC. |
| `BackfillYears` | `5` | Años de historial extraído en la primera ejecución. |
| `ReconcileLookbackWeeks` | `4` | Semanas recientes resincronizadas cada ciclo para captar revisiones. |
| `SocrataAppToken` | — | Token opcional que aumenta el límite de velocidad anónimo. |
| `CotIndexLookbackWeeks` | `156` | Informes semanales utilizados como rango de índice COT (~3 años). |

## Cierre

La visibilidad es una puerta de dos niveles, idéntica a la del calendario económico: la puerta dura de etiqueta blanca `App:Branding:EnableCot` (nivel de compilación) **y** el alternador de función de tiempo de ejecución `App:Features:Cot`. Con cualquiera desactivado, el enlace de navegación, la página, la API REST y las herramientas MCP desaparecen (la API devuelve `404`). Debido a que la fuente de datos no tiene clave, no hay puerta de clave de fuente de datos — habilitado significa visible.

## Para desarrolladores

- Dominio: `Core.Cot` — `CotMarket` y `CotReport` agregados, el objeto de valor `CotPositions`, el servicio de dominio `CotIndexCalculator`, y los puertos `ICotReports` / `ICotSource`.
- Infraestructura: `Infrastructure.Cot` — el analizador anti-corrupción `CftcSocrataSource`, la puerta de velocidad, el servicio de escritura de solo anexo, el lado de lectura y el trabajador de ingesta semanal (esquema EF `cot`).
- Acceso de cBot e IA: la [API de cBot COT](./cot-cbot-api.md) (REST, JWT `market:read`) y las herramientas MCP `CotMarkets`, `CotLatest`, `CotHistory`, `CotHealth`.
