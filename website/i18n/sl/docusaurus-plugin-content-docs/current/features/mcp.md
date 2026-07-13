---
description: "cMind oddaja strežnik protokola modelnega konteksta (MCP) kot ločena postopek/Nameščanje — lestvica + ponovno namestiti neodvisno od spletne aplikacije. Razkrijte orodja cBota, instanci, AI..."
---

# Strežnik MCP

cMind oddaja strežnik Model Context Protocol (MCP) kot **ločeno postopek/Nameščanje** —
lestvica + ponovno namestiti neodvisno od spletne aplikacije. Razkrijte orodja cBota, instanci,
AI odjemalcem MCP (npr. pomočniki AI) čez transport HTTP + SSE.

## Avtentizacija

- Ključi API po uporabnika `mcpk_<hex>`, SHA-256 heširani, indeks predpone (`McpKeyAuthHandler`).
  Upravljajte iz strani **Mcp** (agregat `McpApiKey`).
- Brezstansko prevozne HTTP z `AddHttpContextAccessor` — gradniki klicev tečejo kot avtentizirani
  uporabnik.

## Orodja

- `CBotTools` — avtorski ustvarit / graditi cBote.
- `InstanceTools` — tečeni / testirani / pregledati instanci.
- `AiTools` — generirati, pregledi, sentiment, analiza-testiranje, kopiranje orodja.

## Ops

Razkrijte `/version`; končne točke zdravja (`/health`, `/alive`) preslikane vse okoljene za
K8s/oblačne sonde. Strukturirani Serilog JSON + OpenTelemetry, enako kot spletna aplikacija.
