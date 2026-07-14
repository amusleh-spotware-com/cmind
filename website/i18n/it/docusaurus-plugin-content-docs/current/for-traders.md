---
slug: /for-traders
title: cMind per trader cTrader
description: Perché un trader cTrader dovrebbe self-host cMind — possiedi il tuo stack e dati, autore, backtest, esecuzione e monitoraggio di cBots in una console potenziata da IA, sul tuo laptop, VPS o telefono.
keywords:
  - cTrader
  - algorithmic trading
  - piattaforma di trading self-hosted
  - backtesting di cBot
  - bot di trading IA
  - software di trading open source
sidebar_position: 5
---

# cMind per trader cTrader 📈

Fai già trading su cTrader. Giocolari già un editor di codice, un backtester, un VPS, e tre
schede del browser. **cMind collassa tutto ciò in una sola console scura e keyboard-friendly che esegui
tu stesso** — ed è open source, quindi nulla del tuo edge, delle tue strategie, o delle tue credenziali
esce mai dalla tua scatola.

:::tip[TL;DR]
Self-host cMind su un laptop, un VPS economico, o un server domestico. Autore, backtest, esecuzione e monitoraggio di cBots
in un unico posto, con un core IA che fa i lavori. → [Eseguilo in 5 minuti](./deployment/local.md)
:::

## Perché self-host invece di un servizio ospitato?

- **Possiedi il tuo stack e i tuoi dati.** I tuoi cBots, credenziali, token, e storia dell'equity vivono su
  **la tua** infrastruttura — nessuna terza parte, nessun lock-in, nessuna email "stiamo tramontando questo prodotto".
- **È veramente tuo da modificare.** C# 14 / .NET 10, rigoroso DDD, EF Core + PostgreSQL, un server MCP
  — tutto open source e hackabile. Fork, estendi, invia una PR.
- **Nessun paywall per-feature.** Porta la tua propria chiave IA per qualsiasi provider; ogni feature IA è attiva.

Preferisci non gestire i server tu stesso? Un'azienda di hosting può gestire un cMind gestito per te —
vedi [Per provider cloud e VPS](./for-cloud-providers.md).

## Una console, nessun juggling di schede

- **Autore** in un vero IDE Monaco (l'editor VS Code), con template C# **e** Python e
  `dotnet build` sandboxed in container monouso. → [Build & backtest](./features/build-and-backtest.md)
- **Backtest** tra una flotta di nodi e guarda le curve di equity tornare live.
- **Esegui** strategie live e **monitorale** da un unico dashboard. → [Dashboard](./features/dashboard.md)
- **Copia** un account master su molti account tra broker e cTrader ID, con riconciliazione
  che sopravvive alle connessioni cadute e ai token ruotati. → [Copy trading](./features/copy-trading.md)

## IA che fa lavori, non chiacchierata

Porta la tua propria chiave API (qualsiasi provider supportato — cloud o un modello locale) e ottieni plain-English → un
cBot compilato reale con un ciclo di auto-riparazione, sintonizzazione di parametri, post-mortem di backtest, e un risk
guard che può auto-stoppare un bot che si comporta male. → [Conosci il core IA](./features/ai.md)

## Strumenti di grado istituzionale, per uno

Lo stesso rigore che una scrivania paga, sulla tua scatola:

- [Integrità di backtest](./features/backtest-integrity.md) · [Dimensionamento di posizione](./features/position-sizing.md)
- [Salute della strategia](./features/strategy-health.md) · [Regime lab](./features/regime-lab.md)
- [TCA di esecuzione](./features/execution-tca.md) · [Diario di trading](./features/trading-journal.md)
- [Agent Studio](./features/agent-studio.md) · [Posizionamento contrarian](./features/contrarian-positioning.md)

## Funziona dove lo fai tu

Inizia sul tuo laptop con `docker compose up`, passa a un VPS economico o un server domestico quando sei
pronto, e controlla i tuoi bot dal tuo telefono — cMind è un [PWA](./features/pwa.md) installabile e mobile-first.
→ [Eseguilo localmente](./deployment/local.md)

Vuoi che il tuo client IA lo guidi? C'è un [server MCP](./features/mcp.md) integrato.

## Aiuta a renderlo migliore

cMind è open source e con licenza MIT — la roadmap è plasmata dalla comunità:

- Archivia problemi e richieste di feature, e vota su ciò che conta.
- Aggiungi template cBot, adattatori di provider IA, o traduzioni di UI.
- Invia PR — tre livelli di test (unit + integrazione + E2E) e rigoroso DDD mantengono l'asticella alta, e la
  [Guida Contribuente](./contributing.md) ti guida attraverso.

Pronto? → [Leggi l'intro](./intro.md) quindi [eseguilo localmente](./deployment/local.md).
