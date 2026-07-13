---
description: "cMind envía servidor del Protocolo de Contexto del Modelo (MCP) como proceso/Implementación separado — escala + redeploy independiente de aplicación web. Expone herramientas de cBot, instancia, IA…"
---

# Servidor MCP

cMind envía servidor del Protocolo de Contexto del Modelo (MCP) como **proceso/Implementación separado** — escala + redeploy independiente de aplicación web. Expone cBot, instancia, herramientas de IA a clientes MCP (p. ej. asistentes de IA) sobre transporte HTTP + SSE.

## Autenticación

- Claves de API por usuario `mcpk_<hex>`, SHA-256 hasheadas, índice prefijo (`McpKeyAuthHandler`). Gestiona desde página **MCP** (agregado `McpApiKey`).
- Transporte HTTP sin estado con `AddHttpContextAccessor` — llamadas de herramienta se ejecutan como usuario autenticado.

## Herramientas

- `CBotTools` — autor / construir cBots.
- `InstanceTools` — ejecutar / backtest / inspeccionar instancias.
- `AiTools` — generar, revisar, sentimiento, analizar-backtest, herramientas de copia.

## Ops

Expone `/version`; puntos finales de salud (`/health`, `/alive`) mapeados en todos los ambientes para sondas K8s/nube. JSON Serilog estructurado + OpenTelemetry, igual que aplicación web.
