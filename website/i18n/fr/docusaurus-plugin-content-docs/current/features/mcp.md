---
description: "cMind livre le serveur Model Context Protocol (MCP) comme processus/déploiement séparé — mise à l'échelle + redéploiement indépendants de l'app Web. Exposez les outils cBot, instance, IA…"
---

# Serveur MCP

cMind livre le serveur Model Context Protocol (MCP) comme **processus/déploiement séparé** — mise à l'échelle + redéploiement indépendants de l'app Web. Exposez les outils cBot, instance, IA aux clients MCP (par ex. assistants IA) sur le transport HTTP + SSE.

## Auth

- Clés API par utilisateur `mcpk_<hex>`, SHA-256 hashées, index préfixe (`McpKeyAuthHandler`). Gérer depuis la page **Mcp** (agrégat `McpApiKey`).
- Transport HTTP apatride avec `AddHttpContextAccessor` — les appels d'outils s'exécutent en tant qu'utilisateur authentifié.

## Outils

- `CBotTools` — auteur / construire cBots.
- `InstanceTools` — exécuter / backtester / inspecter les instances.
- `AiTools` — générer, réviser, sentiment, analyser-backtest, outils de copie.

## Ops

Exposez `/version` ; endpoints de santé (`/health`, `/alive`) mappés tous les environnements pour les sondes K8s/cloud. JSON Serilog structuré + OpenTelemetry, identique à l'app Web.
