---
description: "Suite completa de pruebas reproducibles de copy-trading. Dos capas:"
---

# Suite de pruebas de copy-trading (determinista + en vivo)

Suite completa de pruebas reproducibles de copy-trading. Dos capas:

1. **Pruebas deterministas** (xUnit, sin red) — matemática de copy + lógica del motor. Rápidas, CI, sin secretos. Cubren cada modo de gestión de dinero, cada filtro/opción, resiliencia del motor.
2. **Pruebas E2E en vivo** (cuentas demo reales de cTrader) — `CopyEngineHost` de producción placed + copying órdenes reales entre cuentas reales. Totalmente automatizadas, re-ejecutables como prueba unitaria: leen credenciales cacheadas de archivos locales gitignored, auto-refrescan el access token, skip limpido cuando no hay secretos (CI se mantiene verde).

Nunca se ejecuta contra cuenta financiada en vivo — cada cuenta **demo**, cada prueba en vivo cierra las posiciones que abrió.

## Estructura

```
tests/UnitTests/CopyTrading/
  CopySizingCalculatorTests.cs   — cada modo de sizing + redondeo + lot min/max
  CopyDecisionEngineTests.cs     — dirección/reverse/slippage/delay/filtro símbolo/tamaño cero
  CopyEngineHostTests.cs         — lógica de copy del host contra una sesión fake en memoria
  FakeTradingSession.cs          — IOpenApiTradingSession determinista (registra órdenes/cierres/enmiendas)
  OpenApiConnectionTests.cs      — conectar / reconectar / backoff / falla fatal (resiliencia)

tests/IntegrationTests/CopyLive/
  LiveCopySecrets.cs             — carga los secretos gitignored, guarda tokens refrescados
  LiveTokenBootstrapTests.cs     — one-shot: descifra tokens del DB de la app al cache de tokens
  LiveCopyFixture.cs             — rota el access token, expone la lista de cuentas demo
  LiveCopyScenario.cs            — ejecuta un escenario real de copy de principio a fin (open → copy → verify → cleanup)
  CopyTradingLiveTests.cs        — los escenarios en vivo (1:1, 1:many, reverse, …)
```

## Secretos (locales, gitignored — nunca comiteados)

Todas las credenciales bajo `<repo>/secrets/` (ya en `.gitignore`). El dev escribe **solo los primeros dos archivos**; el tercero (tokens) se produce automáticamente mediante onboarding.

`secrets/openapi-test-app.local.json` — Open API app:

```json
{ "ClientId": "2175_…", "ClientSecret": "…" }
```

`secrets/openapi-cids.local.json` — credenciales de login cID para autorizar (una o muchas):

```json
{ "Cids": [
  { "Cid": "amusleh",  "Username": "amusleh",  "Password": "…" },
  { "Cid": "afhacker", "Username": "afhacker", "Password": "…" }
] }
```

`secrets/openapi-tokens.local.json` — **escrito por onboarding**, multi-cID, refrescado cada ejecución:

```json
{ "Cids": [
  { "Cid": "amusleh", "RefreshToken": "…", "AccessToken": "…", "IsLive": false,
    "Accounts": [ { "CtidTraderAccountId": 25172589, "TraderLogin": 3635817, "IsLive": false }, … ] }
] }
```

El refresh token **nunca expira**, así que después del onboarding one-time las pruebas en vivo funcionan indefinidamente: cada ejecución intercambia el refresh token de cada cID por access token fresco (rotación) — sin navegador, sin prompts.

## Onboarding one-time (totalmente automatizado — sin interacción del dev más allá de guardar credenciales)

El onboarding drivea login real de cTrader ID en navegador headless desde credenciales cID guardadas, captura el callback OAuth en el listener HTTPS local en el redirect registrado de la app (`https://localhost:7080/openapi/callback`), intercambia código por tokens, carga la lista de cuentas, escribe el cache de tokens multi-cID. Se ejecuta una vez por máquina (o al añadir cID):

```bash
CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests
```

Autoriza cada cID en `openapi-cids.local.json`, escribe `openapi-tokens.local.json`. Después de eso las pruebas de copy en vivo no necesitan nada más. (La cuenta cTrader ID del cID debe tener 2FA/captcha desactivados en el login para que la automatización se complete.)

**Bootstrap alternativo** (si las cuentas ya están autorizadas en la app en ejecución): descifra los tokens almacenados directamente desde el volumen Postgres de la app en lugar de re-autorizar:

```bash
docker run -d --name cmind-pg-extract -e POSTGRES_PASSWORD=appdev \
  -v app-pg-data:/var/lib/postgresql/data -p 5544:5432 postgres:17-alpine
CMIND_VOLUME_CONN="Host=127.0.0.1;Port=5544;Database=appdb;Username=postgres;Password=appdev" \
  dotnet test tests/IntegrationTests --filter FullyQualifiedName~LiveTokenBootstrapTests
docker rm -f cmind-pg-extract
```

## Seguridad — solo demo

Las pruebas en vivo tradedean **solo cuentas demo**: el fixture filtra el cache de tokens a cuentas con `IsLive == false` y se conecta al gateway demo, así que la orden nunca puede aterrizar en cuenta live/fondeada incluso si una cuenta live está autorizada. Cada posición que una prueba abre se cierra en el cleanup.

## Ejecución

```bash
# Pruebas deterministas de copy nada más (rápidas, sin secretos, CI-safe)
dotnet test tests/UnitTests --filter FullyQualifiedName~CopyTrading

# Pruebas de copy en vivo contra las cuentas demo reales (necesita los dos archivos de secretos)
dotnet test tests/IntegrationTests --filter FullyQualifiedName~CopyTradingLiveTests

# Todo
dotnet test
```

Sin archivos de secretos las pruebas en vivo imprimen razón de skip + pasan como no-ops, así que la suite es segura para ejecutar en cualquier lugar.

## Cobertura

### Gestión de dinero / sizing (determinista — `CopySizingCalculatorTests`)
FixedLot · LotMultiplier · NotionalMultiplier (tamaño contrato / moneda) · ProportionalBalance ·
ProportionalEquity · ProportionalFreeMargin · AutoProportional · FixedRiskPercent · FixedLeverage ·
scale **up** y **down** por desajuste de balance/leva/capacidad (la "regla de oro") · redondeo de lot-step ·
skip por lot mínimo vs forzar a min · tope de lot máximo · bound más ajustado vs espec min & max · skip por balance cero del master.

### Filtros de decisión (determinista — `CopyDecisionEngineTests`)
Whitelist / blacklist de símbolo / allow · LongOnly / ShortOnly · reverse invierte el lado efectivo ·
slippage sobre límite skip + exactamente-en-límite permitido · skip por señal stale (max delay) · skip por tamaño cero ·
reconciliación en reconexión (dedupe de faltantes abiertos, cierre de huérfanos).

### Copy engine host (determinista — `CopyEngineHostTests`, sesión en memoria)
Open refleja una orden de mercado (lado / volumen / label) · **reverse** invierte lado y **swapea SL/TP** ·
**mapeo de símbolo** resuelve el símbolo destino · **fallo de orden en un slave sigue copiando a los demás** ·
cierre del source cierra la copia reflejada · resincronización en reconnect cierra copias huérfanas.

### Resiliencia de conexión (determinista — `OpenApiConnectionTests`)
Llega a Connected después de auth de la app · conexión caída reconecta y re-authea · error de auth fatal ·
backoff exponencial.

### En vivo, cuentas demo reales de cTrader (`CopyTradingLiveTests`)
Refresh de token + listado de cuentas · copy **1:1** ejecuta · copy **1:many** refleja a cada slave ·
**reverse** convierte master buy en slave sell · copy **cross-cID** (master bajo un cID refleja a slave bajo otro, cada uno autenticando con su propio token). Cada una abre posición real de lot mínimo en master, espera a que el motor la refleje (matched por source-position-id label en slave), asserts, cierra todo. Mercado cerrado reportado **Inconclusive**, no falla.

## Logging y auditabilidad

Cada operación de copy trading registrada vía eventos estructurados source-generated (`Core/Logging/LogMessages.cs`, IDs de evento 1043–1055), trail completo auditable:

| Evento | Id | Significado |
|-------|----|-----------|
| CopyHostStarted | 1046 | el motor de un perfil arrancó (source + conteo de destinos) |
| CopySourceOpen | 1047 | master abrió una posición (símbolo / lado / lots) |
| CopyOrderPlaced | 1048 | orden de copy enviada a un slave (símbolo / lado / volumen / source id) |
| CopySkipped | 1049 | un copy fue omitido y por qué (slippage / dirección / filtro_símbolo / tamaño_cero / …) |
| CopyProtectionApplied | 1050 | SL/TP aplicado a una copia slave |
| CopyOpenFailed | 1051 | el copy-open de un slave falló (aislado — los otros slaves continúan) |
| CopySourceClose / CopyPositionClosed | 1052 / 1053 | master cerró → copia slave cerrada |
| CopyCloseFailed | 1054 | el copy-close de un slave falló |
| CopyResync | 1055 | reconciliación en reconexión (conteo de source abiertos, huérfanos cerrados) |
| CopyPartialClose | 1056 | cierre parcial del master reflejado — slice proporcional cerrada en un slave |
| CopyScaleIn | 1057 | scale-in del master reflejado (opt-in) — volumen añadido copiado a un slave |
| CopyPendingOrderPlaced | 1058 | orden pendiente limit/stop reflejada a un slave (opt-in) |
| CopyPendingOrderCancelled | 1059 | pendiente del source cancelada → pendiente del slave cancelada |
| CopyTrailingApplied | 1060 | trailing stop aplicado a una copia slave (opt-in) |
| CopyStopLossAmended | 1061 | el move del SL del source re-enmendó la copia slave |
| CopyHostTokenRotated | 1062 | supervisor reinició un host en ejecución después de la rotación de su access token |

Logs emitidos como JSON compacto Serilog (props estructurados: `ProfileId`, `DestinationCtid`, `SourcePositionId`, `Symbol`, `Side`, `Volume`, …), enviados a OTLP cuando `OTEL_EXPORTER_OTLP_ENDPOINT` está configurado. **Totalmente configurable** por categoría vía config estándar — ej. subir/bajar la verbosidad del copy-engine sin tocar código:

```jsonc
// appsettings.json — Overrides de nivel Serilog
"Serilog": { "MinimumLevel": { "Override": {
  "CopyEngine": "Information",              // el trail de audit del CopyEngineHost
  "Nodes.CopyTrading": "Information"        // supervisor / refresh de token
} } }
```

`Audit_log_records_every_trading_operation` host test assert que el trail se dispara por open, orden, protección, close.

## Casos borde (validados contra cómo fallan plataformas reales de copy/MAM)

Slippage & latencia, sufijo/mismatch de símbolo, trades duplicados en reconnect, desajuste de leverage & sizing margen-safe, diferencias de moneda de depósito/tamaño de contrato, lot min/max & redondeo, órdenes rechazadas, filtros de dirección, limpieza de huérfanos después de desconexión — todo cubierto arriba. Fuentes:
[desajuste de leverage](https://copygram.app/blog/education/the-truth-about-leverage-mismatches-copying-high-leverage-low-leverage-accounts) ·
[copy cross-broker](https://www.mt4copier.com/cross-broker-trade-copying-efficient-forex-replication/) ·
[trampas del copier](https://www.mt4copier.com/copy-trading-pitfalls-every-account-manager-must-avoid/) ·
[slippage y latencia](https://copygram.app/blog/education/understanding-slippage-latency-copy-trading) ·
[por qué falla el copy trading](https://xtsupport.zendesk.com/hc/en-us/articles/51566808595993-Why-Copy-Trading-Fails-Causes-Prevention-Guide) ·
[parámetros de riesgo](https://www.mt4copier.com/risk-parameters/).

## Cobertura de mirroring avanzado (cierre parcial · órdenes pendientes · SL-trailing)

El host refleja más que market open/close. Cada comportamiento = opt-in por destino en `CopyDestination` (`MirrorPartialClose` por defecto on, `MirrorScaleIn`/`CopyPendingOrders`/`CopyTrailingStop` por defecto off), guardado por métodos de intención, persistido en jsonb (migración `CopyAdvancedMirroringAndNodeAffinity`).

| Comportamiento | Prueba determinista (`CopyEngineHostTests`) | Prueba en vivo |
|-----------|--------------------------------------------|-----------|
| Cierre parcial → slice proporcional | `Partial_close_mirrors_a_proportional_slice_on_the_slave` (1.0→0.4 cierra 60%) + path deshabilitado | `Partial_close_shrinks_the_slave_copy_proportionally` ✅ |
| Scale-in | `Scale_in_is_ignored_by_default_and_mirrored_when_enabled` | — |
| Órden pendiente limit/stop placed | `Pending_order_is_placed_on_the_slave_when_enabled` (Teoría: Limit+Stop) + path deshabilitado | `Pending_limit_order_is_mirrored_and_cancel_propagates` ✅ |
| Cancelación de pendiente | `Source_pending_cancel_cancels_the_slave_pending` | (misma prueba en vivo — cancela en master, assert que slave cancela) ✅ |
| Pendiente filled sin double-open | `Filled_pending_does_not_double_open` (order-id → position-id dedupe) | — |
| Trailing stop | `Trailing_stop_is_applied_to_the_copy_when_enabled` | `Trailing_stop_is_mirrored_onto_the_slave_copy` ✅ |
| Move del SL del source re-enmienda | `Source_stop_loss_move_re_amends_the_copy` | — |
| Eventos de audit disparados | `Advanced_mirroring_audit_events_fire` (1056/1058/1059) | — |

Todas las pruebas en vivo anteriores **verificadas verdes contra cuentas demo reales de cTrader** (1:1, 1:many, reverse, cross-cID, cierre parcial, pending+cancel, trailing).

Adiciones de wire en `OpenApiTradingSession`: `SendPendingOrderAsync`, `CancelOrderAsync`, `ReconcilePendingOrdersAsync`, flag de trailing en `AmendPositionSltpAsync`, campos de orden/pending en `ExecutionEvent`, `LoadSpotPriceAsync` (subscribe spot → bid/ask, usado por pruebas en vivo de pending/trailing para colocar órdenes descansando away from market), `StopLoss`/`TrailingStopLoss` en `OpenPositionSnapshot` (estado trailing de la copia observable vía reconcile). Las copias de destino se mantienen etiquetadas por **source position id** (copias pending por **source order id**) para que el reconcile en reconnect se mantenga id-based, sin duplicar trades.

**Gotcha de evento de cTrader (verificado en vivo):** el `ORDER_ACCEPTED`/`ORDER_CANCELLED` de una orden pendiente resting lleva **un `Position` placeholder no OPEN** más el `Order`. El stream debe clasificarlo como evento *orden* **antes** del branch de posición (contingenciado en que la posición no sea `OPEN`), si no el placement de pending se malinterpreta como cierre de posición. `SourceExecutionsAsync` hace esto; no hacerlo filtra silenciosamente todo el pending mirroring.

## Rotación de token + afinidad de nodo

- **Rotación en hosts en ejecución.** `CopyEngineSupervisor` registra la firma del token en cada host en ejecución y, en cada reconcile, reconstruye el plan desde DB (recién rotado por `OpenApiTokenRefreshService`). La firma cambiada reinicia el host (`CopyHostTokenRotated`, 1062); el nuevo host de `ResyncAsync` reconstruye el estado sin duplicar trades. Rotación forzada mid-run vía `IOpenApiTokenClient.RefreshAsync` para verificar que el host en vivo sigue copiando.
- **Afinidad de nodo (sin double-copy).** Tanto el nodo local Web como el worker `CopyAgent` ejecutan un supervisor. Cada perfil en ejecución es reclamado por exactamente un nodo (`CopyProfile.AssignedNode`, `ExecuteUpdate` atómico con claim key `CopyOptions.NodeName`, nombre de máquina por defecto). Los hosts del supervisor solo contienen perfiles que les pertenecen; stop/pause libera el claim. Cobertura:
  - Dominio (unit): `AssignToNode_makes_profile_hosted_by_only_that_node`,
    `Stopping_a_profile_releases_its_node_assignment`, `NodeIdentity_rejects_blank`.
  - **Integración (Postgres real, Testcontainers)**: `CopyNodeAffinityTests` drivea el `ClaimUnassignedProfilesAsync` real del supervisor — assert que el primer nodo reclama los 3 perfiles en ejecución, el segundo reclama **0** (sin double-host), pause→restart libera el claim para otro nodo.
  - Detección de rotación (`TokenRotationSignatureTests`): la `TokenSignature` del supervisor cambia cuando el token del source o destination rota, estable de lo contrario (el host en ejecución solo se reinicia en rotación real).

### Tokens de refresh de un solo uso (importante)

Los refresh tokens de cTrader son **de un solo uso** — cada refresh retorna un *nuevo* refresh token, invalida el antiguo. El fixture en vivo refresca al inicio, persiste el token rotado a `secrets/openapi-tokens.local.json`. Consecuencias:
- Si el run refresca pero **no puede persistir** el nuevo token (ej. mount de solo lectura), el token cacheado muere, el próximo run falla `ACCESS_DENIED`. Regenerar con onboarding headless:
  `CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests`.
- `LiveCopySecrets.SaveTokens` ignora fallos de escritura así que el cache de solo lectura no crashea el run, pero **en vivo** la suite in-cluster todavía necesita **cache writable** (el Job de K8s copia Secret a emptyDir — ver doc de deployment). 

## Ejecutando la suite en un cluster Kubernetes

La suite completa se ejecuta in-cluster contra la app desplegada con Helm, así que la regresión se detecta in-cluster igual que localmente. Ver [`docs/deployment/kubernetes.md`](../deployment/kubernetes.md#in-cluster-test-suite).

```bash
scripts/k8s-e2e.sh                                   # cluster kind, suite determinista (sin secretos)
TEST_FILTER='FullyQualifiedName~CopyTradingLiveTests' COPY_SECRET=cmind-copy-secrets scripts/k8s-e2e.sh  # en vivo
```

`Dockerfile.tests` construye la imagen del runner; Helm `tests-job.yaml` (gated `tests.enabled=false`) la ejecuta contra Postgres + Web in-cluster. **Por defecto = suite determinista de copy** (sin secretos, sin tokens rotando). Para la suite en vivo, establecer `tests.copySecret` a Secret conteniendo los `openapi-*.local.json` gitignored; el init-container lo copia a **writable** emptyDir en `/app/secrets` (requerido — los refresh tokens de un solo uso deben ser persistibles). Las pruebas de copy solo necesitan Web + Postgres + cache de tokens — no agentes de nodo privilegiados. El script assert que el Job sale 0 y los logs contienen `Passed!`.

**Verificado aquí (Docker, sin cluster):** la imagen de prueba ejecuta la suite determinista (`101 passed`) y, con `secrets/` writable mount, la suite **en vivo** completa (`8 passed`) — camino exacto del Job menos Kubernetes. `kind`/`kubectl`/`helm` no disponibles en el entorno de authoring, así que la ejecución completa del cluster `k8s-e2e.sh` es el paso no ejecutado aquí.

## Live option matrix + caos (LiveCopyMatrix / LiveCopyChaos)

Dos suites en vivo data-driven construidas sobre `LiveCopyScenario` / `LiveCopyFixture`, contraparte en vivo de la suite de stress determinista DST:

- **`LiveCopyMatrix`** — matriz de opciones `[Theory]`/`[MemberData]`: un master real abierto por fila contra cuentas demo, cada una configurada diferentemente en destino, assertando el resultado dorado. Filas: `one_to_one`, `half_multiplier`, `reverse` (lado opuesto), `manage_only` (no abre nada), `trading_hours_closed` (ventana excluye ahora → sin copy), `source_label_block` (filtro label → sin copy), `lot_sanity_block` (techo → sin copy).
- **`LiveCopyChaos`** — copy engine contra inicio hostil: el master ya tiene posición antes de que el host inicie, así que la convergencia solo puede venir del resync al inicio (`Sync-Open-on-Start`), más caso negativo con toggle off. El socket-flap / rotación de token / caos de rechazo determinista cubierto exhaustivamente por la suite DST; esto assert el camino de resync en vivo.

Ambos **saltan limpiamente** sin credenciales, reportan **Inconclusive** en mercado cerrado, cada fila limpia las posiciones que abrió (solo cuentas demo).

**Robustez del fixture:** `LiveCopyFixture` ahora se degrada a skip limpio cuando los tokens cacheados no pueden refrescarse (los refresh tokens de cTrader son de un solo uso — cache stale o run offline ya no falla toda la colección en vivo; re-ejecutar OAuth onboarding para refrescar cache).

## Ejecutando las suites en vivo + cluster tú mismo (SIN excusas — todo es autoservicio)

Todo lo siguiente está disponible en este entorno. **No** affirmar "credenciales no disponibles", "token stale", o "kind/helm no instalados" — configúralos y ejecútalos.

### 1. Obtén un token fresco de Open API (OAuth headless, sin interacción de navegador)

Los refresh tokens de cTrader son de un solo uso, así que el token cacheado SÍ se volverá stale. Remíntalo tú mismo desde username/password cID guardados (`secrets/openapi-cids.local.json` + `secrets/openapi-test-app.local.json`, o unificado `secrets/dev-credentials.local.json`). La prueba de onboarding drivea **Edge headless** OAuth vía Playwright, escribe `secrets/openapi-tokens.local.json`:

```bash
CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests
```

~13s; autoriza cada cID, cachea tokens frescos. Re-ejecutar cuando la suite en vivo reporte fixture unavailable debido a fallo de refresh.

### 2. Ejecuta las suites de copy en vivo (cuentas demo reales de cTrader)

```bash
dotnet test tests/IntegrationTests --filter FullyQualifiedName~CopyTradingLiveTests   # mirroring core (8)
dotnet test tests/IntegrationTests --filter FullyQualifiedName~LiveCopyMatrix          # matriz de opciones (7)
dotnet test tests/IntegrationTests --filter FullyQualifiedName~LiveCopyChaos           # caos de resync (2)
```

Place + limpia órdenes DEMO reales (nunca cuentas live), reporta **Inconclusive** en mercado cerrado. Verificadas verdes end-to-end.

### 3. Bootstrap de tokens desde un volumen de app en ejecución (alternativo)

Si la app está corriendo + cID vinculado en-app, extrae el refresh token más reciente de la app directamente desde el volumen Postgres `app-pg-data` en lugar de re-autorizar — ver `LiveTokenBootstrapTests`, establecer `CMIND_VOLUME_CONN`.

### 4. E2E de Kubernetes cluster

`kind`, `helm`, Docker disponibles (instalar kind/helm vía `go install`/release binaries o `choco install kind kubernetes-helm` si no están en PATH). Script one-shot construye+carga imágenes, despliega chart, ejecuta Job de prueba in-cluster, asserta exit 0:

```bash
scripts/k8s-e2e.sh                                 # suite determinista de copy (sin secretos)
TEST_FILTER='FullyQualifiedName~CopyTradingLiveTests' COPY_SECRET=cmind-copy-secrets scripts/k8s-e2e.sh   # en vivo in-cluster
```

Ver [../deployment/kubernetes.md](../deployment/kubernetes.md).