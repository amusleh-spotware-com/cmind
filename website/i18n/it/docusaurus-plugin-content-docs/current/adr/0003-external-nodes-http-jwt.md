---
title: 0003 — I nodi cTrader CLI sono HTTP + JWT, senza SSH/shell
description: Perché gli agenti dei nodi remoti espongono solo un'API HTTP con JWT di breve durata e mai una shell.
---

# 0003 — I nodi cTrader CLI sono HTTP + JWT, senza SSH/shell

## Contesto

I contenitori di backtest/esecuzione si eseguono su host remoti. L'approccio ovvio — SSH e esegui docker — dà all'app principale un'esecuzione di codice remoto arbitraria e credenziali di lunga durata su ogni nodo. Questo è un raggio di esplosione grande per un sistema che esegue cBot non attendibili dell'utente.

## Decisione

Ogni host remoto esegue un **agente HTTP** standalone `CtraderCliNode` con **niente SSH e niente shell**. L'app principale chiama l'agente su HTTP; ogni richiesta porta un **JWT HS256** di breve durata (5 minuti, `iss=app-main` / `aud=app-node`) firmato con il segreto di quel nodo. L'agente:

- esegue solo immagini corrispondenti a `AllowedImagePrefix` (con un confine di percorso in modo che `ghcr.io/spotware` non possa corrispondere a `ghcr.io/spotware-evil/...`);
- esegue docker tramite `ArgumentList` — mai una stringa di shell;
- è **stateless**, trovando i contenitori per etichetta `app.instance`;
- si auto-registra e invia heartbeat a `POST /api/nodes/register`; l'app principale fa upsert del `CtraderCliNode` **per nome**, quindi un nodo sopravvive ai cambiamenti IP.

## Conseguenze

- Un token di richiesta trapelato scade in pochi minuti; non c'è alcuna credenziale di shell permanente da rubare.
- La capacità dell'agente è limitata a "esegui un'immagine consentita" — non può essere trasformata in una shell remota generale.
- L'identità del nodo è basata sul nome, quindi il re-provisioning di un nodo con un nuovo IP non orfanizza la sua cronologia.
