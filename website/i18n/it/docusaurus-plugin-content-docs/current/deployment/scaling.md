---
description: "cMind scala orizzontalmente con minimo sforzo dell'operatore. Due carichi di lavoro stateful — esecuzione run/backtest, copy-trading — usano entrambi il database come punto di coordinamento, quindi…"
---

# Scaling orizzontale

cMind scala orizzontalmente con minimo sforzo dell'operatore. Due carichi di lavoro stateful — esecuzione run/backtest,
copy-trading — usano entrambi il database come punto di coordinamento, quindi aggiungere repliche non richiede
nessun coordinatore esterno (no ZooKeeper, no leader election).

## Copy-trading (lease auto-guarente)

Ogni nodo esegue `CopyEngineSupervisor` (gated su `App:Copy:Enabled`). Ogni ciclo di reconcile,
il supervisor:

1. **Claim** ogni profilo in esecuzione non assegnato *oppure* lease scaduto, in un singolo `UPDATE` atomico —
   due supervisor in competizione non ottengono mai lo stesso profilo, quindi il profilo è copiato da esattamente un
   nodo (no ordini doppi).
2. **Rinnova** il lease sui profili che hosta.
3. Hosta i profili assegnati, spinge le rotazioni token di accesso all'host in esecuzione in-place (nessun
   drop dell'event-stream).

Crash nodo → smette di rinnovare; una volta passato `App:Copy:LeaseTtl`, qualsiasi nodo sopravvissuto reclama
i suoi profili al ciclo successivo, ricostruisce lo stato da reconcile senza duplicare trades. **Scaling
out** = aggiungere repliche; profili non assegnati/liberi raccolti automaticamente.

**Scale-in graceful / rolling update (S1)** = su `SIGTERM`, `CopyEngineSupervisor.StopAsync`
**rilascia i lease di questo nodo** (`AssignedNode`/`LeaseExpiresAt` → null) così il sopravvissuto li reclama
al *molto successivo* ciclo di reconcile — **non** dopo il `LeaseTtl` completo. Solo hard crash aspetta il TTL.
Il `terminationGracePeriodSeconds` del copy-agent (default 30) dà tempo al rilascio di finire prima del pod killed.

### Manopole (`App:Copy`)

| Impostazione | Default | Note |
|---------|---------|-------|
| `Enabled` | `false` | Attiva l'hosting copy per il nodo. |
| `ReconcileInterval` | `30s` | Quanto spesso il nodo claim/renew/reconciles. |
| `LeaseTtl` | `120s` | Grazia prima che i profili del nodo silenzioso siano reclamati. Mantieni qualche intervallo di reconcile così un ciclo lento non causa hand-off spurio. |
| `NodeName` | nome macchina | Impostare distintamente quando due supervisor condividono un host. |

Su Kubernetes i supervisor copy girano come Deployment; impostare `replicas` al parallelismo desiderato. Ogni
pod ottiene `NodeName` stabile (default: hostname pod), così i lease attribuiti per pod. Il database è
la singola source of truth — no sticky sessions, no stato per-pod da migrare.

**Distribuzione bilanciata (S4):** impostare `App:Copy:MaxProfilesPerNode` > 0 per limitare quanti profili in esecuzione
un nodo hosta. Ogni supervisor poi claim **al massimo** il suo spazio rimanente tramite claim atomico
`FOR UPDATE SKIP LOCKED` bounded, così i profili si **distribuiscono** tra le repliche invece che il primo
supervisor prendere tutto — no single hot pod / SPOF. Il claim skip-locked mantiene la garanzia "esattamente un nodo
per profilo" (no double-hosting) anche sotto claim concorrenti. `0` (default) = unbounded (un nodo hosta tutto, invariato).

**A scala (S7/S8):** ogni pod jitterizza reconcile fino al 20% di `ReconcileInterval`
(`CopyEngineSupervisor.JitteredInterval`) così N repliche non sparano `UPDATE` di claim/renew
simultaneamente (Postgres thundering-herd). Quando `copyAgent.replicas > 1` la chart distribuisce anche
le repliche tra i nodi (`topologySpreadConstraints`) e aggiunge `PodDisruptionBudget` (`minAvailable: 1`)
così drain/upgrade non porta mai la capacità copy a zero.

## Run/backtest execution

`NodeScheduler` sceglie il nodo eleggibile meno caricato rispettando `MaxInstances`; gli agenti nodo remoti
si auto-registrano e heartbeat (`App:Discovery`), `NodeHeartbeatMonitor` marca il nodo unreachable
quando l'heartbeat supera `Discovery:HeartbeatTtl`. Aggiungere agenti nodo per aggiungere capacità di esecuzione;
l'agente morto instradato automaticamente.

## Migrations su scale-out / rolling deploy

Ogni replica Web/MCP esegue `OwnerSeeder` all'avvio, che applica le EF migrations e seeded il proprietario.
Per rendere ciò sicuro quando N repliche partono insieme, migrate + seed girano dentro un **Postgres session
advisory lock** (`MigrationLock.RunExclusiveAsync`, key `DatabaseDefaults.MigrationAdvisoryLockKey`):
la prima replica ad acquisirlo migra e seeded; le altre bloccano sul lock, poi trovano le migrate
già applicate (no-op) e il proprietario già presente. Nessun job di migrazione separato o leader election necessario.
Se aggiungi un seeding first-run, mettilo **dentro** lo stesso block guarded così è single-writer.

## Node-agent HTTP resilience

Il nodo main parla a ogni agente `CtraderCliNode` su HTTP attraverso tre client purpose-split così un
nodo flaky o rete non corrompe mai lo stato:

- **read** (`status` / `report` / `stats`) — GET idempotenti, ritentati su fallimenti transitori
  (exponential backoff + jitter, `NodeAgentHttp.ReadRetryCount`) con timeout per tentativo e totale.
- **write** (`start` / `stop` / `clean`) — POST non-idempotenti, in timeout ma **mai ritentati**:
  un `start` ritentato potrebbe lanciare doppio container.
- **stream** (`logs`) — lo stream `docker logs -f` long-lived ottiene timeout infinito e nessuna
  pipeline di resilience, così il tailing non viene mai tagliato.

Un nodo che resta unreachable è gestito da heartbeat + [reclamo istanza orfana](../operations/node-discovery.md);
il layer HTTP only ammorbidisce blip transitori.

## Tier stateless

Web (Blazor Server + API) e MCP server sono stateless dietro database, replicabili liberamente.
L'auth è cookie-based; scalare Web orizzontalmente dietro load balancer. MCP server è processo/Deployment
separato così scala indipendentemente da Web.

## Database connection resilience

Ogni host che apre il database usa una **retrying execution strategy** così una disconnessione transitoria
o un failover managed-Postgres (RDS / Flexible Server patching) viene ritentato invece di emergere come errore all'utente:

- Web e MCP registrano il contesto attraverso il componente Npgsql Aspire con `DisableRetry=false`
  e un `CommandTimeout` esplicito (`DatabaseDefaults.CommandTimeoutSeconds`).
- CopyAgent (non-Aspire) registra tramite `UseAppNpgsql`, che applica lo stesso
  `EnableRetryOnFailure(MaxRetryCount, MaxRetryDelay)` + command timeout da `DatabaseDefaults`.

Tutti i write sono singoli `SaveChanges` / singoli `ExecuteUpdate` / singole `ExecuteSql` statements, quindi la
retrying strategy è sicura (nessuna transazione multi-statement necessita wrapping `strategy.ExecuteAsync`
manuale). Se aggiungi una transazione manuale o multipli `SaveChanges` in un'operazione logica, wrap
in `db.Database.CreateExecutionStrategy().ExecuteAsync(...)` — altrimenti lancia sotto retry.

## Checklist per scaling out

- [ ] Postgres dimensionato per carico di connessione aggiunto (ogni replica Web/MCP/nodo apre un pool).
- [ ] `App:Copy:Enabled=true` su ogni nodo che dovrebbe hostare profili copy.
- [ ] `App:Copy:NodeName` distinto per supervisor co-locati (K8s: default per-pod ok).
- [ ] `LeaseTtl` ≥ 3× `ReconcileInterval`.
- [ ] Agenti nodo deployati dove Docker privileged disponibile (AKS/EKS/EC2/VM, non Fargate).
- [ ] Multi-replica Web: impostare la stringa di connessione `signalr` (Redis backplane) **e** abilitare
      session affinity all'ingresso (sticky sessions) così un circuit Blazor si riconnette a un pod live. Un'eccezione
      componente è catturata dal `MainLayout` `ErrorBoundary` (retry amichevole, circuit resta vivo).
