---
description: "Calendario económico propio — calendario de publicaciones, datos reales, forecasts, revisiones y modelo de impacto determinista, sourced from autoridades primarias (bancos centrales y agencias estadísticas), ≥10 años de historial, sin dependencias de terceros."
---

# Calendario económico

cMind ships its **own** economic calendar — release schedule, actuals, forecasts, revisions and a
data-driven impact model — sourced from **primary authorities** (central banks and national
statistical agencies), with **zero dependency** on ForexFactory, FXStreet, Investing.com or any
aggregator. It is point-in-time correct, keeps ≥10 years of history, and is wired into trading, the
public API, MCP, cBots, AI, alerts and backtests. It is a decoupled module: it can be disabled with
zero effect on the trading core.

> **Estado.** P0–P4 están implementados y entregados. El núcleo del dominio, la persistencia (esquema EF `calendar`,
> lectura/escritura de solo adición, fuentes FRED + BLS + central-bank-schedule, worker de ingestión
> gated por config con seguimiento de frescura por fuente), la versioned JWT REST API, la UI
> `/economic-calendar` mobile-first, las herramientas MCP, la cBot JWT API, alertas de eventos de
> alto impacto, pausa de copy-trade por news-blackout, overlay de eventos de backtest, stream SSE,
> webhooks firmados con HMAC y el `CmindCalendarClient` tipado — todos están implementados y
> probados con integración. Los extras P5 (análisis de sorpresas, exportación iCal/CSV, búsqueda por
> palabra clave, consenso conectable) son los elementos restantes — ver las fases de despliegue abajo.

## Qué lo hace diferente

Las quejas recurrentes contra los principales calendarios se convirtieron en nuestras restricciones de diseño:

- **Sin cambios silenciosos en la calificación de impacto.** Nuestra calificación de impacto es **determinista, versionada y auditable**. Cada cambio es una revisión grabada con marca de tiempo — nunca una sobrescritura silenciosa. Un usuario puede ver exactamente *por qué* un evento es Alto.
- **Un ancla UTC por evento.** Cada evento se ancla a un solo instante UTC del horario oficial de la fuente primaria; el huso horario propio de la fuente se almacena, y la renderización por usuario usa un huso horario IANA explícito con DST manejado por la base de datos zonal — nunca una alternancia manual ±1h.
- **Cadenas de revisión completas, en todas partes.** El valor original y cada revisión son elementos de primera clase, expuestos de manera idéntica a través de la API, MCP y superficies cBot.
- **≥10 años de historial, sin muro.** Rango de navegación sin restricciones; sin límite de 60 días, sin puerta de registro.
- **Punto-en-tiempo por construcción.** Cada hecho lleva `KnownAt` (cuándo *nosotros* lo aprendimos) y `EffectiveAt` (el instante del evento). "Como lucía el calendario en el momento T" es una consulta de primera clase, así que una regla de noticias con backtest se comporta exactamente como en vivo — sin look-ahead del uso de valores revisados en el historial.

## El modelo de impacto

La puntuación de impacto es una función pura y determinista en `[0, 100]`, agrupada en Baja / Media / Alta / Crítica. Sus insumos son solo datos conocidos en el momento del cálculo (sin fuga futura):

- **Peso previo de la serie** — un peso base por clase de indicador (una decisión de tasas supera a CPI, que supera a una encuesta menor).
- **Huella de volatilidad realizada** — la mediana del retorno absoluto de los símbolos primarios afectados en la ventana después de las *pasadas* publicaciones de esta serie: "esta publicación históricamente mueve el precio así de mucho."
- **Sensibilidad a la sorpresa** — qué tan fuertemente la sorpresa absoluta (un z-score) se ha correlacionado históricamente con el movimiento post-publicación.

La puntuación combina estos con pesos fijos y sella una `ImpactModelVersion`. La recomputación es una operación explícita y registrada que produce una **nueva revisión** — nunca una mutación — así que la puntuación siempre es reproducible desde sus insumos.

## Country → currency → symbol mapping

El papercut de integración de algoritmo más citado se resuelve una vez, como función pura: un país se mapea a su moneda (cada miembro del área del euro se abanico en EUR), y una moneda se mapea a los símbolos de watchlist que la cotizan en cualquiera de las piernas. Así que **EURUSD se ve afectada por eventos tanto de EU como de US**; XAUUSD está expuesta a USD; US500 se mapea a USD. Esto impulsa el filtro de noticias, la resolución de símbolos afectados y la matemática del blackout.

## Política de ventana de noticias

Un `NewsWindowRule` es `{ minImpact, beforeMinutes, afterMinutes, currencies?, series? }`. Una única implementación compartida y pura responde "¿está el instante T dentro de un blackout para el símbolo S?" — usada por el filtro de noticias del cBot, la pausa de copy-trade y el guard de riesgo de IA, para que nunca puedan divergir. En incertidumbre la respuesta de blackout por defecto es el valor conservador configurado (fallo-cerrado por defecto) para que una brecha de datos nunca apruebe silenciosamente el trading a través de una publicación de alto impacto.

## Punto-en-tiempo y revisiones

Los datos reales, forecasts y puntuaciones de impacto son **solo adición**. Cada evento posee una cadena ordenada de revisiones, monótona en `KnownAt`:

- `Scheduled` — el evento se programmó por primera vez (impacto previo, sin dato real).
- `Released` — llegó el primer dato real impreso.
- `Revised` — llegó un valor revisado posterior.
- `Rescheduled` — la fuente movió el instante de la publicación (auditable, alertable).
- `Rescored` — la puntuación de impacto se recomputó bajo una nueva versión del modelo.

Consultar "a fecha de" un instante pasado devuelve exactamente la revisión conocida entonces — la garantía que elimina el look-ahead en reglas de noticias con backtest.

## Forecast / consenso

La mediana de la encuesta de economistas **no** se publica libremente por las fuentes primarias — es el valor agregado propietario de los agregadores, y no la fabricamos. El esquema del evento lleva un `Forecast` anulable; un despliegue puede cablear un feed de consenso con licencia a través del puerto opcional `IForecastProvider` (trae tu propia clave, desactivado por defecto). Los valores anteriores y las revisiones siempre vienen de la fuente oficial.

## Fuentes de datos

Dos capas desacopladas, todas primarias — nunca un agregador:

- **Horario / temporización:** Calendario de publicaciones de FRED; agencias estadísticas nacionales (BLS, BEA, Census, Eurostat, ONS, Destatis, INSEE, e-Stat, ABS, StatCan); calendarios de reuniones de bancos centrales (Fed, ECB, BoE, BoJ, RBA, BoC, SNB, RBNZ).
- **Valores reales:** FRED (con fechas de vintage para revisiones y punto-en-tiempo), más BLS, BEA, Census, ECB SDW, Eurostat y APIs SDMX de OCDE.

Una fuente muerta degrada la cobertura **solo para esa fuente**; el calendario sigue sirviendo todo lo demás y surface la brecha como una métrica de frescura.

## Rate limiting y el plan de respaldo

Los proveedores externos publican límites de tasa (FRED permite ~120 solicitudes/minuto). El calendario está construido para **nunca exceder el límite de un proveedor**, y para que ser throttleado o cortado nunca degrade las lecturas:

- **Throttling proactivo.** Cada cliente HTTP de fuente pasa por una puerta de tasa compartida, thread-safe, que espaixa las solicitudes salientes a un presupuesto configurado (`App:Calendar:FredRequestsPerMinute`, por defecto 100 — deliberadamente bajo el techo del proveedor). Las solicitudes se encolan y se pacean, nunca en ráfaga.
- **Honrar `429 Retry-After`.** Si un proveedor alguna vez devuelve `429 Too Many Requests`, la puerta retrocede toda la fuente por el enfriamiento solicitado por el servidor (o `App:Calendar:RateLimitBackoff`, por defecto 60s) antes de la siguiente llamada — sin bucle de reintento ajustado.
- **Resiliencia estándar.** Cada cliente de fuente también hereda el manejador de resiliencia de toda la app (reintento con backoff + jitter, circuit breaker, timeouts), así que los glitches transitorios se absorben y una fuente persistentemente fallida se estaciona (su cobertura se vuelve stale) sin afectar a las otras.
- **El plan de respaldo — el caché read-through duradero.** Las lecturas **nunca** se sirven llamando a un proveedor. Una vez que un rango se obtiene, se persiste de solo adición a Postgres y se sirve desde allí para siempre (ver §"Carga bajo demanda"). Así que incluso cuando una fuente está limitada o caída, el calendario sigue respondiendo desde datos cached, punto-en-tiempo correctos; el tramo faltante simplemente permanece sin cubrir y se reintenta en el próximo ciclo de ingestión. Las respuestas de blackout adicionalmente fallan al valor conservador por defecto bajo incertidumbre, así que una brecha de datos nunca aprueba el trading a través de una publicación.
- **Polling económico.** Fetch condicional (ETag / If-Modified-Since / cursores de vintage de fuente) y el caché "fetch un tramo una vez, nunca más" mantienen el volumen real de solicitudes muy por debajo de cualquier límite en operación normal — la puerta de tasa es una red de seguridad, no el camino común.

## Habilitar / deshabilitar

Dos niveles independientes, exactamente como otras características de cMind:

- **Nivel 1 — toggle de característica en runtime** (`Feature.EconomicCalendar`) cambiado desde la UI de admin de Features; sin re-despliegue, toma efecto en vivo.
- **Nivel 2 — puerta hard white-label** (`App:Branding:EnableEconomicCalendar`, por defecto `true`). Un revendedor lo establece en `false` para remover la característica enteramente; un operador entonces no puede re-habilitarlo.

El estado efectivo es `Branding.EnableEconomicCalendar && FeatureToggle.EconomicCalendar`. Cuando está desactivado, la entrada de navegación se oculta y `/economic-calendar`, `/api/calendar/**` y las herramientas MCP del calendario devuelven un 404 limpio de característica-desactivada — nunca un 500. El historial persistido se retiene en un toggle-off de runtime para que la re-activación sea instantánea.

## Fases de despliegue

- **P0 — núcleo del dominio** *(implementado)*: aggregates, value objects, ports, modelo de impacto,
  mapeo country→symbol, política de ventana de noticias, puerta de dos niveles, suite unitaria completa.
- **P1 — persistencia + una fuente** *(implementado)*: esquema EF `calendar` (sus propias tablas, solo adición,
  índices hot), el lector read-through `IEconomicCalendar` con punto-en-tiempo `asOf`, el servicio de escritura idempotente solo adición, el conector FRED detrás de un cliente typed resiliente, y el worker de ingestión gated por config; tests de integración con Testcontainers (persistencia, PIT, idempotencia, blackout).
- **P2 — API REST JWT pública + UI Web** *(implementado)*: la API versionada y secured por JWT `/api/calendar/v1`
  — emisión de cliente, exchange de token, y endpoints core de lectura (events, history, series,
  surprises, next, blackout, affected-symbols, health) con aplicación de alcance y puerta de dos niveles,
  probada con integración. Más la página **`/economic-calendar`** mobile-first, gated, completamente localizada
  (23 idiomas) — agenda de próximas publicaciones como tarjetas phone-friendly con chips de impacto con banda de color
  y un **diálogo de filtro** MudBlazor (divisas + impacto mínimo + un selector **Desde-fecha** para saltar a
  **cualquier** fecha pasada a través del historial completo — sin límite de 60 días, sin muro); entrada de nav,
  probada smoke/mobile/a11y/E2E. Una **página de historial de series por indicador** (`/economic-calendar/series/{code}`, enlazada desde cada evento) lista el historial completo de publicaciones de una serie. Los gráficos de sorpresa y el navegador de scroll infinito siguen.
- **P3 — más fuentes y warm-up** *(iniciado)*: un **catálogo de series core** (CPI, Core CPI, NFP,
  desempleo, PIB, PCE, Fed funds, ventas minoristas → sus ids de FRED) se sembra automáticamente al inicio,
  y un backfill **proactivo, idempotente, chunked por año** puxa su historial de ≥10 años para que el
  caso común esté caliente sin esperar a que un usuario pierda. **La ingestión está activada por defecto**
  (`App:Calendar:IngestionEnabled`, por defecto `true`): la **fuente de calendario de banco central** necesita **sin clave API**,
  así que el calendario de decisiones FOMC / ECB / BoE se puebla desde el primer uso — el backfill sembra esas
  fechas de reunión a través de **tanto el historial reciente como el horizonte forward**, así que navegar *el mes pasado* (o cualquier
  ventana pasada) muestra las reuniones incluso antes de que se configure cualquier clave FRED/BLS; las series de valores se llenan una
  vez que se establecen sus claves. Los workers honran la puerta de dos niveles del calendario — un despliegue white-label o
  el owner deshabilitando la característica del calendario económico detiene la ingestión, y `App:Calendar:IngestionEnabled=false`
  la desactiva explícitamente. ** Frescura por fuente** ahora también es real: el worker graba el último poll exitoso de cada fuente,
  conteo de fallos consecutivos y una bandera de circuit trip (persistida en settings de app,
  cross-process), y el endpoint `/health` + la herramienta MCP `calendar_health` reportan un veredicto `stale`
  por fuente. **BLS** (una 2ª fuente de valores) y la **fuente de calendario de banco central** (fechas de decisión FOMC / ECB /
  BoE, backfill a través del historial y sincronizadas forward en una ventana de horizonte por el worker)
  están incluidas. Aún por venir: fuentes de valores BEA/Census/ECB-SDW/Eurostat/OECD y la pasada de reconciliación.
- **P4 — integración profunda**: **Herramientas MCP** *(implementado — paridad completa con la API de lectura: `calendar_events`,
  `calendar_event`, `calendar_history`, `calendar_series`, `calendar_surprises`, `calendar_next`,
  `calendar_blackout`, `calendar_affected_symbols`, `calendar_health`, gated en la característica)* y los
  **triggers de alerta `EconomicEvent`** *(implementado — un `AlertRule` que dispara N minutos antes de una
  próxima publicación en/superior a un impacto elegido, opcionalmente estrecho a monedas; evaluado por el
  worker de alerta existente sin IA, de-duplicado por publicación; creado vía
  `POST /api/alerts/rules/economic-event`)*. La puerta de blackout de noticias del prop-guard **y la
  pausa de blackout de copy-trade** están incluidas (§5.1 — un opt-in `App:Copy:NewsPauseEnabled`, por defecto off: una fuente
  abierta cuyo símbolo está en un blackout de impacto Crítico se omite, camino hot byte-idéntico cuando está off). El
  **overlay de eventos de backtest** está incluido — `GET /api/calendar/v1/for-symbol` y la
  herramienta MCP `calendar_events_for_symbol` devuelven los eventos punto-en-tiempo-correctos que afectan a un símbolo en una
  ventana, y la **página de reporte instance/backtest** renderiza las publicaciones de alto impacto que cayeron dentro de la
  ventana de backtest debajo de la curva de capital (para que un autor vea qué operaciones cayeron en NFP), gated y
  localizada. El plan completo está ahora implementado.
- **P5 — extras**: análisis de sorpresas, exportación iCal/CSV, búsqueda por palabra clave, consenso conectable.

Ver la [referencia de API cBot & REST](calendar-cbot-api.md) para la superficie de integración.

## La fuente de datos es requerida (la característica está oculta sin una)

El calendario muestra valores real/forecast/previous solo desde una fuente de valores configurada (FRED o
BLS). Sin `App:Calendar:FredApiKey` o `App:Calendar:BlsApiKey` la característica está **oculta** de
la navegación; si se fuerza-habilita (white-label/owner) sin una clave, la página muestra un aviso
accionable "configura una fuente de datos" en lugar de valores vacíos, y la acción de filtro permanece oculta hasta que se
establece una fuente. Las filas de eventos muestran el **nombre** de la serie (del catálogo), no el código de serie sin procesar.
