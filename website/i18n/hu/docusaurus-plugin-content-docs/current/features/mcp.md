---
description: "Az MCP szerver az AI-ügyfelekhez az cMind-ben az eszközöket teszi elérhetővé — Antropic-kontextus-protokollon keresztül. Az szerzői, backtest, másolási, AI és compliance-eszközök érvényes JWT megkövetelve."
---

# MCP szerver

Az MCP szerver az AI-ügyfelekhez az cMind-ben az eszközöket teszi elérhetővé. Az OpenAI-kompatibilis vagy a Antropic-kliens a `http://localhost:8081/mcp` vagy egy felügyelt telepítésre csatlakozhat.

## Eszközök

| Eszköz | Leírás | Auth |
|--------|--------|------|
| `cbots:list` | Az felhasználó cBot-jait sorolja meg. | JWT |
| `cbots:get` | Az cBot részleteit naplózza. | JWT |
| `instances:backtest` | Az backtest-munkákat indít. | JWT |
| `instances:list` | Az felhasználó futtatási/backtest-munkáit sorolja meg. | JWT |
| `copy:profile:create` | Az másolási profilt hoz létre. | JWT |
| `copy:execution` — | Az másolási végrehajtás részleteit naplózza. | JWT |
| `ai:*` — | Az AI-kérdéseket küld — valuta-erő, stratégia-elemzés stb. | JWT |
| `compliance:audit-log` | Az kezelő auditcsapása. | JWT (Owner) |

## Hitelesítés

Az összes MCP-eszköz érvényes HS256 JWT megköveteli az MCP-kulcsok alól (`/settings/mcp-keys` az UI-ban vagy `POST /api/mcp-keys` az API-ban).

## Tesztek

- **Integráció** — `IntegrationTests/McpTests.cs`: az MCP-eszközök az érvényes JWT-vel a várt eredményt adják vissza.
- **E2E** — `E2ETests/McpE2ETests.cs`: a Playground az MCP-kéréseket az kliens-ből küldi.
