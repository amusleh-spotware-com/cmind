---
description: "cMind ship Model Context Protocol (MCP) server เป็น separate process/Deployment — scale + redeploy independent ของ Web app Expose cBot instance AI tools…"
---

# MCP server

cMind ship Model Context Protocol (MCP) server เป็น **separate process/Deployment** — scale + redeploy independent ของ Web app Expose cBot instance AI tools ไป MCP clients (เช่น AI assistants) ผ่าน HTTP + SSE transport

## Auth

- Per-user API keys `mcpk_<hex>` SHA-256 hashed prefix-indexed (`McpKeyAuthHandler`) Manage จาก **Mcp** page (`McpApiKey` aggregate)
- Stateless HTTP transport ด้วย `AddHttpContextAccessor` — tool calls รัน เป็น authed user

## Tools

- `CBotTools` — author / build cBots
- `InstanceTools` — run / backtest / inspect instances
- `AiTools` — generate review sentiment analyze-backtest copy tools

## Ops

Expose `/version`; health endpoints (`/health` `/alive`) mapped ทั้งหมด environments สำหรับ K8s/cloud probes Structured Serilog JSON + OpenTelemetry เหมือนกับ Web app
