# Economic calendar

cMind envía su **propio** calendario económico — cronograma de lanzamiento, valores reales, pronósticos, revisiones y un modelo de impacto impulsado por datos — obtenido de **autoridades primarias** (bancos centrales y agencias estadísticas nacionales), con **cero dependencia** de ForexFactory, FXStreet, Investing.com o cualquier agregador. Es correcto punto-en-tiempo, mantiene ≥10 años de historial, y está cableado en operaciones, la API pública, MCP, cBots, IA, alertas y backtests. Es un módulo desacoplado: puede deshabilitarse sin efecto en el núcleo de operaciones.

> **Estado.** El núcleo del dominio (modelo de impacto, mapeo país→símbolo, política de ventana de noticias, cadenas de revisión punto-en-tiempo, gating de dos capas) **y** persistencia (el esquema `calendar` Postgres, el lado de lectura/escritura append-only, el conector FRED y el worker de ingesta gated por config) están implementados y probados (unit + Testcontainers integration). La API REST JWT, las herramientas MCP y la UI desembarcan en las fases de lanzamiento posteriores descritas abajo.

## Qué la hace diferente

Los quejos recurrentes contra los calendarios líderes se convirtieron en nuestras restricciones de diseño:

- **Sin cambios de clasificación de impacto silenciosos.** Nuestra clasificación de impacto es **determinista, versionada y auditable**. Cada cambio es una revisión registrada con marca de tiempo — nunca una sobrescritura silenciosa. Un usuario puede ver exactamente *por qué* un evento es Alto.
- **Un ancla UTC por evento.** Cada evento está anclado a un instante UTC único del cronograma oficial de la fuente primaria; la zona horaria propia de la fuente se almacena, y la representación por usuario usa una zona horaria IANA explícita con DST manejada por la base de datos de zonas — nunca una alternancia ±1h manual.
- **Cadenas de revisión completas, en todas partes.** El valor original y cada revisión son de primera clase, expuestos idénticamente a través de las superficies de API, MCP y cBot.
- **≥10 años de historial, sin pared.** Exploración de rango sin restricciones; sin límite de 60 días, sin puerta de registro.
- **Punto-en-tiempo por construcción.** Cada hecho lleva `KnownAt` (cuándo *lo supimos*) y `EffectiveAt` (el instante del evento). "Como se veía el calendario en el tiempo T" es una consulta de primera clase, por lo que una regla de noticias backtested se comporta exactamente como en vivo — sin look-ahead de usar valores revisados en historial.

## El modelo de impacto

La puntuación de impacto es una función pura y determinista en `[0, 100]`, agrupada a Bajo / Medio / Alto / Crítico. Sus entradas son solo datos conocidos en tiempo de puntuación (sin fuga futura):

- **Serie anterior** — un peso de línea base por clase de indicador (una decisión de tasa supera IPC, que supera una encuesta menor).
- **Huella de volatilidad realizada** — el rendimiento absoluto mediano de los símbolos primarios afectados en la ventana después de los lanzamientos *pasados* de esta serie: "este lanzamiento históricamente mueve precio esta cantidad."
- **Sensibilidad de sorpresa** — cuán fuertemente la sorpresa absoluta (una puntuación z) ha correlacionado históricamente con el movimiento post-lanzamiento.

La puntuación mezcla estos con pesos fijos y marca una `ImpactModelVersion`. Recomputar es una operación explícita y registrada que produce una **nueva revisión** — nunca una mutación — por lo que la puntuación siempre es reproducible desde sus entradas.

## Mapeo país → divisa → símbolo

El papercut de integración de algo más citado se resuelve una vez, como una función pura: un país mapea a su divisa (cada miembro de la eurozona se abanica a EUR), y una divisa mapea a los símbolos de lista de vigilancia que la citan en cualquiera de las piernas. Así que **EURUSD se ve afectado por eventos de EU y US**; XAUUSD está expuesto a USD; US500 mapea a USD. Esto impulsa el filtro de noticias, la resolución de símbolos afectados y la matemática de blackout.

## Política de ventana de noticias

Una `NewsWindowRule` es `{ minImpact, beforeMinutes, afterMinutes, currencies?, series? }`. Una implementación única, compartida y pura responde "¿está el instante T dentro de un blackout para símbolo S?" — utilizado por el filtro de noticias de cBot, la pausa de copia de operaciones y la guardia de riesgo de IA, por lo que nunca pueden divergir. En incertidumbre, la respuesta de blackout por defecto se configura de forma conservadora (fail-closed por defecto) para que una brecha de datos nunca habilite silenciosamente operaciones a través de un lanzamiento de alto impacto.

## Punto-en-tiempo y revisiones

Valores reales, pronósticos y puntuaciones de impacto son **append-only**. Cada evento posee una cadena ordenada de revisiones, monótonas en `KnownAt`:

- `Scheduled` — el evento fue programado inicialmente (impacto previo, sin actual).
- `Released` — el primer valor real impreso llegó.
- `Revised` — llegó un valor revisado posterior.
- `Rescheduled` — la fuente movió el instante de lanzamiento (auditable, alertable).
- `Rescored` — la puntuación de impacto se recomputó bajo una nueva versión de modelo.

Consultar `as of` un instante pasado retorna exactamente la revisión conocida entonces — la garantía que mata look-ahead en reglas de noticias backtested.

## Pronóstico / consenso

La mediana de la encuesta de economistas **no** es libremente publicada por fuentes primarias — es el valor agregado propietario de los agregadores, y no lo fabricamos. El esquema de eventos lleva un nullable `Forecast`; un despliegue puede conectar un feed de consenso licenciado a través del puerto `IForecastProvider` opcional (traer tu propia clave, desactivado por defecto). Valores previos y revisiones siempre vienen de la fuente oficial.

## Fuentes de datos

Dos capas desacopladas, todas primarias — nunca un agregador:

- **Cronograma / sincronización:** calendario de lanzamiento FRED; agencias estadísticas nacionales (BLS, BEA, Census, Eurostat, ONS, Destatis, INSEE, e-Stat, ABS, StatCan); calendarios de reunión de banco central (Fed, ECB, BoE, BoJ, RBA, BoC, SNB, RBNZ).
- **Valores reales:** FRED (con fechas de vintage para revisiones y punto-en-tiempo), más APIs de BLS, BEA, Census, ECB SDW, Eurostat y OECD SDMX.

Una fuente muerta degrada cobertura para **esa fuente solo**; el calendario continúa sirviendo todo lo demás y superficializa la brecha como una métrica de frescura.

## Limitación de velocidad & el plan de respaldo
