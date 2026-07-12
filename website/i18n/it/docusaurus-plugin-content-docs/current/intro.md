---
slug: /intro
title: Benvenuto in cMind
description: Un'introduzione amichevole a cMind — la piattaforma di trading operations per cTrader, open source e self-hostable.
sidebar_position: 1
---

# Benvenuto in cMind 👋

Quindi vuoi creare bot di trading, fare backtest senza far fondere il portatile, eseguirli su più
macchine, replicare le operazioni su una dozzina di conti e avere un'IA che tiene d'occhio il rischio
mentre dormi. **Sei esattamente nel posto giusto.**

cMind è una **piattaforma di trading operations per cTrader, open source e self-hostable**. Immaginala
come l'intero tuo trading desk — creazione, esecuzione, una flotta di calcolo, copy trading e un nucleo
di IA — racchiusa in un'app calma, scura e adatta ai dispositivi mobili, che possiedi da cima a fondo.

:::tip In una frase
Costruisci → backtest → esegui → copia le tue strategie cTrader su larga scala, con l'IA integrata, sui
tuoi server e con il tuo marchio.
:::

## Cosa può fare davvero?

| Vuoi… | cMind lo fa | Approfondisci |
|---|---|---|
| Scrivere un cBot nel browser | IDE Monaco + template C#/Python, build in sandbox | [Costruire e backtest](./features/build-and-backtest.md) |
| Fare backtest su più macchine | Una flotta di nodi auto-riparante sceglie la macchina meno impegnata | [Scalabilità](./deployment/scaling.md) |
| Copiare un conto su molti | Replica robusta con risincronizzazione, senza operazioni doppie | [Copy trading](./features/copy-trading.md) |
| Lasciare all'IA il lavoro pesante | Generazione di strategie, auto-riparazione, guardia del rischio, post-mortem | [Nucleo di IA](./features/ai.md) |
| Restare dentro le regole della prop firm | Monitoraggio dell'equity in tempo reale + simulazione delle regole di challenge | [Prop-firm](./features/prop-firm.md) |
| Distribuirlo come *tuo* prodotto | White-label completo: nome, colori, logo, favicon | [White-label](./features/white-label.md) |
| Eseguirlo sul telefono | PWA installabile e mobile-first | [PWA](./features/pwa.md) |
| Pilotarlo da un client IA | Server MCP integrato (HTTP + SSE) | [MCP](./features/mcp.md) |

## Il percorso di 5 minuti ⏱️

Se hai Docker e cinque minuti, puoi mettere le mani su una vera istanza cMind proprio adesso:

```bash
git clone https://github.com/amusleh-spotware-com/cmind.git
cd cmind
cp .env.example .env        # set OWNER_EMAIL + OWNER_PASSWORD
docker compose up --build
```

Poi apri **<http://localhost:8080>**, accedi e sei pronto. La guida completa (con la risoluzione dei
problemi per quando Docker avrà inevitabilmente le sue opinioni) si trova in
**[Eseguirlo in locale](./deployment/local.md)**.

## Nuovo qui? Segui la strada di mattoni gialli 🟡

1. **[A chi è rivolto?](./audience.md)** — assicurati di essere il nostro tipo di guai.
2. **[Eseguirlo in locale](./deployment/local.md)** — avvia una vera istanza.
3. **[Funzionalità](./features/README.md)** — il tour completo di ciò che c'è dentro.
4. **[Distribuire sul serio](./deployment/cloud.md)** — Docker, Kubernetes, Azure, AWS.
5. **[Rendilo tuo](./white-label-for-business.md)** — applica il tuo white-label per la tua attività.
6. **[Contribuire](./contributing.md)** — i PR (umani *e* assistiti dall'IA) sono molto graditi.

## Due parole veloci sul denaro 💸

cMind muove **capitale reale**. Lo prendiamo sul serio — ogni modifica viene rilasciata con test
unitari, di integrazione ed end-to-end, percorsi di errore inclusi (connessioni cadute, ordini
rifiutati, nodi morti). Dovresti prenderlo sul serio anche tu: **prova prima su un conto demo** e leggi
le [note di conformità](./features/compliance.md) prima di puntarlo su qualcosa di reale. Il trading è
rischioso; questo software è uno strumento, non una consulenza finanziaria.

Bene — basta preamboli. Andiamo a costruire qualcosa. →
