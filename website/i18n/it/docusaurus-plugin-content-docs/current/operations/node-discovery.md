---
description: "I nodi cTrader CLI si uniscono al cluster mediante auto-registrazione + heartbeat — nessuna voce manuale. Lo stesso pattern dei agenti Consul/Nomad/kubeadm: l'agente si avvia sapendo la posizione del nodo principale..."
---

# Auto-discovery del nodo

I nodi cTrader CLI si uniscono al cluster tramite **auto-registrazione + heartbeat** — nessuna voce manuale. Lo stesso pattern dei agenti Consul/Nomad/kubeadm: l'agente si avvia sapendo la posizione del nodo principale + segreto del cluster condiviso, quindi si annuncia continuamente.

> Verificato end-to-end su Docker Compose e cluster `kind` Kubernetes: gli agenti si auto-registrano, appaiono in DB raggiungibili, auto-marchiati non raggiungibili quando gli heartbeat si interrompono oltre TTL, tornano online quando ripresi.

## Come funziona

```
Agente CtraderCliNode                      Main (Web)
------------------                        ----------
POST /api/nodes/register  ── join token ──▶ verifica token (tempo costante)
  { name, baseUrl, mode,                   verifica versione protocollo
    maxInstances, dataDir,                  upsert CtraderCliNode per nome
    protocolVersion }                       marca LastHeartbeatAt, IsReachable=true
        ▲                                    └─ CtraderCliNode.SelfRegister / RecordHeartbeat
        │  ogni HeartbeatInterval           NodeHeartbeatMonitor (background):
        └──────────────────────────────────── se now - LastHeartbeatAt > HeartbeatTtl
                                                 → CtraderCliNode.MarkUnreachable() (NodeWentOffline)
```

- **Registrazione == heartbeat.** L'agente fa di nuovo POST su `HeartbeatIntervalSeconds`. La prima chiamata crea il nodo (`NodeRegistered` event); le chiamate successive aggiornano la liveness. L'heartbeat ripreso dopo l'interruzione capovolge il nodo di nuovo raggiungibile (`NodeCameOnline`).
- **Riconciliazione della liveness.** `NodeHeartbeatMonitor` contrassegna i nodi il cui ultimo heartbeat supera `HeartbeatTtl` come non raggiungibili. Lo scheduler (`IsActive`/`AcceptsRun`/`AcceptsBacktest` gated su raggiungibilità) smette di posizionare il lavoro fino a quando non segnalano di nuovo.
- **Reclame di istanze orfane.** `NodeInstanceReclaimer` (background) fa transizione di qualsiasi istanza non terminale bloccata su un nodo non raggiungibile a **Failed** (`FailureReason = "Node unreachable - instance reclaimed"`, evento di dominio `InstanceFailed` → notifica utente), quindi un nodo crashato/partizionato non può mai lasciare un'istanza bloccata "Running" per sempre. La reclama si attiva solo dopo che l'ultimo heartbeat del nodo è obsoleto oltre `HeartbeatTtl + InstanceReclaimGrace`, dando una breve interruzione una possibilità di recupero. I **run riclamati non vengono rischedulati automaticamente**: un nodo partizionato ma vivo potrebbe ancora eseguire il contenitore e non c'è recinzione a livello di contenitore, quindi il rilancio rischia l'esecuzione doppia — l'utente riavvia deliberatamente un run riclamato. I backtest si auto-escono, quindi un backtest riclamato viene semplicemente rieseguito.
- **L'identità è il nome del nodo.** Main fa upsert per `NodeName`, quindi il pod il cui IP/URL cambia al riavvio mantiene l'identità, si re-registra con nuovo `AdvertiseUrl`.
- **Mode fissato al primo registro.** La modalità del nodo (`Run`/`Backtest`/`Mixed`) è di tipo persistente, non può cambiare sull'heartbeat; la re-registrazione con modalità diversa onorate per la liveness ma il cambio di modalità ignorato (registrato come avviso). Per cambiare modalità: elimina il nodo, lascialo re-registrare.

## Configurazione

Main (Web) — `App:Discovery`:

| Chiave | Predefinito | Significato |
|-----|---------|---------|
| `Enabled` | `false` | Master switch per register endpoint + monitor. |
| `JoinToken` | — | Segreto del cluster condiviso (≥ 32 caratteri) che gli agenti devono presentare. |
| `HeartbeatTtl` | `00:01:30` | Grazia prima che il nodo silenzioso sia contrassegnato come non raggiungibile. |
| `InstanceReclaimGrace` | `00:01:00` | Margine aggiuntivo oltre `HeartbeatTtl` prima che un'istanza bloccata su un nodo non raggiungibile venga riclamata (fallita). |
| `MonitorInterval` | `00:00:30` | Frequenza con cui il monitor e il riclamatore di istanze spazzano. |
| `HeartbeatInterval` | `00:00:30` | Valore restituito agli agenti come cadenza suggerita. |

Agente (CtraderCliNode) — `NodeAgent`:

| Chiave | Significato |
|-----|---------|
| `MainUrl` | URL di base del nodo principale. Vuoto = modalità di registrazione manuale (loop no-op). |
| `AdvertiseUrl` | URL che main usa per raggiungere **questo** agente. |
| `NodeName` | Nome univoco; predefinito al nome della macchina se vuoto. |
| `Mode` | `Run` / `Backtest` / `Mixed`. |
| `MaxInstances` | Suggerimento di capacità onorato dallo scheduler. |
| `HeartbeatIntervalSeconds` | Cadenza di re-registrazione. |
| `JwtSecret` | Deve essere uguale al `JoinToken` di main — sia il bearer di registrazione che la chiave di firma JWT di dispatch. |

## Modello di sicurezza (v1)

I nodi auto-registrati condividono **un segreto del cluster** (`JoinToken` == `JwtSecret` di ogni agente). Main firma ogni richiesta di dispatch come JWT HS256 di 5 minuti con quel segreto; l'agente convalida. Requisiti:

- Mantieni `JoinToken` ≥ 32 caratteri e ruotalo (aggiorna `App:Discovery:JoinToken` di main e `NodeAgent:JwtSecret` di ogni agente insieme).
- Termina TLS davanti a main e agenti in produzione (proxy inverso / ingress).
- L'agente esegue ancora solo immagini corrispondenti a `AllowedImagePrefix`.

**Follow-up di hardening (non v1):** emetti un segreto univoco per nodo al registro (bootstrap in stile kubeadm → credenziale per nodo) in modo che un singolo agente compromesso non possa forgiare token di dispatch per i peer. Il flusso di registrazione restituisce già un corpo di risposta — posto naturale per restituire il segreto per nodo coniato.

## I nodi manuali funzionano ancora

`POST /api/nodes` (UI admin) continua a registrare i nodi fissati con il proprio segreto per nodo. La scoperta è additiva.

Una distribuzione white-label può **nascondere i controlli manuali** (o l'intera superficie dei nodi) e fare affidamento puramente su auto-discovery: `App:Branding:NodesUi=Monitor` rilascia add/delete manuale, `Hidden` rimuove la navigazione, la pagina e l'API manuale, e `App:Branding:RestrictNodesToOwner` pavimenta la superficie solo al proprietario. L'endpoint self-register + heartbeat qui non è interessato in nessuna modalità. Vedi [White-label → Visibilità dell'interfaccia utente dei nodi](../features/white-label.md#nodes-ui-visibility).
