---
description: "cMind ship Model Context Protocol (MCP) server như separate process/Deployment — scale + redeploy independent của Web app. Expose cBot, instance, AI tools…"
---

# MCP server

cMind ship Model Context Protocol (MCP) server như **separate process/Deployment** — scale + redeploy independent của Web app. Expose cBot, instance, AI tools tới MCP clients (ví dụ AI assistants) qua HTTP + SSE transport.

## Auth

- Per-user API keys `mcpk_<hex>`, SHA-256 hashed, prefix-indexed (`McpKeyAuthHandler`). Manage từ **Mcp** page (`McpApiKey` aggregate).
- Stateless HTTP transport với `AddHttpContextAccessor` — tool calls chạy như authed user.

## Tools

- `CBotTools` — author / build cBots.
- `InstanceTools` — run / backtest / inspect instances.
- `AiTools` — generate, review, sentiment, analyze-backtest, copy tools.

## Ops

Expose `/version`; health endpoints (`/health`, `/alive`) mapped tất cả environments cho K8s/cloud probes. Structured Serilog JSON + OpenTelemetry, same như Web app.
