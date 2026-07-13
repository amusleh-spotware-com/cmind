---
description: "cMind ship Model Context Protocol (MCP) server jako separate process/Deployment — scale + redeploy independent z Web app. Expose cBot, instance, AI tools…"
---

# MCP server

cMind ship Model Context Protocol (MCP) server jako **separate process/Deployment** — scale + redeploy independent z Web app. Expose cBot, instance, AI tools do MCP clients (np. AI assistants) nad HTTP + SSE transport.

## Auth

- Per-user API keys `mcpk_<hex>`, SHA-256 hashed, prefix-indexed (`McpKeyAuthHandler`). Manage z **Mcp** page (`McpApiKey` aggregate).
- Stateless HTTP transport z `AddHttpContextAccessor` — tool calls run jako authed user.

## Tools

- `CBotTools` — author / build cBots.
- `InstanceTools` — run / backtest / inspect instances.
- `AiTools` — generate, review, sentiment, analyze-backtest, copy tools.

## Ops

Expose `/version`; health endpoints (`/health`, `/alive`) mapped wszystkie environments dla K8s/cloud probes. Structured Serilog JSON + OpenTelemetry, same jako Web app.
