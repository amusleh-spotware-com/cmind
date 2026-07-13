---
description: "cMind ship Model Context Protocol (MCP) server as separate process/Deployment — scale + redeploy independent of Web app. Expose cBot, instance, AI tools…"
---

# MCP server

Το cMind έχει Model Context Protocol (MCP) server ως **separate process/Deployment** — scale + redeploy independent του Web app. Εκθέστε cBot, instance, AI tools στο MCP clients (π.χ. AI assistants) πάνω HTTP + SSE transport.

## Auth

- Per-user API keys `mcpk_<hex>`, SHA-256 hashed, prefix-indexed (`McpKeyAuthHandler`). Διαχειριστείτε από **Mcp** page (`McpApiKey` aggregate).
- Stateless HTTP transport με `AddHttpContextAccessor` — tool calls τρέχουν ως authed user.

## Tools

- `CBotTools` — author / build cBots.
- `InstanceTools` — run / backtest / inspect instances.
- `AiTools` — generate, review, sentiment, analyze-backtest, copy tools.

## Ops

Εκθέστε `/version`; health endpoints (`/health`, `/alive`) mapped όλα environments για K8s/cloud probes. Structured Serilog JSON + OpenTelemetry, ίδιο ως Web app.
