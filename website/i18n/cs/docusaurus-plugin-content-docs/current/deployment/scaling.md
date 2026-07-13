---
description: "cMind se škáluje ven s minimálním úsilím operátora. Dva stavové workloady — run/backtest execution, copy-trading — oba používají databázi jako koordinační bod, takže…"
---

# Horizontální škálování

cMind se škáluje ven s minimálním úsilím operátora. Dva stavové workloady — run/backtest
execution, copy-trading — oba používají databázi jako koordinační bod, takže přidávání replik nevyžaduje
žádný externí koordinátor (žádný ZooKeeper, žádná volba leadera).

## Copy-trading (samo-hojící lease)

Každý node běží `CopyEngineSupervisor` (gated na `App:Copy:Enabled`). Každý reconcile cyklus,
supervisor:

1. **Claimuje** každý běžící profil nepřiřazený *nebo* s propadlou lease, v jedné atomické `UPDATE` —
   dva závodící supervisoři nikdy neclaimují stejný profil, takže profil je kopírován přesně jedním
   nodem (žádné duplicitní objednávky).
2. **Obnovuje** lease na profilech, které hostuje.
3. Hostuje přiřazené profily, pushuje rotace access-tokenů k běžícímu hostovi in-place (žádný
   event-stream drop).

Node crash → přestane obnovovat; jakmile uplyne `App:Copy:LeaseTtl`, jakýkoliv přeživší node reclaimuje
jeho profily v dalším cyklu, rebuilduje stav z reconcile bez duplikování obchodů. **Škálování
ven** = přidávejte repliky; nepřiřazené/volné profily jsou automaticky picked up.
**Graceful scale-in / rolling update (S1)** = na `SIGTERM`, `CopyEngineSupervisor.StopAsync`
**uvolní tento node's leases** (`AssignedNode`/`LeaseExpiresAt` → null) takže survivor reclaimed je
jeho *velmi další* reconcile cyklus — *ne* po plném `LeaseTtl`. Pouze hard crash čeká TTL.
Copy-agent's `terminationGracePeriodSeconds` (default 30) dává release čas dokončit před smrtí
pod.

### Knoby (`App:Copy`)

| Nastavení | Default | Poznámky |
|---------|---------|---------|
| `Enabled` | `false` | Zapne copy hosting pro node. |
| `ReconcileInterval` | `30s` | Jak často node claimuje/obnovuje/reconciles. |
| `LeaseTtl` | `120s` | Grace před tím, než jsou tichý node's profily reclaimed. Držte několik reconcile intervalů, aby pomalý cyklus nezpůsobil falešné předávání. |
| `NodeName` | machine name | Nastavte odlišně když dva supervisoři sdílejí hosta. |

Na Kubernetes copy supervisoři běží jako Deployment; nastavte `replicas` na požadovanou paralelizaci. Každý
pod dostane stabilní `NodeName` (default: pod hostname), takže leases jsou attribuovány per pod. Databáze je
single source of truth — žádné sticky sessions, žádný per-pod stav k migraci.

**Vyvážená distribuce (S4):** nastavte `App:Copy:MaxProfilesPerNode` > 0 pro cap kolik běžících
profilů node hostuje. Každý supervisor pak claimuje **nejvýše** svůj zbývající headroom přes atomickou
`FOR UPDATE SKIP LOCKED` bounded claim, takže profily se **šíří** mezi repliky místo prvního
supervisora grabujícího všechny — žádný single hot pod / SPOF. Skip-locked claim zachovává "přesně jeden node
per profil" záruku (žádné double-hosting) i při konkurenčních claimech. `0` (default) =
unbounded (jeden node hostuje vše, nechanged).

**Ve velkém měřítku (S7/S8):** každý pod jitteruje reconcile až o 20% `ReconcileInterval`
(`CopyEngineSupervisor.JitteredInterval`) takže N replik neodpaluje claim/renew `UPDATE`
současně (Postgres thundering-herd). Když `copyAgent.replicas > 1` chart také šíří
repliky across nodes (`topologySpreadConstraints`) a přidává `PodDisruptionBudget` (`minAvailable: 1`)
takže drain/upgrade nikdy nevezme copy kapacitu na nulu.

## Run/backtest execution

`NodeScheduler` vybírá nejméně zatížený eligible node s respektem k `MaxInstances`; remote node agenti
se sami registrují a heartbeat (`App:Discovery`), `NodeHeartbeatMonitor` označí node unreachable
když heartbeat přesáhne `Discovery:HeartbeatTtl`. Přidejte node agenty pro přidání execution kapacity;
mrtvý agent je automaticky obejit.

## Migrace při scale-out / rolling deploy

Každá Web/MCP replica běží `OwnerSeeder` při startu, který aplikuje EF migrace a seeduje ownera.
Pro bezpečnost když N replik startuje najednou, migrate + seed běží inside **Postgres session
advisory lock** (`MigrationLock.RunExclusiveAsync`, key `DatabaseDefaults.MigrationAdvisoryLockKey`):
první replica, která ho získá, migruje a seeduje; zbytek blokuje na lock, pak zjistí že migrace
již aplikovány (no-op) a owner již present. Žádná samostatná migration job nebo leader election není
potřebná. Pokud přidáváte first-run seeding, dejte ho **inside** stejný guarded block takže je single-writer.

## Node-agent HTTP resilience

Main node mluví s každým `CtraderCliNode` agentem přes HTTP přes tři účelově rozdělené klienty takže
vadný node nebo síť nikdy nepoškodí stav:

- **read** (`status` / `report` / `stats`) — idempotentní GETs, retry na transient failures
  (exponential backoff + jitter, `NodeAgentHttp.ReadRetryCount`) s per-attempt a total timeouts.
- **write** (`start` / `stop` / `clean`) — non-idempotent POSTs, timeout ale **nikdy neretry**: a
  retried `start` by mohl double-launchnout container.
- **stream** (`logs`) — dlouho běžící `docker logs -f` stream má infinite timeout a žádný
  resilience pipeline, takže tailing není nikdy cut off.

Node který zůstává unreachable je handled by heartbeat + [orphaned-instance reclaim](../operations/node-discovery.md);
HTTP vrstva pouze vyhlazuje transient blips.

## Bezestavové vrstvy

Web (Blazor Server + API) a MCP server jsou bezestavové za databází, replikují se freely.
Auth je cookie-based; škálujte Web horizontálně za load balancer. MCP server je samostatný
process/Deployment takže škáluje nezávisle na Web.

## Database connection resilience

Každý host, který otevře databázi, používá **retrying execution strategy** takže transient
disconnect nebo managed-Postgres failover (RDS / Flexible Server patching) je retryován instead of
surfacing jako error uživateli:

- Web a MCP registrují context přes Aspire Npgsql component s `DisableRetry=false`
  a explicit `CommandTimeout` (`DatabaseDefaults.CommandTimeoutSeconds`).
- CopyAgent (non-Aspire) registruje přes `UseAppNpgsql`, které aplikuje stejnou
  `EnableRetryOnFailure(MaxRetryCount, MaxRetryDelay)` + command timeout from `DatabaseDefaults`.

Všechny zápisy jsou single `SaveChanges` / single `ExecuteUpdate` / single `ExecuteSql` statements, takže
retrying strategy je bezpečná (žádný multi-statement transaction nepotřebuje manuální `strategy.ExecuteAsync`
wrapping). Pokud přidáte manuální transaction nebo multiple `SaveChanges` v jedné logické operaci, wrapujte
to v `db.Database.CreateExecutionStrategy().ExecuteAsync(...)` — jinak to throw under retry.

## Checklist pro škálování ven

- [ ] Postgres dimensionován pro přidanou connection load (každá Web/MCP/node replica otevírá pool).
- [ ] `App:Copy:Enabled=true` na každém nodu, který by měl hostovat copy profily.
- [ ] Distinct `App:Copy:NodeName` per co-located supervisor (K8s: default per-pod fine).
- [ ] `LeaseTtl` ≥ 3× `ReconcileInterval`.
- [ ] Node agenti nasazeni kde privilegovaný Docker dostupný (AKS/EKS/EC2/VM, ne Fargate).
- [ ] Multi-replica Web: nastavte `signalr` connection string (Redis backplane) **a** enable ingress
      session affinity (sticky sessions) takže Blazor circuit se reconnectuje na live pod. A component
      exception je chycena `MainLayout` `ErrorBoundary` (friendly retry, circuit stays alive).
