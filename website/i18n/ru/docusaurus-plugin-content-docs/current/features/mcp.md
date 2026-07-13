---
description: "cMind поставляет Model Context Protocol (MCP) сервер как отдельный процесс/Deployment — scale + перезаразвернуть независимо от Web приложения. Expose cBot, instance, AI tools…"
---

# MCP сервер

cMind поставляет Model Context Protocol (MCP) сервер как **отдельный процесс/Deployment** — scale + перезаразвернуть независимо от Web приложения. Expose cBot, instance, AI tools MCP клиентам (например AI assistants) через HTTP + SSE transport.

## Auth

- Per-user API ключи `mcpk_<hex>`, SHA-256 hashed, prefix-indexed (`McpKeyAuthHandler`). Управляйте из **Mcp** страницы (`McpApiKey` агрегат).
- Stateless HTTP transport с `AddHttpContextAccessor` — tool calls работают как authed пользователь.

## Tools

- `CBotTools` — author / build cBots.
- `InstanceTools` — run / backtest / inspect instances.
- `AiTools` — generate, review, sentiment, analyze-backtest, copy tools.

## Ops

Expose `/version`; health endpoints (`/health`, `/alive`) mapped все environments для K8s/cloud probes. Structured Serilog JSON + OpenTelemetry, то же как Web приложение.
