---
title: 0005 — Il client AI utilizza HTTP grezzo, non l'SDK Anthropic
description: Perché IAiClient chiama l'API Anthropic su un HttpClient tipizzato invece dell'SDK ufficiale e perché l'AI è completamente gated su una chiave.
---

# 0005 — Il client AI utilizza HTTP grezzo, non l'SDK Anthropic

## Contesto

Ogni funzione di IA (generazione di strategie, auto-riparazione, protezione del rischio, post-mortem) chiama l'API Anthropic. Una dipendenza SDK aggiunge una superficie transitiva che non controlliamo, accoppia il nostro ciclo di rilascio al loro e nasconde il contratto di filo esatto di cui abbiamo bisogno per ragionare sulla resilienza e sui costi.

## Decisione

`IAiClient` chiama Anthropic su **HTTP grezzo** attraverso un `HttpClient` tipizzato — deliberatamente **non** l'SDK. `AiFeatureService` è il singolo orchestratore condiviso dai Web endpoint, dagli `AiTools` MCP e da `AiRiskGuard`. L'intera superficie è **gated su `AppOptions.Ai.ApiKey`**: senza una chiave, ogni funzione restituisce `AiResult.Fail` e l'app viene eseguita invariata.

## Conseguenze

- Nessuna chiave è richiesta per build, test o E2E — CI e dev locale eseguono l'app completa senza AI.
- Possediamo esplicitamente la forma della richiesta/risposta, la politica di retry/timeout e la contabilità dei token.
- Le nuove funzioni Anthropic devono essere cablate a mano; scambiamo comodità per controllo e una superficie di dipendenza più piccola. Vedere il riferimento `claude-api` per i modelli e i parametri attuali.
