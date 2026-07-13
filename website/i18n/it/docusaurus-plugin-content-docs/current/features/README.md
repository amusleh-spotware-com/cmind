---
slug: /features
title: Funzionalità — il tour completo
description: Tutto quello che cMind può fare — copy trading, AI, build & backtest, prop-firm guards, white-label, PWA, MCP, e altro ancora.
sidebar_label: Panoramica
---

# Funzionalità — il tour completo 🧭

Benvenuto al grand tour. cMind ha *molte* funzionalità in una sola app, quindi qui c'è la mappa. Ogni capacità
ha il suo documento di approfondimento — clicca su qualunque cosa ti interessi.

## 🔁 Copy trading

Il gioiello della corona. Specchia un conto master su molti, e tienili sincronizzati anche quando internet
si comporta male.

- **[Copy trading](./copy-trading.md)** — il core: mirroring, order types, SL/TP, slippage, desync/resync.
- **[Trasparenza dell'esecuzione](./copy-execution-transparency.md)** — vedi esattamente cosa è stato copiato, quando, e perché.
- **[Commissioni di performance](./copy-performance-fees.md)** — addebbita per il tuo segnale, stile high-water-mark.
- **[Marketplace dei provider](./copy-provider-marketplace.md)** — consenti ai trader di scoprire e seguire i provider.
- **[Notifiche](./copy-notifications.md)** — ricevi notifiche quando qualcosa ha bisogno di te.
- **[Raccomandazione IA per il copy](./ai-copy-recommender.md)** — lascia che l'IA suggerisca chi copiare.
- **[Ciclo di vita dei token Open API](./token-lifecycle.md)** — come cMind mantiene esattamente un token valido per cID.

## 📊 La tua base

- **[Dashboard](./dashboard.md)** — il centro di controllo live e mobile-first: KPI con sparkline, un grafico di attività, un anello di stato, un feed live, e (per gli admin) lo stato del cluster. Si aggiorna da solo.

## 🧠 Core IA

Non una chat box attaccata al lato — IA che effettivamente *fa il lavoro*.

- **[Assistente IA, agente, risk guard e alert](./ai.md)** — generazione di strategie, build auto-riparanti, un risk guard in background che può auto-stoppare i bot, e alert intelligenti.

## 🛠️ Build & run

- **[Build & backtest cBots](./build-and-backtest.md)** — l'IDE Monaco nel browser, template C#/Python, build sandboxed, e curve di equity live.
- **[Server MCP](./mcp.md)** — esponi gli strumenti di cMind su HTTP + SSE così i client IA possono guidarlo.

## 🏢 Gestiscilo come un business

- **[White-label / branding](./white-label.md)** — rebrand ogni superficie via config.
- **[Simulazione prop-firm challenge](./prop-firm.md)** — applica regole di daily-loss, drawdown, e target con equity live.
- **[Feature toggles](./feature-toggles.md)** — decidi cosa vede ogni deployment/tenant.
- **[Compliance / legal](./compliance.md)** — l'audit trail e la superficie legale.

## 📱 L'esperienza

- **[App installabile (PWA)](./pwa.md)** — mobile-first, shell offline, add-to-home-screen.
- **[Sistema di design UI & mobile-first](../ui-guidelines.md)** — i token di design e le regole dietro l'aspetto.

## ⚙️ Sotto il cofano

I bit operativi che mantengono tutto in funzione:

- **[Fleet di nodi & discovery](../operations/node-discovery.md)** — come i nodi si auto-registrano e si guariscono.
- **[Scaling orizzontale](../deployment/scaling.md)** — aggiungi repliche, nessun coordinatore esterno necessario.
- **[Logging & audit](../operations/logging.md)** — log strutturati + OpenTelemetry.
- **[Deployment](../deployment/local.md)** — fallo funzionare ovunque.

:::note Mantenere i documenti onesti
Ogni documento di funzionalità è tenuto in sincronia con il codice — cambia il comportamento, aggiorna il doc, stesso
commit. Se noti mai un disallineamento, è un bug: per favore
[apri un issue](https://github.com/amusleh-spotware-com/cmind/issues/new/choose) o invia una PR. 🙏
:::
