# Calendar REST & cBot API

El calendario económico se expone como una **API REST versionada, asegurada por JWT, con limitación de velocidad** — la superficie de integración estrella. Cualquier servicio externo, panel de control o cBot se integra contra ella como un producto. Tiene paridad de características con la API del Calendario de FXStreet y va más allá: `asOf` punto en el tiempo, cadenas de revisión completas, lógica de impacto determinista, análisis de sorpresas, resolución país→símbolo, y matemática de blackout que otras API de calendario no exponen.

> **Estado.** La seguridad JWT (emisión de cliente + intercambio de token), el gating, y los endpoints de lectura principales — `token`, `events`, `events/{id}`, `history`, `series`, `surprises`, `next`, `blackout`, `affected-symbols`, `health` — están **implementados y probados con integración** (auth, cumplimiento de scope, feature/white-label 404), más **`events/batch`** (multiplex acotado) y un documento **`/openapi.json`** descubible, **`ETag`/`If-None-Match` 304** en las lecturas de event/history, y **paginación de cursor de conjunto de claves** (`Link: rel="next"`), el **SSE `stream`** (push en vivo `event: release`, respaldado por polling), **webhooks firmados con HMAC** (`X-CMind-Signature: sha256=…`, registrados por propietario, entregados por un worker gated por config desde una marca de agua persistida), y el cliente tipado **enviado** (`CmindCalendarClient`). La superficie completa de la API pública está implementada.

## Seguridad — JWT

La API reutiliza la maquinaria de token HS256 existente del repositorio (el mismo patrón que usan los agentes CtraderCliNode), no un nuevo esquema:

- Un administrador de la app emite un **cliente de API de Calendario** (nombre + scopes + vencimiento). El cliente intercambia su id y secreto en `POST /api/calendar/v1/token` por un **JWT HS256 de corta vida** (`iss=cmind-calendar`, `aud=calendar-api`, `exp` ~15 min, claim `scope`). Solo el JWT corto se monta en solicitudes (`Authorization: Bearer <jwt>`).
- El secreto del cliente se almacena **encriptado** vía `ISecretProtector` — nunca en texto plano, nunca registrado.
- **Scopes** (menor privilegio): `calendar:read`, `calendar:blackout`, `calendar:surprises`, `calendar:stream`. Un token de cBot típicamente obtiene solo `read` + `blackout`.
- Validación estándar de `JwtBearer` (emisor, audiencia, tiempo de vida, clave de firma; `alg=none` rechazado; desfase de reloj apretado). Límite de cubo de tokens por cliente + limitador global; `429` con `Retry-After`. Todos los fallos de auth se auditan.
- Desactivar el cliente detiene la emisión de tokens futuros inmediatamente; la vida útil corta del JWT acota un token filtrado. El árbol completo `/api/calendar/**` devuelve `404` cuando la característica está deshabilitada.

## Convenciones

- **Ruta base y versionado:** `/api/calendar/v1/...` (versionado en URL; los cambios aditivos no lo modifican).
- **Formato:** JSON; instantes RFC 3339 UTC más una `sourceTimeZone` explícita; `tz=` opcional procesa una hora local de conveniencia sin perder el ancla UTC.
- **Paginación:** basada en cursor (`cursor`, `limit` ≤ 1000); cursor `next` en el cuerpo y un encabezado `Link`.
- **Almacenamiento en caché:** `ETag` + `If-None-Match`; rangos históricos obtienen un TTL largo, próximos uno corto.
- **Errores:** RFC 7807 `problem+json`, nunca un `500` desnudo.
- **Lecturas degradadas:** un fallo de fuente/DB devuelve `200` datos más conocidos más una señal `X-Calendar-Freshness` / `stale=true` (o solo `503 Retry-After` si realmente no se conoce nada) — el cBot decide.

## Endpoints

| Método y ruta | Propósito | Parámetros clave |
|---|---|---|
| `POST /v1/token` | Intercambiar id+secreto del cliente → JWT corto | body: `clientId`, `clientSecret` |
| `GET /v1/events` | Eventos en una ventana (próximos o históricos) | `from`,`to`,`countries`,`currencies`,`series`,`minImpact`,`category`,`q`,`asOf`,`cursor`,`limit`,`tz` |
| `GET /v1/events/{id}` | Un evento: cadena de revisión completa, sorpresa, justificación de impacto, símbolos afectados | `watchlist?`,`asOf?` |
| `GET /v1/events/{id}/revisions` | Historial de revisión ordenado | — |
| `GET /v1/history` | Extracción histórica profunda para una serie (≥10y) | `series`,`from`,`to`,`asOf`,`cursor`,`limit` |
| `GET /v1/series` | Catálogo de indicadores rastreados + cadencia + fuente | `countries`,`currencies`,`q` |
| `GET /v1/surprises` | Serie histórica de puntuación z real/pronóstico/sorpresa | `series`,`count`/`from,to` |
| `GET /v1/next` | Siguiente lanzamiento relevante para un símbolo (país→símbolo mapeado) | `symbol`,`minImpact` |
| `GET /v1/blackout` | ¿Está un símbolo dentro de una ventana de alto impacto ahora/en T | `symbol`,`at?`,`minImpact`,`before`,`after` |
| `GET /v1/affected-symbols` | Resolver un evento → símbolos en una lista de vigilancia | `eventId`,`watchlist` |
| `POST /v1/events:batch` | Multiplex de varias consultas en un viaje redondo | body: matriz de consultas |
| `GET /v1/stream` (SSE) | Push en vivo: lanzamientos/revisiones/entrada de ventana | `currencies`,`minImpact` (scope `calendar:stream`) |
| `POST /v1/webhooks` | Registrar un callback firmado con HMAC para lanzamiento/revisión/blackout | body: url, filtros, secreto |
| `GET /v1/health` | Frescura y cobertura por fuente | — |

## Blackout — el filtro de noticias del cBot

`GET /v1/blackout` retorna `{ inBlackout, event, startsAt, endsAt, stale }`. En incertidumbre, por defecto devuelve la **respuesta conservadora configurada** (fail-closed por defecto: "asumir en-blackout" para bots de riesgo-off), más una bandera `stale` — una brecha de datos nunca habilita operaciones a través de NFP. El endpoint es una lectura pura de DB/cache con un tiempo de espera del servidor duro; no hay búsqueda de origen sincrónica en la ruta caliente.

Un cliente tipado enviado (`Infrastructure.Calendar.CmindCalendarClient`) envuelve esto: apunta su `HttpClient` a la raíz de la API, llama a `GetTokenAsync(clientId, clientSecret)` una vez, luego `GetBlackoutAsync(token, symbol)` antes de cada orden — es **fail-safe por construcción** (cualquier no-éxito o error de análisis retorna `InBlackout = true, Stale = true`, por lo que una brecha de datos nunca habilita operaciones). Un cBot se pausa alrededor de noticias así:

```csharp
// Pseudocódigo para un cBot de cTrader usando WebRequest + un token cliente de Calendar API.
var jwt = CalendarApi.GetToken(clientId, clientSecret);           // POST /v1/token
var res = CalendarApi.Blackout(jwt, symbol: SymbolName,           // GET  /v1/blackout
                               minImpact: "High", before: 15, after: 15);
if (res.InBlackout || res.Stale)                                  // fail-safe: stale ⇒ tratar como blackout
    return;                                                       // saltar nuevas entradas en la ventana de noticias
// ...de lo contrario proceder a colocar la orden
```

## Punto en el tiempo para backtests

Pasa `asOf` en cualquier lectura para obtener el calendario exactamente como estaba en un instante pasado — los valores reales, pronósticos y revisiones *como eran entonces*. Porque las lecturas `asOf` son puras y cacheables, un backtest martilleando historial obtiene bytes idénticos cada vez, y una regla de noticias backtested se comporta exactamente como la en vivo (sin look-ahead de valores revisados).

## Resiliencia para llamadores de algoritmos

La API se sienta en una ruta caliente de operaciones, por lo que nunca lanza en un bot en vivo: cada ruta devuelve un `problem+json` bien formado o un cuerpo degradado tipado. Reutiliza primitivas de resiliencia de copy-trading — el manejador de resiliencia HTTP estándar en cada cliente de fuente, un interruptor de circuito de dominio por fuente, un worker de ingesta de singleton guardado por arrendamiento con reconciliación de inicio, y verificaciones de salud cableadas en `/health`. El fragmento de cliente tipado enviado viene con retry + timeout + circuit-breaker preconfigurado para que los autores de bots hereden resiliencia.

## Hermano: AI macro currency-strength (`market:read`)

La [lectura del modelo de fortaleza de divisa de AI macro](./currency-strength.md) monta la **misma** maquinaria JWT — un esquema, un secreto de firma, un limitador de velocidad — añadiendo solo un scope `market:read`. Registra un cliente de API con ese scope, intercambialo por un token exactamente como arriba, y llama:

```
GET /api/market/v1/currency-strength/latest?horizon=3M&tier=Majors
GET /api/market/v1/currency-strength/history?days=30
GET /api/market/v1/currency-strength/pair/EUR/USD?horizon=3M
```

```csharp
// obtener un token vía POST /api/calendar/v1/token como arriba, luego:
http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
var view = await http.GetFromJsonAsync<JsonElement>(
    baseUrl + "/api/market/v1/currency-strength/latest?horizon=3M");
// view.ranking[], view.forecasts[], view.pairs[] (bias/conviction), view.narrative
```

Un token sin `market:read` obtiene `403`; un token vencido/alterado obtiene `401`. Los endpoints son gated en la bandera de característica de IA y servidos bajo `/api/market/v1` para que permanezcan independientes de la puerta de característica del calendario. En dispatch de run/backtest un despliegue puede inyectar `CMIND_API_BASEURL` + un token `market:read` de corta vida para que un cBot llame de vuelta con cero registro de cliente.
