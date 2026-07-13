---
description: "cMind sa škáluje von s minimálnym úsilím operátora. Dve stateful workloads — run/backtest execution, copy-trading — obe používajú databázu ako coordination point, takže…"
---

# Horizontálne škálovanie

cMind sa škáluje von s minimálnym úsilím operátora. Dve stateful workloads — run/backtest
execution, copy-trading — obe používajú databázu ako coordination point, takže pridávanie
replík nevyžaduje externý koordinátor (žiadny ZooKeeper, žiadne leader election).

## Copy-trading (self-healing lease)

Každý node beží `CopyEngineSupervisor` (gated na `App:Copy:Enabled`). Každý reconcile cyklus,
supervisor:

1. **Claims** každý bežiaci profil nepridelený *alebo* lease-lapsed, v jednom atómickom `UPDATE` —
   dva súbežné supervisor-y nikdy oba neclaimnu ten istý profil, takže profil kopíruje presne jeden
   node (žiadne dvojité objednávky).
2. **Renewsuje** lease na profiloch, ktoré hostuje.
3. Hostuje pridelené profily, pushuje access-token rotácie na bežiaci host na mieste (bez
   event-stream drop).

Node crash → prestane renewovať; akonáhle `App:Copy:LeaseTtl` prejde, akýkoľvek prežijúci node reclaimne
jeho profily v ďalšom cykle, rebuilduje stav z reconcile bez duplikovania obchodov. **Škálovanie
von** = pridať repliky; nepridelené/voľné profily picknuté automaticky.

**Graceful scale-in / rolling update (S1)** = na `SIGTERM`, `CopyEngineSupervisor.StopAsync`
**uvoľní tento node's leases** (`AssignedNode`/`LeaseExpiresAt` → null) takže prežijúci reclaimne ich
v jeho *veľmi ďalšom* reconcile cykle — **nie** po full `LeaseTtl`. Iba hard crash čaká TTL.
Copy-agent's `terminationGracePeriodSeconds` (predvolené 30) dáva release čas dokončiť pred
pod killed.

### Gombíky (`App:Copy`)

| Nastavenie | Predvolené | Poznámky |
|---------|---------|--------|
| `Enabled` | `false` | Zapnúť copy hosting pre node. |
| `ReconcileInterval` | `30s` | Ako často node claimuje/renewuje/reconciles. |
| `LeaseTtl` | `120s` | Grace pred tým, než tichý node's profily sú reclaimnuté. Nechajte niekoľko reconcile intervalov, takže pomalý cyklus nespôsobuje falošné hand-off. |
| `NodeName` | machine name | Nastavte distinctne keď dva supervisory zdieľajú host. |

Na Kubernetes copy supervisory bežia ako Deployment; nastavte `replicas` na požadovanú paralelnosť. Každý
pod dostáva stabilný `NodeName` (predvolené: pod hostname), takže leases sú pripisované per pod. Databáza je
single source of truth — žiadne sticky sessions, žiadny per-pod state na migráciu.

**Balanced distribution (S4):** nastavte `App:Copy:MaxProfilesPerNode` > 0 pre cap koľko bežiacich
profilov node hostuje. Každý supervisor potom claimuje **najviac** svoj zvyšný headroom cez atomický
`FOR UPDATE SKIP LOCKED` bounded claim, takže profily sa **spread** naprieč replikami namiesto prvého
supervisora grabujúceho všetky — žiadny single hot pod / SPOF. Skip-locked claim udržuje "presne jeden node
per profil" záruku (žiadne double-hosting) aj pod súbežných claimov. `0` (predvolené) =
unbounded (jeden node hostuje všetko, nezmenené).

**At scale (S7/S8):** každý pod jitteruje reconcile až o 20% z `ReconcileInterval`
(`CopyEngineSupervisor.JitteredInterval`) takže N replík nefire-uje súčasne claim/renew `UPDATE`
(Postgres thundering-herd). Keď `copyAgent.replicas > 1` chart tiež spreaduje
replicas naprieč nodes (`topologySpreadConstraints`) a pridáva `PodDisruptionBudget` (`minAvailable: 1`)
takže drain/upgrade nikdy neberie copy kapacitu na nulu.

## Run/backtest execution

`NodeScheduler` vybere najmenej zaťažený eligible node s rešpektovaním `MaxInstances`; remote node agenty
sa self-register a heartbeat (`App:Discovery`), `NodeHeartbeatMonitor` označí node unreachable
keď heartbeat prekročí `Discovery:HeartbeatTtl`. Pridajte node agenty pre pridanie execution kapacity;
mŕtvy agent obídený automaticky.

## Migrations pri scale-out / rolling deploy

Každá Web/MCP replica beží `OwnerSeeder` pri štarte, ktorá aplikuje EF migrácie a seedne ownera.
Aby to bolo bezpečné keď N replík štartuje naraz, migrate + seed beží inside a **Postgres session
advisory lock** (`MigrationLock.RunExclusiveAsync`, key `DatabaseDefaults.MigrationAdvisoryLockKey`):
prvá replica, ktorá ho získa, migruje a seeduje; ostatné block na lock, potom nájdu migrácie
už aplikované (no-op) a ownera už prítomné. Žiadne separátne migration job alebo leader election
nie je potrebné. Ak pridáte first-run seeding, vložte ho **inside** rovnaký guarded block takže je single-writer.

## Node-agent HTTP resilience

Hlavný node komunikuje s každým `CtraderCliNode` agentom cez HTTP cez tri purpose-split klienty tak, aby
chybý node alebo sieť nikdy neskorumpovala stav:

- **read** (`status` / `report` / `stats`) — idempotentné GETs, retries na tranzientných zlyhaniach
  (exponential backoff + jitter, `NodeAgentHttp.ReadRetryCount`) s per-attempt a total timeouts.
- **write** (`start` / `stop` / `clean`) — non-idempotent POSTs, timed out ale **nikdy neretried**: a
  retried `start` by mohol double-launchnúť kontajner.
- **stream** (`logs`) — dlhodobý `docker logs -f` stream dostáva infinite timeout a žiadnu
  resilience pipeline, takže tailing nikdy nie je cut off.

Node, ktorý zostáva unreachable, je handled heartbeatom + [orphaned-instance reclaim](../operations/node-discovery.md);
HTTP layer iba hladí tranzientné blips.

## Bezstavové vrstvy

Web (Blazor Server + API) a MCP server sú bezstavové za databázou, replikujú sa voľne.
Auth je cookie-based; škálujte Web horizontálne za load balancerom. MCP server je samostatný
process/Deployment takže škáluje nezávisle od Web.

## Database connection resilience

Každý host, ktorý otvára databázu, používa **retrying execution strategy** tak, aby tranzientné
odpojenie alebo managed-Postgres failover (RDS / Flexible Server patching) bolo retryované namiesto
surfacing ako chyba používateľovi:

- Web a MCP registrujú context cez Aspire Npgsql component s `DisableRetry=false`
  a explicitným `CommandTimeout` (`DatabaseDefaults.CommandTimeoutSeconds`).
- CopyAgent (non-Aspire) registruje cez `UseAppNpgsql`, čo aplikuje rovnaké
  `EnableRetryOnFailure(MaxRetryCount, MaxRetryDelay)` + command timeout z `DatabaseDefaults`.

Všetky writes sú single `SaveChanges` / single `ExecuteUpdate` / single `ExecuteSql` statements, takže
retrying strategy je bezpečná (žiadny multi-statement transaction nepotrebuje manuálny `strategy.ExecuteAsync`
wrapping). Ak pridáte manuálnu transakciu alebo viac `SaveChanges` v jednej logickej operácii, wrap
to v `db.Database.CreateExecutionStrategy().ExecuteAsync(...)` — inak to hodí pod retry.

## Checklist pre škálovanie von

- [ ] Postgres dimensionovaný pre pridanú connection load (každá Web/MCP/node replica otvára pool).
- [ ] `App:Copy:Enabled=true` na každom node, ktorý by mal hostovať copy profily.
- [ ] Distinct `App:Copy:NodeName` per co-located supervisor (K8s: predvolené per-pod je v pohode).
- [ ] `LeaseTtl` ≥ 3× `ReconcileInterval`.
- [ ] Node agenty deploynuté tam, kde je privileged Docker dostupný (AKS/EKS/EC2/VM, nie Fargate).
- [ ] Multi-replica Web: nastavte `signalr` connection string (Redis backplane) **a** povolte ingress
      session affinity (sticky sessions) tak, aby Blazor circuit reconnectoval k živému podu. Component
      exception je catched `MainLayout` `ErrorBoundary` (friendly retry, circuit stays alive).
