---
description: "Agent Studio — crea agenti di trading senza codice guidati da persona, con carattere e archetipo che gestiscono account verso i tuoi obiettivi sotto l'Autonomy & Safety Kernel (envelope di rischio, circuit breaker, kill switch, consenso disclaimer versionato)."
---

# Agent Studio

Agent Studio ti permette di creare un **agente di trading con carattere** — senza codice — e dargli la gestione dei
tuoi account verso obiettivi misurabili. Un agente è come un cBot guidato da personalità: scegli un archetipo
e un'attitudine, imposti le guardrail, e gira sotto l'**Autonomy & Safety Kernel**.

Apri **AI → Agent Studio** (`/agent-studio`).

## Crea un agente

Il dialogo **Nuovo agente** raccoglie, senza codice:

- **Nome** e **archetipo** — Scalper, Day Trader, Swing Trader, Position Trader, News Trader,
  Contrarian, Mean Reversion o Breakout/Momentum. Ogni preset fissa una cadenza e postura sensata.
- **Attitudine** — sliders di aggressività, pazienza e trend-following.
- **Livello di autonomia** — **Advisory** (solo propone) oppure **Approval-gated** (agisce solo dopo la tua
  approvazione per-azione). **Full Auto** (nessuna approvazione per-trade) richiede in più un **risk envelope**
  e accettazione del disclaimer di rischio prima di potersi armare.

La persona compila **deterministicamente** nel system prompt dell'agente (nessun LLM lo scrive), così la
stessa configurazione produce sempre le stesse istruzioni — riproducibile e verificabile.

## La rosa

Ogni agente mostra in una tabella control-room: **quale agente, il suo tipo, quanti account gestisce, i suoi
obiettivi, stato di esecuzione, e ultima azione**, con controlli **Start / Stop / Kill**. Il Kill switch ferma un
agente in esecuzione immediatamente.

## La sicurezza è un invariante di dominio, non un'impostazione

Tutto ciò che tocca soldi passa attraverso l'**Autonomy & Safety Kernel**:

- **Risk envelope** — limiti hard per-ordine (max daily loss, esposizione aperta, dimensione posizione, leverage,
  perdite consecutive, ordini/ora, simboli permessi). Ogni ordine è validato contro di esso prima del dispatch;
  una violazione è rifiutata, non clampata. Richiesta prima che un agente possa raggiungere Full Auto.
- **Circuit breaker** — ferma deterministicamente nuovo rischio su una serie di perdite, violazione daily-loss, una **violazione
  hard di performance-goal**, oppure **indisponibilità del provider AI** (un modello down o in allucinazione non apre
  mai posizioni fresche).
- **Versioned disclaimer consent** — un consenso one-time e versionato è richiesto per armare Full Auto
  (consenso richiesto legalmente, non approvazione per-trade); aggiornare il disclaimer forza il re-consenso.
- **Kill switch** — halt emergency idempotente su ogni agente in esecuzione.

## Obiettivi

Dai a un agente **obiettivi misurabili** — es. *mantieni max drawdown sotto il 4%*, *profit factor almeno
1.5*, *win rate ≥ 55%*. Ogni target è **Hard** (una guardrail — una violazione fa scattare il circuit breaker) oppure
**Soft** (influenza il ragionamento solo), valutato come On-track / At-risk / Breached.

## La pipeline decisionale

Una volta avviato, un agente gira un **loop supervisionato 24/7** (`AgentRuntimeService`). Ogni tick, per ogni
account gestito, legge lo **stato deterministico dell'account** (ground truth, mai la memoria del modello);
chiede al decision engine una mossa; la passa attraverso la **safety gate** (`AgentDecisionProcessor`) —
autonomy level → circuit breaker → risk envelope; scrive un **`AgentDecisionRecord`** append-only; e
ferma o esegue come la gate dirige. Il loop è **fault-isolated** (il fallimento di un agente non tocca
mai un altro o l'host) e **safe by default**: è inerte a meno che AI sia configurato *e*
`App:Ai:AgentRuntimeEnabled` sia impostato, e non apre mai rischio fresco mentre il provider AI è indisponibile.

- **Approval gate** — l'ordine proposto da un agente **Approval-gated** è registrato come **Pending** e non
  fa nulla finché il proprietario non lo approva (`POST /api/agent-studio/{id}/decisions/{seq}/approve` o
  `/reject`); **Full Auto** passa attraverso l'envelope senza approvazione per-trade; **Advisory** solo
  propone.
- **Audit ledger** — ogni decisione è riproducibile: ragionamento (XAI), le evidenze citate, il verdetto della gate,
  l'intento dell'ordine e se è stato eseguito, a `GET /api/agent-studio/{id}/decisions`.
- **Research desk** — un dibattito multi-agente on-demand: analisti Alpha/Sentiment/Technical/Risk danno
  ciascuno una view e un Reviewer sintetizza una proposta (`POST /api/agent-studio/{id}/debate`).
- **Memory** — l'agente ricorda ogni decisione e richiama la memoria recente nel suo prossimo prompt per
  continuità (`GET /api/agent-studio/{id}/memory`).

Il **Details** di ogni riga della rosa apre il feed decisionale dell'agente (con Approve/Reject sugli ordini pending),
la sua memoria, e un tab Run-debate.

## Ambito

Spedito: ciclo di vita completo dell'agente, la safety gate deterministica, il runtime 24/7, l'approval gate
human-in-the-loop, l'audit ledger, e l'**integrazione live cTrader Open API** — l'account-state store
(legge balance reale, posizioni e esposizione aperta in lotti) e l'executor di ordini (piazzamento di ordini market
reali, lotti→volume tramite lot size del simbolo), entrambi che risolvono le credenziali OAuth di ogni account
gestito e degradano in sicurezza quando un account non è collegato. **Richiede la chiave API Anthropic** per il modello
per generare ordini (fino ad allora il motore tiene); ancora da venire sono i ruoli del dibattito multi-agente e
memoria stratificata/reflection. Il runtime è off a meno che `App:Ai:AgentRuntimeEnabled` sia impostato, quindi
il trading live succede solo su un opt-in esplicito e fully-consented.

## Account gestiti e modifica

Quando crei un agente scegli gli account di trading che gestisce (richiesto prima che possa avviarsi).
Ogni agente può essere **modificato** successivamente (nome, temperamento, autonomia e account gestiti) dall'icona
matita sulla sua riga nella rosa. I controlli del ciclo di vita (details, edit, start, stop, kill) sono pulsanti
icona, ciascuno disabilitato negli stati dove l'azione non si applica.
