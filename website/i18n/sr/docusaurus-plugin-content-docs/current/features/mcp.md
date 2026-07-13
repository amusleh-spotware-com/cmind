---
description: "cMind isporučuje Model Context Protocol (MCP) server kao zaseban proces/Deployment — skaliranje + redploy nezavisno od Web aplikacije. Izlaže cBot, instance, AI alate MCP klijentima."
---

# MCP server

cMind isporučuje Model Context Protocol (MCP) server kao **zaseban proces/Deployment** — skaliranje + redploy nezavisno od Web aplikacije. Izlaže cBot, instance, AI alate MCP klijentima (npr. AI asistenti) preko HTTP + SSE transporta.

## Auth

- Per-user API keys `mcpk_<hex>`, SHA-256 hash-ovani, prefix-indeksirani (`McpKeyAuthHandler`). Upravlja se sa **Mcp** stranice (`McpApiKey` agregat).
- Stateless HTTP transport sa `AddHttpContextAccessor` — tool pozivi se pokreću kao authed korisnik.

## Alati

- `CBotTools` — autor / gradnja cBot-ova.
- `InstanceTools` — pokretanje / testiranje unatrag / inspekcija instanci.
- `AiTools` — generisanje, pregled, sentiment, analyze-backtest, copy alati.

## Ops

Izlaže `/version`; health endpoint-i (`/health`, `/alive`) mapirani sva okruženja za K8s/cloud probe. Structured Serilog JSON + OpenTelemetry, isto kao Web aplikacija.
