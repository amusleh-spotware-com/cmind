---
description: "cMind versendet Model Context Protocol (MCP) Server als separaten Prozess/Bereitstellung — Skalierung + Neubereitstellung unabhängig von Web-App. Verfügbar machen cBot, Instance, KI-Tools…"
---

# MCP-Server

cMind versendet Model Context Protocol (MCP) Server als **separaten Prozess/Bereitstellung** — Skalierung + Neubereitstellung unabhängig von Web-App. Verfügbar machen cBot-, Instance-, KI-Tools an MCP-Clients (z. B. KI-Assistenten) über HTTP + SSE-Transport.

## Authentifizierung

- Pro-Benutzer API-Tasten `mcpk_<hex>`, SHA-256 gehasht, Präfix-indexiert (`McpKeyAuthHandler`). Verwaltung von **Mcp**-Seite (`McpApiKey` Aggregate).
- Statuslose HTTP-Transport mit `AddHttpContextAccessor` — Tool-Aufrufe laufen als authed Benutzer.

## Tools

- `CBotTools` — Autor / Build cBots.
- `InstanceTools` — Run / Backtest / Inspiziere Instances.
- `AiTools` — generieren, überprüfen, Stimmung, Backtest analysieren, Copy-Tools.

## Ops

Verfügbar machen `/version`; Gesundheit Endpoints (`/health`, `/alive`) kartiert alle Umgebungen für K8s/Cloud Sonden. Strukturiert Serilog JSON + OpenTelemetry, gleich wie Web-App.
