---
description: "cMind ship Model Context Protocol (MCP) server as separate process/Deployment — scale + redeploy independent of Web app. Expose cBot, instance, AI tools…"
---

# MCP server

cMind ship Model Context Protocol (MCP) server as **separate process/Deployment** — scale + redeploy independent of Web app. Expose cBot, instance, AI tools to MCP clients (e.g. AI assistants) over HTTP + SSE transport.

## Auth

- Per-user API keys `mcpk_<hex>`, SHA-256 hashed, prefix-indexed (`McpKeyAuthHandler`). Manage from **Mcp** page (`McpApiKey` aggregate).
- Stateless HTTP transport with `AddHttpContextAccessor` — tool calls run as authed user.

## Tools

- `CBotTools` — author / build cBots.
- `InstanceTools` — run / backtest / inspect instances.
- `AiTools` — generate, review, sentiment, analyze-backtest, copy tools.

## Ops

Expose `/version`; health endpoints (`/health`, `/alive`) mapped all environments for K8s/cloud probes. Structured Serilog JSON + OpenTelemetry, same as Web app.