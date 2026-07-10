# MCP server

cMind ships a Model Context Protocol (MCP) server as a **separate process/Deployment**, so it
scales and redeploys independently of the Web app. It exposes cBot, instance, and AI tools to MCP
clients (e.g. AI assistants) over an HTTP + SSE transport.

## Auth

- Per-user API keys of the form `mcpk_<hex>`, SHA-256 hashed and prefix-indexed
  (`McpKeyAuthHandler`). Manage keys from the **Mcp** page (`McpApiKey` aggregate).
- Stateless HTTP transport with `AddHttpContextAccessor`, so tool calls run as the authenticated
  user.

## Tools

- `CBotTools` — author / build cBots.
- `InstanceTools` — run / backtest / inspect instances.
- `AiTools` — generate, review, sentiment, analyze-backtest, and copy tools.

## Ops

Exposes `/version`; health endpoints (`/health`, `/alive`) are mapped in all environments for
K8s/cloud probes. Structured Serilog JSON + OpenTelemetry, same as the Web app.
