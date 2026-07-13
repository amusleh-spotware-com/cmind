---
description: "MCP server je HTTP+SSE povrch cMind — nástrojů pro AI klienty k řízení cBotů, copy tradingu, alertů, a dalších. Wire kompatibilní s Claude a ostatní AI."
---

# MCP server

cMind hostuje **MCP (Model Context Protocol) server** — HTTP+SSE povrch nad API a doménové logiky. Umožňuje AI klientům (Claude, ostatní) volat nástrojů k řízení backtestů, copy tradingu, alertů a dalších bez vědoucího SSH/pověření.

## Co je na serveru

Nástrojů zahrnuté se přihlašují k feature flags a role:

- **cBot tools** (`CBotTools`): list/create/build/run cBots, přístup param sady
- **Backtest tools** (`InstanceTools`): list/create backtesty, monitor běhy, přístup reports
- **Copy tools** (`CopyTools`): list/create/manage copy profily (feature `CopyTrading`)
- **Prop-firm tools** (`PropFirmTools`): list/create/manage challenges (feature `PropFirm`)
- **AI tools** (`AiTools`): řízení AI agenta, přístup AI funkcím (feature `Ai`)
- **Alert tools** (`AlertTools`): list/create rules, subscribe ke events
- **Economic calendar tools** (`CalendarTools`): browse events, check blackout windows

Všechny nástrojů jsou JWT-autentizované; client vydaný cMind API.

## Autorství & client
Endpointy: `POST /api/mcp-keys` (vytvoř klíč s jménem + role), `GET /api/mcp-keys` (seznam), `DELETE /api/mcp-keys/{id}` (zrevokov).

Server běží na `/mcp` (HTTP) a `/mcp/events` (SSE pro events streaming).

## Testy

- **Unit** — nástrojů transformace, parametr validace
- **Integration** — server konektivita, auth, nástrojů volání přes HTTP
- **E2E** — full nástrojů workflows z AI klienta

Viz [MCP spec](https://spec.modelcontextprotocol.io/).
