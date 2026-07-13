---
title: Architecture Decision Records
description: Le decisioni di progettazione non ovvie dietro cMind — contesto, decisione e conseguenze — che non puoi desumere dal codice.
---

# Architecture Decision Records

Questi registri documentano le decisioni di progettazione che **non puoi desumere dal codice** — i compromessi, i percorsi non intrapresi e il motivo. Ciascuno è breve: *Contesto → Decisione → Conseguenze*. Nuova decisione strutturale → aggiungi un ADR qui (numero successivo) in modo che il prossimo ingegnere (umano o AI) erediti il ragionamento, non solo il risultato.

| # | Decisione |
|---|---|
| [0001](./0001-strict-ddd-pure-core.md) | DDD rigoroso con un `Core` puro |
| [0002](./0002-tph-instance-replaces-entity.md) | Lo stato dell'istanza è TPH; una transizione sostituisce l'entità |
| [0003](./0003-external-nodes-http-jwt.md) | I nodi cTrader CLI sono HTTP + JWT, senza SSH/shell |
| [0004](./0004-cbotbuilder-on-web-host.md) | `CBotBuilder` viene eseguito sull'host web in un contenitore sandbox |
| [0005](./0005-anthropic-raw-http.md) | Il client AI utilizza HTTP grezzo, non l'SDK Anthropic |
| [0006](./0006-copy-profile-db-lease.md) | L'hosting delle copie è coordinato da un lease DB atomico |
