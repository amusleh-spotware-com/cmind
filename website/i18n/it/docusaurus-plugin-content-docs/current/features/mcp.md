---
description: "cMind spedisce server Model Context Protocol (MCP) come processo/Deployment separato — scale + redeploy indipendente dalla Web app. Espone cBot, instance, AI tools…"
---

# MCP server

cMind spedisce Model Context Protocol (MCP) server come **processo/Deployment separato** — scale + redeploy
indipendente dalla Web app. Espone cBot, instance, AI tools a client MCP (es. AI assistants) su
HTTP + trasporto SSE.

## Auth

- API keys per-user `mcpk_<hex>`, SHA-256 hashed, prefix-indexed (`McpKeyAuthHandler`). Gestisci dalla
  pagina **Mcp** (`McpApiKey` aggregate).
- Trasporto HTTP stateless con `AddHttpContextAccessor` — le chiamate tool girano come utente authed.

## Tools

- `CBotTools` — author / build cBots.
- `InstanceTools` — run / backtest / inspect instances.
- `AiTools` — generate, review, sentiment, analyze-backtest, copy tools.

## Ops

Esponi `/version`; endpoint health (`/health`, `/alive`) mappati tutti gli ambienti per probe
K8s/cloud. Serilog JSON strutturato + OpenTelemetry, stesso della Web app.
