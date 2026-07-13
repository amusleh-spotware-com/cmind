---
title: Mapa de cobertura de rutas de fallo
description: "Cada escenario de fallo que el mandato requiere, mapeado a las pruebas que realmente lo ejercitan - para que una brecha sea visible, no asumida."
---

# Mapa de cobertura de rutas de fallo

El mandato de pruebas es explicito: las rutas de fallo cuentan. Esta pagina mapea cada escenario requerido a las pruebas que lo ejercitan.

## Escenarios requeridos -> pruebas

| Escenario | Niveles | Pruebas |
|---|---|---|
| Caida de conexion -> reconnect | unit, stress, E2E | OpenApiConnectionTests; DST |
| Rechazo de orden | unit, stress | CopyTransparencyTests; CopyCircuitBreakerTests; DST |
| Desync / resync | unit, stress | CopyPartialFillTests; CopyEngineHostTests; DST |
| Rotacion de token | unit, integration, stress | OpenApiAuthorizationTests; TokenRotationSignatureTests; DST |
| Muerte de nodo -> lease reclaim | unit, integration, stress | NodeInstanceReclaimerTests; CopyNodeAffinityTests; DST |
| Error de proveedor de IA | unit, integration | AnthropicAiClientTests; AiHttpResilienceTests |
| IA completamente deshabilitada | unit, integration, E2E | AiFeatureServiceTests; AiRecommendDisabledTests; AiPagesTests |
| Fallo transitorio de DB | integration | DatabaseResilienceTests; MigrationLockTests |
| Fallo de agente HTTP de nodo | integration | NodeAgentHttpResilienceTests |
| Auto-exit de contenedor | unit | BacktestCompletionPollerTests |
| Incumplimiento de prop-firm | unit, integration | PropFirmChallengeRulesTests; PropFirmAlertNotifierTests |
| Entrada invalida / rechazo de auth | unit, integration, E2E | LoginTests; BrandingOptionsValidatorTests |

## Puntos delgados

- Rechazo de auth de herramienta MCP - ninguna prueba dedicada encontrada.
- Fallo de compilacion de cBot surfacing - la rama de fallo necesita confirmacion.

## Como se enforce

La suite DST deterministic replay estos fallos en un reloj comprimido y debe mantenerse verde. FakeTradingSession es el simulador cTrader-faithful que estas pruebas impulsan.
