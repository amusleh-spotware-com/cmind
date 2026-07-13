---
description: "Suite de testing de copy-trading completa y reproducible. Dos capas:"
---

# Suite de testing de copy-trading (determinista + live)

Suite de testing de copy-trading completa y reproducible. Dos capas:

1. **Tests deterministas** (xUnit, sin red) — matemática de copy + lógica del engine. Rápidos, CI, sin secretos. Cubren cada modo de gestión de dinero, cada filtro/opción, resiliencia del engine.
2. **Tests E2E live** (cuentas demo reales de cTrader) — `CopyEngineHost` de producción colocando + copiando órdenes reales entre cuentas reales. Completamente automatizados, re-ejecutables como test unitario: leen credenciales cacheadas de archivos locales gitignored, auto-refrescan access token, skip limpio cuando faltan secretos (CI stays green).

Nunca corre contra cuenta live financiada — cada cuenta **demo**, cada test live cierra las posiciones que abrió.

## Layout

```
tests/UnitTests/CopyTrading/
  CopySizingCalculatorTests.cs   — cada modo de sizing + rounding + lot min/max
  CopyDecisionEngineTests.cs     — dirección/reverse/slippage/delay/filtro símbolo/tamaño cero
  CopyEngineHostTests.cs         — lógica de copy del host contra sesión fake en memoria
  FakeTradingSession.cs          — IOpenApiTradingSession determinista (registra órdenes/cierres/enmiendas)
  OpenApiConnectionTests.cs      — connect / reconnect / backoff / fault fatal (resiliencia)

tests/IntegrationTests/CopyLive/
  LiveCopySecrets.cs             — carga los secretos gitignored, guarda tokens refrescados
  LiveTokenBootstrapTests.cs     — one-shot: desencripta tokens del DB de la app al caché de tokens
  LiveCopyFixture.cs             — rota el access token, expone la lista de cuentas demo
  LiveCopyScenario.cs            — ejecuta un escenario real de copy end to end (open → copy → verify → cleanup)
  CopyTradingLiveTests.cs        — los escenarios live (1:1, 1:many, reverse, …)
```

## Secretos (locales, gitignored — nunca comprometidos)

Todas las credenciales bajo `<repo>/secrets/` (ya en `.gitignore`). El dev escribe **solo los primeros dos archivos**; el tercero (tokens) se produce automáticamente por onboarding.

`secrets/openapi-test-app.local.json` — Open API app:

```json
{ "ClientId": "2175_…", "ClientSecret": "…" }
```

`secrets/openapi-cids.local.json` — credenciales de login cID para autorizar (una o varias):

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

El refresh token **nunca expira**, así que después del onboarding único los tests live funcionan indefinidamente: cada ejecución intercambia el refresh token de cada cID por un access token fresco (rotación) — sin navegador, sin prompts.

## Onboarding único (completamente automatizado — sin interacción del dev más allá de guardar credenciales)

El onboarding drivea login de cTrader ID real en navegador headless desde credenciales cID guardadas, captura el callback OAuth en un listener HTTPS local en el redirect registrado de la app (`https://localhost:7080/openapi/callback`), intercambia el code por tokens, carga la lista de cuentas, escribe el caché de tokens multi-cID. Ejecutar una vez por máquina (o al agregar cID):

```bash
CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests
```

Autoriza cada cID en `openapi-cids.local.json`, escribe `openapi-tokens.local.json`. Después los tests de copy live no necesitan nada más. (La cuenta cTrader del cID no debe tener 2FA/captcha en login para que la automatización complete.)

**Bootstrap alternativo** (si las cuentas ya están autorizadas en la app corriendo): desencripta los tokens almacenados directamente del volumen Postgres de la app en lugar de re-autorizar:

```bash
docker run -d --name cmind-pg-extract -e POSTGRES_PASSWORD=appdev \
  -v app-pg-data:/var/lib/postgresql/data -p 5544:5432 postgres:17-alpine
CMIND_VOLUME_CONN="Host=127.0.0.1;Port=5544;Database=appdb;Username=postgres;Password=appdev" \
  dotnet test tests/IntegrationTests --filter FullyQualifiedName~LiveTokenBootstrapTests
docker rm -f cmind-pg-extract
```

## Seguridad — solo demo

Los tests live operan **solo con cuentas demo**: el fixture filtra el caché de tokens a cuentas con `IsLive == false` y se conecta al gateway demo, así que la orden nunca puede aterrizar en cuenta live/financiada. Cada posición que un test abre se cierra en cleanup.

## Ejecutando

```bash
# Tests de copy deterministas solo (rápidos, sin secretos, CI-safe)
dotnet test tests/UnitTests --filter FullyQualifiedName~CopyTrading

# Tests de copy live contra las cuentas demo reales (necesita los dos archivos de secretos)
dotnet test tests/IntegrationTests --filter FullyQualifiedName~CopyTradingLiveTests

# Todo
dotnet test
```

Sin los archivos de secretos los tests live imprimen motivo del skip + pasan como no-ops, así que el suite es seguro para ejecutar en cualquier lugar.

## Cobertura

### Gestión de dinero / sizing (determinista — `CopySizingCalculatorTests`)
FixedLot · LotMultiplier · NotionalMultiplier (tamaño de contrato / moneda) · ProportionalBalance ·
ProportionalEquity · ProportionalFreeMargin · AutoProportional · FixedRiskPercent · FixedLeverage ·
scale **up** y **down** por mismatch de balance/leverage/capacidad (la "regla de oro") · lot-step
rounding · min-lot skip vs force-to-min · max-lot cap · tighter-of bound-vs-spec min & max · zero
master balance skip.

### Filtros de decisión (determinista — `CopyDecisionEngineTests`)
Whitelist / blacklist de símbolo / allow · LongOnly / ShortOnly · reverse invierte el lado efectivo ·
slippage sobre límite skip + exactamente-en-límite permitido · stale-signal (max delay) skip · size-zero skip ·
reconciliación de reconnect (dedupe de missing abierto, cleanup de huérfanos).

### Copy engine host (determinista — `CopyEngineHostTests`, sesión en memoria)
Open copia una orden de mercado (lado / volumen / label) · **reverse** invierte el lado y **swapea SL/TP** ·
**symbol mapping** resuelve el símbolo de destino · **order-failure en un slave aún copia a los otros** · source close cierra la copia mirrored · reconnect resync cierra huérfanas.

### Resiliencia de conexión (determinista — `OpenApiConnectionTests`)
Llega a Connected después de auth de app · conexión dropeada reconnects y re-auths · fatal auth error faults ·
exponential backoff.

### Live, cuentas demo reales de cTrader (`CopyTradingLiveTests`)
Token refresh + account listing · **1:1** copy ejecuta · **1:many** copy refleja a cada slave ·
**reverse** convierte la compra del master en venta del slave · **cross-cID** copy (master bajo un cID copia a slave bajo otro cID, cada uno autenticando con su propio token). Cada uno abre posición real de min-lot en master, espera a que el engine la refleje (matched por source-position-id label en slave), asserts, cierra todo. Mercado cerrado reportado **Inconclusive**, no falla.

## Logging y auditabilidad

Cada operación de copy trading registrada via eventos estructurados source-generated (`Core/Logging/LogMessages.cs`, IDs de evento 1043–1055), trail completo auditable:

| Evento | Id | Significado |
|--------|----|-------------|
| CopyHostStarted | 1046 | el engine de un perfil arrancó (source + conteo de destinos) |
| CopySourceOpen | 1047 | master abrió una posición (símbolo / lado / lots) |
| CopyOrderPlaced | 1048 | orden de copy enviada a un slave (símbolo / lado / volumen / source id) |
| CopySkipped | 1049 | un copy fue saltado y por qué (slippage / dirección / filtro_símbolo / tamaño_cero / …) |
| CopyProtectionApplied | 1050 | SL/TP aplicado a una copia de slave |
| CopyOpenFailed | 1051 | un copy-open de slave falló (aislado — otros slaves continúan) |
| CopySourceClose / CopyPositionClosed | 1052 / 1053 | master cerró → slave copy cerró |
| CopyCloseFailed | 1054 | un copy-close de slave falló |
| CopyResync | 1055 | reconciliación de reconnect (conteo de abiertos source, huérfanos cerrados) |
| CopyPartialClose | 1056 | master partial close reflejado — tajada proporcional cerrada en un slave |
| CopyScaleIn | 1057 | master scale-in mirrored (opt-in) — volumen agregado copiado a un slave |
| CopyPendingOrderPlaced | 1058 | pendiente limit/stop reflejada a un slave (opt-in) |
| CopyPendingOrderCancelled | 1059 | pendiente de source cancelada → pendiente de slave cancelada |
| CopyTrailingApplied | 1060 | trailing stop aplicado a una copia de slave (opt-in) |
| CopyStopLossAmended | 1061 | un movimiento de SL de source re-enmendó la copia de slave |
| CopyHostTokenRotated | 1062 | supervisor reinició un host corriendo después de que su access token rotó |

Logs emitidos como JSON compacto Serilog (props estructurados: `ProfileId`, `DestinationCtid`, `SourcePositionId`, `Symbol`, `Side`, `Volume`, …), enviado a OTLP cuando `OTEL_EXPORTER_OTLP_ENDPOINT` está configurado. **Completamente configurable** por categoría via config estándar — p. ej. subir/bajar verbosidad del copy-engine sin tocar código:

```jsonc
// appsettings.json — overrides de nivel Serilog
"Serilog": { "MinimumLevel": { "Override": {
  "CopyEngine": "Information",              // el audit trail del CopyEngineHost
  "Nodes.CopyTrading": "Information"        // supervisor / token refresh
} } }
```

El test de host `Audit_log_records_every_trading_operation` asserts que el trail dispara para open, orden, protección, close.

## Casos borde (validados contra cómo fallan plataformas reales de copy/MAM)

Slippage & latencia, suffix/mismatch de símbolo, trades duplicados en reconnect, mismatch de leverage & sizing safe de margen, diferencias de deposit-currency/contract-size, min/max lot & rounding, órdenes rechazadas, filtros de dirección, cleanup de huérfanos después de desconexión — todo cubierto arriba. Fuentes:
[leverage mismatch](https://copygram.app/blog/education/the-truth-about-leverage-mismatches-copying-high-leverage-low-leverage-accounts) ·
[cross-broker copying](https://www.mt4copier.com/cross-broker-trade-copying-efficient-forex-replication/) ·
[copier pitfalls](https://www.mt4copier.com/copy-trading-pitfalls-every-account-manager-must-avoid/) ·
[slippage & latency](https://copygram.app/blog/education/understanding-slippage-latency-copy-trading) ·
[why copy trading fails](https://xtsupport.zendesk.com/hc/en-us/articles/51566808595993-Why-Copy-Trading-Fails-Causes-Prevention-Guide) ·
[risk parameters](https://www.mt4copier.com/risk-parameters/).

## Cobertura de mirroring avanzado (partial close · pending orders · SL-trailing)

El host refleja más que market open/close. Cada comportamiento = flag opt-in por destino en `CopyDestination` (`MirrorPartialClose` por defecto on, `MirrorScaleIn`/`CopyPendingOrders`/`CopyTrailingStop` por defecto off), guarded por métodos de intención, persistido en jsonb (migration `CopyAdvancedMirroringAndNodeAffinity`).

| Comportamiento | Test determinista (`CopyEngineHostTests`) | Test live |
|---------------|-------------------------------------------|-----------|
| Partial close → tajada proporcional | `Partial_close_mirrors_a_proportional_slice_on_the_slave` (1.0→0.4 cierra 60%) + path deshabilitado | `Partial_close_shrinks_the_slave_copy_proportionally` ✅ |
| Scale-in | `Scale_in_is_ignored_by_default_and_mirrored_when_enabled` | — |
| Pending limit/stop placed | `Pending_order_is_placed_on_the_slave_when_enabled` (Teoría: Limit+Stop) + path deshabilitado | `Pending_limit_order_is_mirrored_and_cancel_propagates` ✅ |
| Pending cancel | `Source_pending_cancel_cancels_the_slave_pending` | (mismo test live — cancela en master, assert que slave cancela) ✅ |
| Filled pending no double-open | `Filled_pending_does_not_double_open` (order-id → position-id dedupe) | — |
| Trailing stop | `Trailing_stop_is_applied_to_the_copy_when_enabled` | `Trailing_stop_is_mirrored_onto_the_slave_copy` ✅ |
| Source SL move re-amend | `Source_stop_loss_move_re_amends_the_copy` | — |
| Audit events fire | `Advanced_mirroring_audit_events_fire` (1056/1058/1059) | — |

Todos los tests live arriba **verificados green contra cuentas demo reales de cTrader** (1:1, 1:many, reverse, cross-cID, partial close, pending+cancel, trailing).

Adiciones en `OpenApiTradingSession`: `SendPendingOrderAsync`, `CancelOrderAsync`, `ReconcilePendingOrdersAsync`, trailing flag en `AmendPositionSltpAsync`, campos order/pending en `ExecutionEvent`, `LoadSpotPriceAsync` (spot subscribe → bid/ask, usado por tests live pending/trailing para colocar órdenes lejos del mercado), `StopLoss`/`TrailingStopLoss` en `OpenPositionSnapshot` (estado observable trailing de copy vía reconcile). Las copias de destino se mantienen etiquetadas por **source position id** (copias pending por **source order id**) para que el reconcile de reconnect se mantenga id-based, nunca duplicate trade.

**cTrader event gotcha (verificado live):** el execution event de `ORDER_ACCEPTED`/`ORDER_CANCELLED` de una orden pending resting lleva **placeholder `Position` no-OPEN** más el `Order`. El stream debe clasificarlo como evento *order* **antes** del branch de posición (gated en position not `OPEN`), si no la placement de pending se mal-interpreta como position close. `SourceExecutionsAsync` hace esto; no hacerlo silently drop todo el pending mirroring.

## Rotación de token + afinidad de nodo

- **Rotación en hosts corriendo.** `CopyEngineSupervisor` registra la firma de token en cada host corriendo y, en cada reconcile, reconstruye el plan desde BD (rotado frescos por `OpenApiTokenRefreshService`). La firma cambiada reinicia el host (`CopyHostTokenRotated`, 1062); el nuevo host's `ResyncAsync` reconstruye el estado sin duplicar trades. Rotación forzada mid-run via `IOpenApiTokenClient.RefreshAsync` para verificar que el host live mantiene copy.
- **Afinidad de nodo (sin double-copy).** Tanto el nodo local Web como el worker `CopyAgent` corren un supervisor. Cada perfil corriendo reclamado por exactamente un nodo (`CopyProfile.AssignedNode`, `ExecuteUpdate` atómico con claim keyed off `CopyOptions.NodeName`, nombre de máquina por defecto). El supervisor hosts solo perfiles que posee; stop/pause libera claim. Cobertura:
  - Dominio (unit): `AssignToNode_makes_profile_hosted_by_only_that_node`,
    `Stopping_a_profile_releases_its_node_assignment`, `NodeIdentity_rejects_blank`.
  - **Integración (Postgres real, Testcontainers)**: `CopyNodeAffinityTests` drivea el `ClaimUnassignedProfilesAsync` real del supervisor — assert que el primer nodo reclama los 3 perfiles corriendo, el segundo reclama **0** (sin double-host), pause→restart libera claim para otro nodo.
  - Detección de rotación (`TokenRotationSignatureTests`): la `TokenSignature` del supervisor cambia cuando el token del source o destination rota, estable de lo contrario (host corriendo solo se reinicia en rotación real).

### Refresh tokens de un solo uso (importante)

Los refresh tokens de cTrader son de **un solo uso** — cada refresh retorna *nuevo* refresh token, invalida el anterior. El fixture live refresca al inicio, persiste el token rotado a `secrets/openapi-tokens.local.json`. Consecuencias:
- Si el run refresca pero **no puede persistir** el nuevo token (p. ej. mount de solo lectura), token cacheado muerto, el siguiente run falla `ACCESS_DENIED`. Regenera con onboarding headless:
  `CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests`.
- `LiveCopySecrets.SaveTokens` traga fallos de escritura para que el cache de solo lectura no crashee el run, pero el suite **live** in-cluster aún necesita **cache escribible** (el K8s Job copia Secret en emptyDir — ver doc de despliegue).

## Ejecutando el suite en un clúster Kubernetes

El suite completo corre in-cluster contra la app desplegada con Helm, así que la regresión se detecta in-cluster igual que localmente. Ver [`docs/deployment/kubernetes.md`](../deployment/kubernetes.md#in-cluster-test-suite).

```bash
scripts/k8s-e2e.sh                                   # kind cluster, suite determinista (sin secretos)
TEST_FILTER='FullyQualifiedName~CopyTradingLiveTests' COPY_SECRET=cmind-copy-secrets scripts/k8s-e2e.sh  # live
```

`Dockerfile.tests` construye la imagen del runner; el `tests-job.yaml` de Helm (gated `tests.enabled=false`) lo ejecuta contra Postgres + Web in-cluster. **Por defecto = suite determinista** (sin secretos, sin tokens rotando). Para el suite live, establece `tests.copySecret` a Secret sosteniendo los `openapi-*.local.json` gitignored; el init-container lo copia en **emptyDir escribible** en `/app/secrets` (necesario — los refresh tokens de un solo uso deben poder persistirse). Los tests de copy solo necesitan Web + Postgres + token cache — no agentes de nodo privilegiados. El script assert que el Job sale 0 y los logs contienen `Passed!`.

**Verificado aquí (Docker, sin cluster):** la imagen de test corre suite determinista (`101 passed`) y, con `secrets/` mount escribible, suite **live** completo (`8 passed`) — ruta exacta del Job sin Kubernetes. `kind`/`kubectl`/`helm` no disponibles en el ambiente de autoría, así que la ejecución completa del cluster `k8s-e2e.sh` es el único paso no ejecutado aquí.

## Live option matrix + chaos (LiveCopyMatrix / LiveCopyChaos)

Dos suites live data-driven construidas sobre `LiveCopyScenario` / `LiveCopyFixture`, contraparte live del suite de stress determinista DST:

- **`LiveCopyMatrix`** — `[Theory]`/`[MemberData]` matriz de opciones: un master real abierto por row contra cuentas demo, cada una con destino configurado diferente, assertando resultado dorado. Rows: `one_to_one`, `half_multiplier`, `reverse` (lado opuesto), `manage_only` (abre nada), `trading_hours_closed` (ventana excluye ahora → sin copy), `source_label_block` (filtro de label → sin copy), `lot_sanity_block` (techo → sin copy).
- **`LiveCopyChaos`** — copy engine contra inicio hostil: el master ya tiene posición antes de que el host inicie, así que la convergencia solo puede venir del resync de startup (`Sync-Open-on-Start`), más el caso negativo con toggle off. El caos determinista de socket-flap / token-rotation / rejection cubierto exhaustivamente por el suite DST; esto assert la ruta live de resync.

Ambos **skip cleanly** sin creds, reportan **Inconclusive** en mercado cerrado, cada row limpia las posiciones que abrió (solo cuentas demo).

**Robustez del fixture:** `LiveCopyFixture` ahora degrada a skip limpio cuando los tokens cacheados no pueden refrescarse (refresh tokens de cTrader de un solo uso — cache stale o run offline ya no faulta la colección live completa; re-ejecutar onboarding OAuth para refrescar cache).

## Ejecutando el suite live + cluster tú mismo (SIN excusas — todo es self-serviceable)

Todo lo de abajo disponible en este ambiente. No afirme "creds no disponibles", "token stale", o "kind/helm no instalado" — configúrelos y ejecútelos.

### 1. Obtén un token Open API fresco (OAuth headless, sin interacción de navegador)

Los refresh tokens de cTrader son de un solo uso, así que el token cacheado se volverá stale. Remíntalo tú mismo desde saved cID username/password (`secrets/openapi-cids.local.json` + `secrets/openapi-test-app.local.json`, o unificado `secrets/dev-credentials.local.json`). El test de onboarding drivea OAuth Edge headless via Playwright, escribe `secrets/openapi-tokens.local.json`:

```bash
CMIND_ONBOARD=1 dotnet test tests/E2ETests --filter FullyQualifiedName~OnboardingTests
```

~13s; autoriza cada cID, cachea tokens frescos. Re-ejecutar cuando el suite live reporta fixture unavailable debido a fallo de refresh.

### 2. Ejecuta los suites de copy live (cuentas demo reales de cTrader)

```bash
dotnet test tests/IntegrationTests --filter FullyQualifiedName~CopyTradingLiveTests   # mirroring core (8)
dotnet test tests/IntegrationTests --filter FullyQualifiedName~LiveCopyMatrix          # matriz de opciones (7)
dotnet test tests/IntegrationTests --filter FullyQualifiedName~LiveCopyChaos           # caos de resync (2)
```

Coloca + limpia órdenes DEMO reales (nunca cuentas live), reporta **Inconclusive** en mercado cerrado. Verificados green end to end.

### 3. Bootstrap de tokens desde un volumen de app corriendo (alternativo)

Si la app corre + cID linkeado in-app, extrae el refresh token más reciente de la app directamente desde el volumen `app-pg-data` Postgres en lugar de re-autorizar — ver `LiveTokenBootstrapTests`, establece `CMIND_VOLUME_CONN`.

### 4. E2E de clúster Kubernetes

`kind`, `helm`, Docker disponibles (instalar kind/helm via `go install`/release binaries o `choco install kind kubernetes-helm` si no están en PATH). Script one-shot construye+carga imágenes, despliega chart, ejecuta el Job de test in-cluster, asserta exit 0:

```bash
scripts/k8s-e2e.sh                                 # suite determinista de copy (sin secretos)
TEST_FILTER='FullyQualifiedName~CopyTradingLiveTests' COPY_SECRET=cmind-copy-secrets scripts/k8s-e2e.sh   # live in-cluster
```

Ver [../deployment/kubernetes.md](../deployment/kubernetes.md).
