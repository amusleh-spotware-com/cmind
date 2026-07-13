---
description: "Το cMind αποστέλλει Model Context Protocol (MCP) server ως ξεχωριστή διεργασία/Deployment — κλιμάκωση και επαναφορά ανεξάρτητα από την Web εφαρμογή. Εκθέτει cBot, instance, εργαλεία AI σε MCP clients (π.χ. AI assistants) μέσω HTTP + SSE transport."
---

# MCP server

Το cMind αποστέλλει Model Context Protocol (MCP) server ως **ξεχωριστή διεργασία/Deployment** — κλιμάκωση και επαναφορά ανεξάρτητα από την Web εφαρμογή. Εκθέτει cBot, instance, εργαλεία AI σε MCP clients (π.χ. AI assistants) μέσω HTTP + SSE transport.

## Auth

- API keys ανά χρήστη `mcpk_<hex>`, κατακερματισμένα με SHA-256, ευρετηριασμένα κατά πρόθεμα (`McpKeyAuthHandler`). Διαχειριστείτε από τη σελίδα **Mcp** (`McpApiKey` aggregate).
- Stateless HTTP transport με `AddHttpContextAccessor` — οι κλήσεις εργαλείων εκτελούνται ως ο authed χρήστης.

## Εργαλεία

- `CBotTools` — συγγραφή / build cBots.
- `InstanceTools` — εκτέλεση / backtest / επιθεώρηση instances.
- `AiTools` — δημιουργία, ανασκόπηση, sentiment, analyze-backtest, εργαλεία copy.

## Ops

Εκθέτει `/version`; health endpoints (`/health`, `/alive`) αντιστοιχίζονται σε όλα τα περιβάλλοντα για K8s/cloud probes. Structured Serilog JSON + OpenTelemetry, ίδια με την Web εφαρμογή.