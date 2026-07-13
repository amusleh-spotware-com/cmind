---
title: Mapa de cobertura de ruta de fallo
description: Cada escenario de fallo que el mandato requiere, mapeado a la(s) prueba(s) que realmente lo ejercita — para que una brecha sea visible, no asumida.
---

# Failure-path coverage map

El mandato de prueba es explícito: **las rutas de fallo cuentan** — un cambio que puede romperse en una conexión caída, una orden rechazada, un desync, una rotación de token, o un nodo muerto se envía con una prueba para eso, en el mismo commit. Esta página mapea cada escenario requerido a la(s) prueba(s) que lo ejercitan, para que una brecha real sea *visible* en lugar de asumida. Cuando agregas una ruta de fallo, agrega una fila aquí.

## Escenarios requeridos → pruebas

| Escenario | Tier(s) | Pruebas |
|---|---|---|
| **Caída de conexión → reconexión** | unit · stress · E2E | `OpenApiConnectionTests.Dropped_connection_reconnects_and_raises_reconnected`; `FakeTradingSession.Disconnect/ReconnectAsync` y `SyncTradingSession` (DST); `MiscUiTests` estados de modal de reconexión |
| **Rechazo de orden** | unit · stress | `CopyTransparencyTests.A_rejected_open_emits_a_Failed_execution_fact_with_the_reason`; `CopyCircuitBreakerTests`; DST `CopyDstWorld.FailOrders` / `RejectMarketRange` |
| **Desync / resync** | unit · stress | `CopyPartialFillTests.Resync_tops_up_a_broker_partial_fill…`; `CopyEngineHostTests.Reconnect_resync_closes_orphaned_destination_positions` (+ `…tolerates_a_position_not_found…`); `CopyAdvancedScenariosTests.Reconnect_resync_opens_missing_copies_and_closes_orphans_after_a_desync`; `CopyChaosDstTests` |
| **Rotación de token / invalidación** | unit · integration · stress | `OpenApiAuthorizationTests.MarkRefreshFailed_*` (ventana de escalada); `FakeTradingSession.InvalidateToken`; `TokenRotationSignatureTests`, `LiveTokenBootstrapTests`, `OpenApiTokenRefreshPersistenceTests` (integration); DST `RotateTokens` |
| **Muerte del nodo → reclamación de arrendamiento** | unit · integration · stress | `NodeInstanceReclaimerTests` (unit + integration); `CopyRulesDomainTests.Lease_is_held_only_by_the_claiming_node_until_it_expires`; `CopyHostWatchdogTests`, `CopyNodeAffinityTests`, `PropFirmTrackingLeaseTests` (integration); `CopyLeaseReclaimStressTests` |
| **Error del proveedor de IA (4xx/5xx/timeout/malformado)** | unit · integration | `AnthropicAiClientTests.Fails_gracefully_on_error_status` / `…on_malformed_json` / `…on_empty_content`; `AiHttpResilienceTests`, `AiRecommendDisabledTests` (integration) |
| **IA completamente deshabilitada (sin clave)** | unit · integration · E2E | `AiFeatureServiceTests`; `AiRecommendDisabledTests`; `AiPagesTests` |
| **Fallo transitorio de base de datos / bloqueo de migración** | integration | `DatabaseResilienceTests`; `MigrationLockTests` |
| **Fallo de agente HTTP de nodo / reintento** | integration | `NodeAgentHttpResilienceTests` |
| **Contenedor auto-salida reconciliación** | unit | `BacktestCompletionPollerTests`; cobertura de `RunCompletionPoller` en `ContainerCommandHelpersTests` |
| **Violación de prop-firm** | unit · integration | `PropFirmChallengeRulesTests`; `PropFirmAlertNotifierTests`; `PropFirmChallengePersistenceTests` |
| **Entrada inválida / auth rechaza (UI + marca)** | unit · integration · E2E | `LoginTests.Invalid_credentials_show_an_error`; `HexColorTests.Rejects_invalid_hex`; `BrandingOptionsValidatorTests` |

## Puntos delgados — verifica antes de asumir cubierto

Estos merecen una verificación explícita (agrega una fila arriba una vez confirmado o completado):

- **Rechazo de auth de herramienta MCP** — `McpKeyAuthHandler` rechaza una clave mala/ausente. No se encontró prueba dedicada; agregar una prueba de integración que llama a un endpoint de herramienta MCP con clave faltante/inválida y asevera 401.
- **Fallo de compilación de cBot superficie** — un error de compilación debe aterrizar en la instancia/UI como `Failed` con la salida de compilación. `CBotLifecycleTests` cubre el camino feliz; confirmar que la rama de fallo se asevera.
- **Ejecución de orden en vivo** — ejecución de copia de extremo a extremo contra credenciales de cTrader reales sigue siendo gated (necesita credenciales + un clúster de nodo); ver [Live copy trading](./live-copy-trading.md).

## Cómo se hace cumplir esto

La suite de estrés determinista (DST, `tests/StressTests`) reproduce estos fallos en un reloj comprimido y debe mantenerse verde — **nunca debilites un escenario DST para hacerlo pasar; arregla el código**. El [FakeTradingSession](./fake-trading-session.md) es el simulador fiel a cTrader que estas pruebas unitarias conducen; extender para nuevo comportamiento de corredor en lugar de relajar una aserción.
