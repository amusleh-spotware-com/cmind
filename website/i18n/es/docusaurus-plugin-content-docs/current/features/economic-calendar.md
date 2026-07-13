---
slug: economic-calendar
sidebar_label: Economic calendar
---

# Calendario económico

cMind incluye su **propio** calendario económico — programación de publicaciones, datos reales, pronósticos, revisiones y un modelo de impacto basado en datos — obtenido de **autoridades primarias** (bancos centrales y agencias estadísticas nacionales), con **cero dependencia** de ForexFactory, FXStreet, Investing.com o cualquier agregador. Es correcto en un momento dado, mantiene ≥10 años de historial y está conectado a trading, la API pública, MCP, cBots, IA, alertas y backtests. Es un módulo desacoplado: puede deshabilitarse sin ningún efecto sobre el núcleo de trading.

> **Estado.** El núcleo del dominio (modelo de impacto, mapeo país→símbolo, política de ventana de noticias, cadenas de revisión en un momento dado, compuerta de dos niveles) **y** la persistencia (el esquema Postgres `calendar`, el lado de escritura/lectura append-only, el conector FRED y el worker de ingestión condicionado por configuración) están implementados y probados (unit + integración Testcontainers). La API REST JWT, las herramientas MCP y la UI aterrizan en las fases de despliegue posteriores descritas a continuación.

## Qué lo diferencia

Las quejas recurrentes contra los principales calendarios se convirtieron en nuestras restricciones de diseño:

- **Sin cambios silenciosos en la calificación de impacto.** Nuestra calificación de impacto es **determinista, versionada y auditable**. Cada cambio es una revisión registrada con marca de tiempo — nunca una sobrescritura silenciosa. Un usuario puede ver exactamente *por qué* un evento es Alto.
- **Un ancla UTC por evento.** Cada evento está anclado a un instante UTC único desde la programación oficial de la fuente primaria; la zona horaria propia de la fuente se almacena, y la representación por usuario utiliza una zona horaria IANA explícita con DST manejado por la base de datos de zonas — nunca un interruptor manual ±1h.
- **Cadenas de revisión completas, en todas partes.** El valor original y cada revisión son de primera clase, expuestos idénticamente a través de la API, MCP y las superficies de cBot.
- **≥10 años de historial, sin muro.** Rango de navegación sin restricciones; sin límite de 60 días, sin muro de registro.
- **Punto en el tiempo por construcción.** Cada hecho lleva `KnownAt` (cuándo *nosotros* lo supimos) y `EffectiveAt` (el instante del evento). "Como se veía el calendario en el momento T" es una consulta de primera clase, así que una regla de noticias con backtest se comporta exactamente como en vivo — sin fuga de información anticipada por usar valores revisados en el historial.

## El modelo de impacto

La puntuación de impacto es una función pura y determinista en `[0, 100]`, agrupada en Low / Medium / High / Critical. Sus entradas son solo datos conocidos en el momento del cálculo (sin fuga futura):

- **Prior de la serie** — un peso base por clase de indicador (una decisión de tasa supera a CPI, que supera a una encuesta menor).
- **Huella de volatilidad realizada** — la mediana del retorno absoluto de los símbolos afectados primarios en la ventana posterior a las *anteriores* publicaciones de esta serie: "esta publicación históricamente mueve el precio esto".
- **Sensibilidad a la sorpresa** — qué tan fuertemente la sorpresa absoluta (un z-score) se ha correlacionado históricamente con el movimiento posterior a la publicación.

La puntuación combina estas con pesos fijos y sella un `ImpactModelVersion`. La recomputación es una operación explícita y registrada que produce una **nueva revisión** — nunca una mutación — para que la puntuación sea siempre reproducible desde sus entradas.

## Mapeo país → divisa → símbolo

La integramación algorítmica más citada se resuelve una vez, como una función pura: un país se mapea a su divisa (cada miembro del área del euro se ramifica en EUR), y una divisa se mapea a los símbolos de vigilancia que la cotizan en cualquiera de sus patas. Así **EURUSD se ve afectada tanto por eventos de la UE como de EE.UU.**; XAUUSD está expuesta a USD; US500 se mapea a USD. Esto impulsa el filtro de noticias, la resolución de símbolos afectados y la matemática de blackout.

## Política de ventana de noticias

Un `NewsWindowRule` es `{ minImpact, beforeMinutes, afterMinutes, currencies?, series? }`. Una única implementación compartida y pura responde "¿está el instante T dentro de un blackout para el símbolo S?" — utilizada por el filtro de noticias del cBot, la pausa de copy-trade y el guard de riesgo de IA, para que nunca puedan divergir. En incertidumbre, la respuesta de blackout se inclina por defecto al valor conservador configurado (fallo cerrado por defecto) para que una brecha de datos nunca active silenciosamente el trading durante una publicación de alto impacto.

## Punto en el tiempo y revisiones

Los datos reales, pronósticos y puntuaciones de impacto son **append-only**. Cada evento posee una cadena ordenada de revisiones, monótona en `KnownAt`:

- `Scheduled` — el evento se programó por primera vez (impacto previo, sin dato real).
- `Released` — llegó el primer dato real.
- `Revised` — llegó un valor revisado posterior.
- `Rescheduled` — la fuente movió el instante de publicación (auditable, alertable).
- `Rescored` — la puntuación de impacto se recalculó bajo una nueva versión del modelo.

Consultar "a fecha de" un instante pasado devuelve exactamente la revisión conocida entonces — la garantía que elimina la fuga de información anticipada en reglas de noticias con backtest.

## Pronóstico / consenso

La mediana de la encuesta de economistas **no** se publica libremente por las fuentes primarias — es el valor agregado propietario de los agregadores, y no la fabricamos. El esquema del evento lleva un `Forecast` anulable; un despliegue puede conectar un feed de consenso con licencia a través del puerto opcional `IForecastProvider` (trae tu propia clave, desactivado por defecto). Los valores anteriores y las revisiones siempre provienen de la fuente oficial.

## Fuentes de datos

Dos capas desacopladas, todas primarias — nunca un agregador:

- **Programación / tiempos:** Calendario de publicaciones de FRED; agencias estadísticas nacionales (BLS, BEA, Census, Eurostat, ONS, Destatis, INSEE, e-Stat, ABS, StatCan); calendarios de reuniones de bancos centrales (Fed, ECB, BoE, BoJ, RBA, BoC, SNB, RBNZ).
- **Valores reales:** FRED (con fechas de vintage para revisiones y punto en el tiempo), más BLS, BEA, Census, ECB SDW, Eurostat y OECD SDMX APIs.

Una fuente inactiva degrada la cobertura **solo para esa fuente**; el calendario sigue sirviendo todo lo demás y presenta la brecha como una métrica de frescura.

## Limitación de tasa y el plan de respaldo

Los proveedores externos publican límites de tasa (FRED permite ~120 solicitudes/minuto). El calendario está construido para **nunca superar el límite de un proveedor**, y para que ser estrangulado o cortado nunca degrade las lecturas:

- **Estrangulamiento proactivo.** Cada cliente HTTP de fuente pasa por una compuerta de tasa compartida y thread-safe que espacia las solicitudes salientes según un presupuesto configurado (`App:Calendar:FredRequestsPerMinute`, por defecto 100 — deliberadamente por debajo del límite del proveedor). Las solicitudes se encolan y controlan, nunca en ráfaga.
- **Honorar `429 Retry-After`.** Si un proveedor devuelve `429 Too Many Requests`, la compuerta retrocede toda la fuente por el enfriamiento solicitado por el servidor (o `App:Calendar:RateLimitBackoff`, por defecto 60s) antes de la siguiente llamada — sin bucle de reintento ajustado.
- **Resiliencia estándar.** Cada cliente de fuente también hereda el manejador de resiliencia de toda la app (reintento con backoff + jitter, circuit breaker, timeouts), así que las fluctuaciones transitorias se absorben y una fuente que falla persistentemente se estaciona (su cobertura se vuelve obsoleta) sin afectar a las demás.
- **El plan de respaldo — el cacheo read-through duradero.** Las lecturas **nunca** se sirven llamando a un proveedor. Una vez que se obtiene un rango, se persiste de forma append-only en Postgres y se sirve desde allí para siempre (ver §"Carga bajo demanda"). Así que incluso cuando una fuente está limitada por tasa o inactiva, el calendario sigue respondiendo desde datos cacheados, correctos en un momento dado; el tramo faltante simplemente permanece sin cobertura y se reintenta en el siguiente ciclo de ingestión. Las respuestas de blackout adicionalmente fallan al valor conservador por defecto bajo incertidumbre, para que una brecha de datos nunca active silenciosamente el trading durante una publicación.
- **Sondeo barato.** Fetch condicional (ETag / If-Modified-Since / cursores de vintage de fuente) y el "fetch de un tramo una vez, nunca más" mantienen el volumen real de solicitudes muy por debajo de cualquier límite en operación normal — la compuerta de tasa es una red de seguridad, no el camino común.

## Habilitar / deshabilitar

Dos niveles independientes, exactamente como otras funcionalidades de cMind:

- **Nivel 1 — interruptor de funcionalidad runtime** (`Feature.EconomicCalendar`) cambiado desde la UI de administración de Features; sin redeploy, surte efecto en vivo.
- **Nivel 2 — compuerta dura white-label** (`App:Branding:EnableEconomicCalendar`, por defecto `true`). Un revendedor lo establece en `false` para eliminar la funcionalidad por completo; un operador entonces no puede reactivarla.

El estado efectivo es `Branding.EnableEconomicCalendar && FeatureToggle.EconomicCalendar`. Cuando está deshabilitado, la entrada de navegación se oculta y `/economic-calendar`, `/api/calendar/**` y las herramientas MCP del calendario devuelven un limpio `404` de funcionalidad deshabilitada — nunca un `500`. El historial persistido se retiene al desactivar en runtime para que la reactivación sea instantánea.

## Fases de despliegue

- **P0 — núcleo del dominio** *(implementado)*: agregados, objetos de valor, puertos, modelo de impacto, mapeo país→símbolo, política de ventana de noticias, compuerta de dos niveles, suite unitaria completa.
- **P1 — persistencia + una fuente** *(implementado)*: esquema EF `calendar` (tablas propias, append-only, índices calientes), el lector `IEconomicCalendar` read-through con punto en el tiempo `asOf`, el servicio de escritura idempotente append-only, el conector FRED detrás de un cliente tipado resiliente, y el worker de ingestión condicionado por configuración; pruebas de integración Testcontainers (persistencia, PIT, idempotencia, blackout).
- **P2 — API REST JWT pública + UI web** *(implementado)*: la API versionada y asegurada por JWT `/api/calendar/v1` — emisión de cliente, intercambio de tokens y endpoints de lectura core (events, history, series, surprises, next, blackout, affected-symbols, health) con aplicación de alcances y compuerta de dos niveles, probada en integración. Más la página **mobile-first `/economic-calendar`** — una agenda de próximas publicaciones como tarjetas aptas para móvil con chips de impacto codificados por colores y un **diálogo de filtro MudBlazor** (divisas + impacto mínimo + un selector **From-date** para saltar a **cualquier** fecha pasada en todo el historial — sin límite de 60 días, sin muro); entrada de navegación, probada con smoke/mobile/a11y/E2E. Una **página de historial de serie por indicador** (`/economic-calendar/series/{code}`, enlazada desde cada evento) lista el historial completo de publicaciones de una serie. Los gráficos de sorpresa y el navegador de scroll infinito siguen.
- **P3 — más fuentes y calentamiento** *(iniciado)*: un **catálogo de series core** (CPI, Core CPI, NFP, desempleo, PIB, PCE, Fed funds, ventas minoristas → sus ids en FRED) se sembra automáticamente al inicio, y un **backfill proactivo one-time e idempotente** por chunks de año extrae su historial de ≥10 años para que el caso común esté caliente sin esperar a que un usuario se lo pierda. **La ingestión está activada por defecto** (`App:Calendar:IngestionEnabled`, por defecto `true`): la **fuente de calendario de bancos centrales** no necesita **ninguna clave API**, así que el calendario de decisiones FOMC / ECB / BoE se puebla desde el primer momento — el backfill sembra esas fechas de reuniones tanto en el historial reciente como en el horizonte forward, así que navegar *el mes pasado* (o cualquier ventana pasada) muestra las reuniones incluso antes de que se configure cualquier clave FRED/BLS; las series de valores se llenan una vez que se establecen sus claves. Los workers honran la compuerta de dos niveles del calendario — un despliegue white-label o el owner deshabilitando la funcionalidad del calendario económico detiene la ingestión, y `App:Calendar:IngestionEnabled=false` la desactiva explícitamente. **La frescura por fuente** también es real ahora: el worker registra el último sondeo exitoso de cada fuente, el conteo de fallos consecutivos y un indicador de circuit breaker activado (persistido en la configuración de la app, entre procesos), y el endpoint `/health` y la herramienta MCP `calendar_health` reportan un veredicto `stale` veraz por fuente. **BLS** (una segunda fuente de valores) y la **fuente de calendario de bancos centrales** (fechas de decisiones FOMC / ECB / BoE, sembradas en el historial y sincronizadas forward en una ventana de horizonte por el worker) están incluidas. Aún por venir: fuentes de valores BEA/Census/ECB-SDW/Eurostat/OECD y la pasada de reconciliación.
- **P4 — integración profunda**: **herramientas MCP** *(implementado — paridad completa con la API de lectura: `calendar_events`, `calendar_event`, `calendar_history`, `calendar_series`, `calendar_surprises`, `calendar_next`, `calendar_blackout`, `calendar_affected_symbols`, `calendar_health`, condicionadas a la funcionalidad)* y el **trigger de alerta `EconomicEvent`** *(implementado — una `AlertRule` que dispara N minutos antes de una próxima publicación en o por encima de un impacto elegido, opcionalmente limitada a divisas; evaluada por el worker de alertas existente sin IA, desduplicada por publicación; creada vía `POST /api/alerts/rules/economic-event`)*. La compuerta de blackout de noticias del prop-guard **y la pausa de copy-trade por noticias** están incluidas (§5.1 — un opt-in `App:Copy:NewsPauseEnabled`, por defecto off: una posición abierta cuyo símbolo está en un blackout de impacto Crítico se omite, camino caliente byte-idéntico cuando está desactivado). **La superposición de eventos en backtest** está incluida — `GET /api/calendar/v1/for-symbol` y la herramienta MCP `calendar_events_for_symbol` devuelven los eventos correctos en un momento dado que afectan a un símbolo en una ventana, y la **página de informe de instancia/backtest** renderiza las publicaciones de alto impacto que cayeron dentro de la ventana de backtest bajo la curva de equidad (así un autor ve qué operaciones cayeron en NFP), condicionada y localizada. Todo el plan está ahora implementado.
- **P5 — extras**: análisis de sorpresas, exportación iCal/CSV, búsqueda por palabra clave, consenso conectable.

Ver la [referencia de API REST y cBot](calendar-cbot-api.md) para la superficie de integración.