---
title: 0004 — CBotBuilder viene eseguito sull'host web in un contenitore sandbox
description: Perché i build di cBot non attendibili avvengono sull'host web all'interno di un contenitore SDK monouso piuttosto che su un nodo.
---

# 0004 — `CBotBuilder` viene eseguito sull'host web in un contenitore sandbox

## Contesto

La compilazione del cBot di un utente significa eseguire **MSBuild non attendibile** — codice arbitrario al momento della compilazione (target, generatori di sorgenti, script di ripristino). Ha bisogno del socket Docker per far girare un contenitore SDK. I nodi eseguono contenitori di trading e non dovrebbero avere anche privilegi di compilazione.

## Decisione

`CBotBuilder` viene eseguito **sull'host web** (che già ha il socket Docker), all'interno di un **contenitore SDK monouso** con:

- una directory `/work` bind-montata (solo gli input/output della compilazione, non il filesystem dell'host);
- un volume condiviso `app-nuget-cache` per le prestazioni di ripristino;
- nessun accesso alla rete dell'host oltre a quello necessario per il ripristino.

Quindi l'MSBuild non attendibile non può raggiungere il filesystem dell'host o la rete. I contenitori di esecuzione/backtest, al contrario, vengono eseguiti su nodi scelti da `NodeScheduler`.

## Conseguenze

- Il privilegio di compilazione (socket Docker) è confinato all'host web; i nodi eseguono solo immagini di trading consentite.
- Ogni compilazione è isolata in un contenitore eliminabile — una compilazione dannosa non può persistere o sfuggire.
- L'host web deve avere un socket Docker disponibile; questo è un requisito di distribuzione, non opzionale.
