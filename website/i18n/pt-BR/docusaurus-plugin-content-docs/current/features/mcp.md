---
description: "cMind fornece servidor Model Context Protocol (MCP) como processo/Implantação separada — escale + reimplante independentemente do aplicativo web. Exponha cBot, instância, ferramentas de IA…"
---

# Servidor MCP

cMind fornece servidor Model Context Protocol (MCP) como **processo/Implantação separada** — escale + reimplante independentemente do aplicativo web. Exponha cBot, instância, ferramentas de IA para clientes MCP (ex. assistentes de IA) via transporte HTTP + SSE.

## Autenticação

- Chaves de API por usuário `mcpk_<hex>`, SHA-256 hash, índice de prefixo (`McpKeyAuthHandler`). Gerencie a partir da página **MCP** (`McpApiKey` agregado).
- Transporte HTTP sem estado com `AddHttpContextAccessor` — chamadas de ferramentas rodam como usuário autenticado.

## Ferramentas

- `CBotTools` — autor / construir cBots.
- `InstanceTools` — executar / backtest / inspecionar instâncias.
- `AiTools` — gerar, revisar, sentimento, analisar-backtest, ferramentas de cópia.

## Operações

Exponha `/version`; endpoints de saúde (`/health`, `/alive`) mapeados todos os ambientes para investigações K8s/nuvem. JSON Serilog estruturado + OpenTelemetry, mesmo que o aplicativo web.
