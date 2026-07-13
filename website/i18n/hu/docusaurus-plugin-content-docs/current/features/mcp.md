---
title: MCP szerver
description: "Az MCP (Model Context Protocol) szerver a cMind eszközeit HTTP + SSE-n keresztül elérhetővé teszi AI kliensek számára. A szerver minden funkciót tool-ként tesz közzé."
---

# MCP szerver

Az MCP (Model Context Protocol) szerver a cMind eszközeit HTTP + SSE-n keresztül elérhetővé teszi AI kliensek számára. A szerver minden funkciót tool-ként tesz közzé.

## Protokoll

Az MCP egy egyszerű JSON-RPC 2.0 wrapper HTTP + Server-Sent Events felett:
- **POST** `/mcp` - JSON-RPC request-ek
- **GET** `/mcp/stream` - SSE stream (server → kliens események)

## Elerheto tool-ok

### Kereskedes

| Tool | Paraméterek | Leiras |
|------|-------------|--------|
| `place_order` | `symbol`, `side`, `quantity`, `type`, `price` | Megbízás elhelyezése |
| `cancel_order` | `orderId` | Megbízás törlése |
| `amend_order` | `orderId`, `price`, `quantity` | Megbízás módosítása |
| `get_positions` | `accountId` | Nyitott pozíciók lekérése |
| `get_orders` | `accountId` | Aktív megbízások lekérése |

### cBots

| Tool | Paraméterek | Leiras |
|------|-------------|--------|
| `list_cbots` | - | cBot-ok listázása |
| `get_cbot` | `id` | cBot részletek |
| `build_cbot` | `id` | cBot fordítása |
| `run_cbot` | `id`, `parameters` | cBot futtatása |
| `backtest_cbot` | `id`, `config` | cBot backtest-elése |

### Analitika

| Tool | Paraméterek | Leiras |
|------|-------------|--------|
| `get_backtest_result` | `instanceId` | Backtest eredmények |
| `get_position_sizing` | `returns`, `targetVol` | Pozíció méretezés |
| `get_integrity_lab` | `instanceId` | Backtest integritás |

### AI

| Tool | Paraméterek | Leiras |
|------|-------------|--------|
| `ask_ai` | `prompt`, `context` | AI kérdés |
| `analyze_journal` | `instanceId` | Napló elemzés |

## Konfiguracio

```json
{
  "App": {
    "Mcp": {
      "Enabled": true,
      "Port": 5100
    }
  }
}
```

## HTTP endpoint

```
POST http://localhost:5100/mcp
Content-Type: application/json

{
  "jsonrpc": "2.0",
  "method": "tools/call",
  "params": {
    "name": "place_order",
    "arguments": {
      "symbol": "EURUSD",
      "side": "Buy",
      "quantity": 100_000
    }
  },
  "id": 1
}
```

## Kapcsolodo

- **[AI funkciók](./ai.md)**
- **[Build és Backtest](./build-and-backtest.md)**
- **[Position Sizing](./position-sizing.md)**
