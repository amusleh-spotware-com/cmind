---
description: "cMind dodáva Model Context Protocol (MCP) server ako samostatný proces/Deployment — škála + redeploy nezávislý Web app. Vystavujú cBot, inštancia, AI nástroje…"
---

# MCP server

cMind dodáva Model Context Protocol (MCP) server ako **samostatný proces/Deployment** — škála + redeploy nezávislý Web app. Vystavujú cBot, inštancia, AI nástroje MCP klientom (napr. AI asistenti) cez HTTP + SSE transport.

## Auth

- Per-user API kľúče `mcpk_<hex>`, SHA-256 hashed, prefix-indexed (`McpKeyAuthHandler`). Spravovať z **Mcp** stránka (`McpApiKey` agregát).
- Bezstavový HTTP transport s `AddHttpContextAccessor` — tool volá spustiť ako auth používateľ.

## Nástroje

- `CBotTools` — autor / stavebný cBots.
- `InstanceTools` — spustiť / backtest / inšpekt inštancie.
- `AiTools` — generovať, recenzovať, sentiment, analyze-backtest, kopírovať nástroje.

## Ops

Vystavujú `/version`; health koncové body (`/health`, `/alive`) mapované všetky prostredia na K8s/cloud sondy. Štruktúrovaný Serilog JSON + OpenTelemetry, rovnako ako Web app.
