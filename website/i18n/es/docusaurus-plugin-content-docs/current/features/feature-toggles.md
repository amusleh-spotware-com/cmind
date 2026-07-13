---
description: "Las implementaciones de etiqueta blanca rara vez envían cada capacidad. Las alternancias de características permiten que el operador active/desactive características del producto principal — en tiempo de implementación vía configuración, o después en…"
---

# Alternancias de características

Las implementaciones de etiqueta blanca rara vez envían cada capacidad. Las alternancias de características permiten que operador active/desactive características principales
del producto — en tiempo de implementación vía configuración, o después en tiempo de ejecución, sin
redeploy. **Todas las características están habilitadas de forma predeterminada**; la implementación solo lista las que cambia.

## Modelo

- `Core.Features.FeatureFlag` — enumeración de características controlables: `Authoring`, `Backtesting`, `Execution`,
  `CopyTrading`, `Ai`, `PortfolioAgent`, `Alerts`, `PropGuard`, `PropFirm`, `Accounts`, `OpenApi`, `Mcp`,
  `Compliance`. Superficies de administrador Core
  (panel de control, usuarios, nodos, autenticación) nunca controlables, no aquí.
- `Core.Options.FeaturesOptions` — línea base de configuración, vinculada desde `App:Features`. Cada propiedad
  por defecto `true`.
- `Core.Features.IFeatureGate` — resuelve estado **efectivo**: línea base de configuración superpuesta
  con invalidación opcional de propietario-establecida en tiempo de ejecución. Implementada por `Infrastructure.Features.FeatureGate`,
  cachés de invalidación brevemente (`FeatureSettings.OverrideCacheTtl`), invalida en cambio.

Las invalidaciones en tiempo de ejecución se almacenan como filas `AppSetting` con clave `feature.<FeatureFlag>` (valor `true`/`false`).
Sin fila = "usar línea base de configuración".

## Dos formas de deshabilitar una característica

### 1. Configuración de implementación (línea base)

Establece bandera `false` bajo `App:Features`. Ejemplo `appsettings.json`:

```json
{
  "App": {
    "Features": {
      "CopyTrading": false,
      "PropGuard": false
    }
  }
}
```

O vía variables de entorno (doble guion bajo):

```
App__Features__CopyTrading=false
```

Línea base de compuertas **registro de inicio** de trabajadores de fondo (`Nodes.AddNodes`) y herramientas MCP
(`Mcp` servidor), por lo que característica deshabilitada en configuración nunca inicia sus servicios alojados ni expone sus
herramientas MCP.

### 2. Invalidación en tiempo de ejecución (propietario)

El propietario puede voltear cualquier característica en vivo desde **Configuración → Características** (`/settings/features`) o API:

```
GET  /api/features            -> [{ "flag": "CopyTrading", "enabled": true }, ...]   (Propietario)
PUT  /api/features/{flag}      cuerpo { "enabled": false }  -> establecer invalidación             (Propietario)
PUT  /api/features/{flag}      cuerpo { "enabled": null  }  -> limpiar invalidación (revertir)  (Propietario)
```

Los cambios en tiempo de ejecución toman efecto inmediatamente para compuertas en tiempo de solicitud (navegación, API). Trabajadores de fondo
y herramientas MCP controladas en inicio, recogen cambio en tiempo de ejecución en reinicio de proceso siguiente.

## Qué cada compuerta aplica

| Capa | Mecanismo | Tiempo |
|-------|-----------|--------|
| HTTP API | `RouteGroupBuilder.RequireFeature(flag)` filtro de punto final → `404` cuando deshabilitado | Tiempo de ejecución |
| Navegación | `NavMenu` oculta enlaces vía `IFeatureGate.IsEnabled` | Tiempo de ejecución |
| Trabajadores de fondo | `AddHostedService` condicional en `Nodes.AddNodes` | Inicio (configuración) |
| Herramientas MCP | `WithTools<>` condicional en servidor MCP | Inicio (configuración) |

Característica alcanzada por enlace profundo mientras deshabilitada renderiza página vacía — su API devuelve `404`;
nav ya no la muestra.

## Mapa Bandera → superficie

| Bandera | Grupos de API | Nav | Trabajadores / MCP |
|------|-----------|-----|----------------|
| Authoring | `/api/cbots`, `/api/paramsets`, `/api/builder` | grupo cBots → cBots (conjuntos de params diálogo por-cBot) | MCP `CBotTools` |
| Backtesting | (comparte `/api/instances`) | grupo cBots → Backtest | — |
| Execution | `/api/instances` | grupo cBots → Ejecutar | MCP `InstanceTools` |
| CopyTrading | `/api/copy` | Copia de trading | `CopyEngineSupervisor`, `OpenApiTokenRefreshService`, MCP `CopyTools` |
| Ai | `/api/ai` | grupo IA → IA; Configuración → IA (clave) | `AiRiskGuard`, MCP `AiTools` |
| PortfolioAgent | `/api/agent` | grupo IA → Agente de Cartera | `PortfolioAgentService` |
| Alerts | `/api/alerts` | grupo IA → Alertas | `AlertEvaluator` |
| PropGuard | `/api/prop` | grupo Prop → Guardia Prop | `PropGuardService` |
| PropFirm | `/api/prop-firm` | grupo Prop → Desafíos | — |
| Accounts | `/api/ctids` | Cuentas de Trading | — |
| OpenApi | `/api/openapi` | Configuración → Open API | — |
| Mcp | `/api/mcp-keys` | grupo IA → Claves MCP | — |
| Compliance | `/api/compliance` | Configuración → Legal y Privacidad | — |

## Pruebas

- **Unidad** — `UnitTests/Features/FeaturesOptionsTests.cs`: predeterminados de línea base, mapeo por-bandera.
- **Integración** — `IntegrationTests/FeatureGateTests.cs`: línea base de configuración, invalidación en tiempo de ejecución vence
  configuración y persiste como `AppSetting`, limpiar revierte a línea base (Postgres real).
- **E2E** — `E2ETests/FeatureToggleTests.cs`: deshabilitación de `CopyTrading` en tiempo de ejecución oculta su enlace nav y
  `404`s `/api/copy`, reinhabilitación restaura ambos.
