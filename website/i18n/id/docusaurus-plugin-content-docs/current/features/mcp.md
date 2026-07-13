---
description: "MCP server — tool AI untuk client AI eksternal (Claude, dll.) mengakses data dan kontrol trading."
---

# MCP Server

MCP server — tool AI untuk client AI eksternal (Claude, dll.) mengakses data dan kontrol trading.

## Apa itu MCP?

Model Context Protocol (MCP) adalah protokol untuk menghubungkan AI dengan tools dan data.
cMind menyediakan MCP server yang mengekspos data dan kontrol trading ke AI client.

## Endpoint

```
/mcp  (HTTP + SSE)
```

Server berjalan sebagai service terpisah di belakang ALB/Nginx.

## Authentication

### Bearer Token

```bash
curl -H "Authorization: Bearer <token>" http://localhost:5000/mcp
```

### API Key

```bash
curl -H "x-api-key: <api-key>" http://localhost:5000/mcp
```

## Tools

### Trading

| Tool | Deskripsi |
|------|-----------|
| `get_accounts` | Daftar semua akun trading |
| `get_positions` | Posisi terbuka |
| `place_order` | Tempatkan order |
| `close_position` | Tutup posisi |
| `modify_order` | Ubah stop loss / take profit |

### Market Data

| Tool | Deskripsi |
|------|-----------|
| `get_quotes` | Quote real-time |
| `get_ohlcv` | OHLCV data |
| `get_symbols` | Daftar simbol yang tersedia |
| `search_symbols` | Cari simbol |

### cBot

| Tool | Deskripsi |
|------|-----------|
| `list_cbots` | Daftar cBot |
| `start_cbot` | Mulai cBot |
| `stop_cbot` | Stop cBot |
| `get_cbot_status` | Status cBot |

### Copy Trading

| Tool | Deskripsi |
|------|-----------|
| `list_copy_profiles` | Daftar profil copy |
| `create_copy_profile` | Buat profil baru |
| `recommend_copy_profile` | AI rekomendasikan pengaturan |

### Backtest

| Tool | Deskripsi |
|------|-----------|
| `list_backtests` | Daftar backtest |
| `run_backtest` | Jalankan backtest |
| `get_backtest_result` | Hasil backtest |

## Contoh

### Claude Desktop

```json
// claude_desktop_config.json
{
  "mcpServers": {
    "cmind": {
      "command": "curl",
      "args": ["-s", "http://localhost:5000/mcp"]
    }
  }
}
```

### Penggunaan

```
You: What are my current positions?

Claude: Let me check that for you.
→ get_positions()

Result: [
  { "symbol": "EURUSD", "direction": "buy", "lots": 0.5, "pnl": 125.50 },
  { "symbol": "XAUUSD", "direction": "sell", "lots": 0.1, "pnl": -45.00 }
]
```

## Streaming

MCP mendukung SSE (Server-Sent Events) untuk streaming real-time:

```javascript
const eventSource = new EventSource('/mcp/stream?token=<token>');

eventSource.onmessage = (event) => {
  const data = JSON.parse(event.data);
  // Handle streaming data
};
```

## Error Handling

| Error Code | Arti |
|------------|------|
| 401 | Unauthorized |
| 403 | Forbidden |
| 404 | Resource not found |
| 429 | Rate limited |
| 500 | Internal error |

## Rate Limits

| Tier | Requests/minute |
|------|----------------|
| Free | 10 |
| Pro | 60 |
| Enterprise | 600 |
