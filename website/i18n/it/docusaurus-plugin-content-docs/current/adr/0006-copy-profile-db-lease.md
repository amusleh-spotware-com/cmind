---
title: 0006 — L'hosting delle copie è coordinato da un lease DB atomico
description: Perché i profili di copia sono rivendicati tramite un lease Postgres atomico invece di un coordinatore dedicato e come ciò impedisce la doppia copia.
---

# 0006 — L'hosting delle copie è coordinato da un lease DB atomico

## Contesto

Un profilo di copia in esecuzione deve essere ospitato da **esattamente un** nodo — due host sullo stesso profilo significa che ogni scambio di fonte viene rispecchiato due volte (denaro reale perso). I nodi vanno e vengono (scalabilità, arresti anomali, aggiornamenti continui) e non vogliamo un servizio coordinatore separato per funzionare e rimanere attivo.

## Decisione

Ogni `CopyEngineSupervisor` rivendica i profili con un **lease DB atomico** sulla tabella `CopyProfiles`:

- **Rivendicazione** — un `ExecuteUpdate` atomico (o `FOR UPDATE SKIP LOCKED` quando si limita per nodo) prende i profili che non sono assegnati *oppure* il cui lease è scaduto. L'atomicità significa che due supervisor in gara non rivendicano mai la stessa riga.
- **Rinnovo** — un nodo attivo aggiorna il suo lease ogni ciclo, quindi mantiene la sua rivendicazione.
- **Riacquisizione** — il lease di un nodo si arresta in modo anomalo scade e un sopravvissuto prende il profilo al ciclo successivo (auto-guarigione). All'arresto normale, il nodo **rilascia** immediatamente i suoi lease in modo che il failover sia veloce.
- **Watchdog** — un host il cui compito è stato interrotto mentre il profilo è ancora nostro viene riavviato.
- La riconciliazione è casuale per evitare un branco di `UPDATE` fragorosi in scala.

## Conseguenze

- Nessun coordinatore autonomo da distribuire o mantenere in salute — Postgres è l'unica fonte di verità.
- La doppia copia è impedita dall'atomicità a livello di riga, non dal blocco a livello di applicazione.
- La latenza di failover è delimitata dal TTL del lease (meno il percorso veloce di rilascio normale).
- Questo è il percorso del denaro; è protetto dalla suite di stress deterministica (DST) — non indebolire mai uno scenario DST per farlo passare.
