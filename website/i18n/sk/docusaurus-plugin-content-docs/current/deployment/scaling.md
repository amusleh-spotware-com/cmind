---
description: "cMind sa mení horizontálne s minimálnym operátorom úsilím. Dva stateful workloads — run/backtest execution, copy-trading — oba používajú databázu ako koordináciu bod, takže..."
---

# Horizontal scaling

cMind sa mení out s minimálnym operátorom úsilím. Dva stateful workloads — run/backtest
execution, copy-trading — oba používajú databázu ako coordination point, takže pridávanie replík potrebuje
žádny externý koordinátor (žádny ZooKeeper, žádna leader election).

## Copy-trading (self-healing lease)

Každý node spúšťa `CopyEngineSupervisor` (gated na `App:Copy:Enabled`). Každý reconcile cycle,
supervisor:

1. **Claims** každý running profile unassigned *alebo* lease-lapsed, v jednom atomic `UPDATE` —
   dva racing supervisory nikdy oba claim rovnaký profil, takže profil je kopírovaný presne jedným
   node (žádne double orders).
2. **Renews** lease na profiles it hosts.
3. Hosts assigned profiles, pushes access-token rotations na running host in place (žádny
   event-stream drop).

Node crash → zastaví renewing; keď `App:Copy:LeaseTtl` prejde, akýkoľvek surviving node reclaims
jeho profiles next cycle, rebuilds stav z reconcile bez duplicitných obchodov. **Scaling
out** = pridajte repliky; unassigned/free profiles picked up automaticky.

**Graceful scale-in / rolling update (S1)** = na `SIGTERM`, `CopyEngineSupervisor.StopAsync`
**releases this node leases** (`AssignedNode`/`LeaseExpiresAt` → null), takže survivor reclaims ich
jeho *veľmi ďalšia* reconcile cycle — **nie** po plnom `LeaseTtl`. Len hard crash čaká plný TTL.
Copy-agent `terminationGracePeriodSeconds` (default 30) dáva release time na finish pred
pod killed.

### Knobs (`App:Copy`)

| Nastavenie | Default | Poznámky |
|---------|---------|-------|
| `Enabled` | `false` | Zapnite copy hosting na node. |
| `ReconcileInterval` | `30s` | Ako často node claims/renews/reconciles. |
| `LeaseTtl` | `120s` | Grace pred silent node profiles reclaimed. Udržujte pár reconcile intervals takže pomalá cyklus nezpôsobí spurious hand-off. |
| `NodeName` | machine name | Nastavte distinctly keď dva supervisory share host. |

Na Kubernetes copy supervisory spúšťame ako Deployment; nastavte `replicas` na desired parallelism. Každý
pod dostáva stable `NodeName` (default: pod hostname), takže leases attributed per pod. Databáza je
single source of truth — žádne sticky sessions, žádny per-pod state na migrate.

**Balanced distribution (S4):** nastavte `App:Copy:MaxProfilesPerNode` > 0 na cap koľko running
profiles node hosts. Každý supervisor potom claims **highest** jeho remaining headroom cez atomic
`FOR UPDATE SKIP LOCKED` bounded claim, takže profiles **spread** across repliky namiesto prvého
supervisor grabbing všetko — žádny single hot pod / SPOF. Skip-locked claim udržiava "presne jeden node
per profil" záruka (žádny double-hosting) aj pod concurrent claims. `0` (default) =
unbounded (jeden node hosts všetko, unchanged).

**At scale (S7/S8):** každý pod jitters reconcile podľa až 20% z `ReconcileInterval`
(`CopyEngineSupervisor.JitteredInterval`), takže N repliky nestrieľajú claim/renew `UPDATE`
simultaneously (Postgres thundering-herd). Keď `copyAgent.replicas > 1` chart aj spreads
repliky across nodes (`topologySpreadConstraints`) a adds `PodDisruptionBudget` (`minAvailable: 1`)
takže drain/upgrade nikdy neberie copy kapacitu na nula.

## Run/backtest execution

`NodeScheduler` picks least-loaded eligible node honouring `MaxInstances`; remote node agents
self-register a heartbeat (`App:Discovery`), `NodeHeartbeatMonitor` marks node unreachable
keď heartbeat prekročí `Discovery:HeartbeatTtl`. Pridajte node agents na add execution capacity;
dead agent routed around automatically.

## Migrations on scale-out / rolling deploy

Každá Web/MCP replica spúšťa `OwnerSeeder` na startup, ktorý aplikuje EF migrations a seeds owner.
Na to bezpečné keď N repliky start naraz, migrate + seed spustite vnútri **Postgres session
advisory lock** (`MigrationLock.RunExclusiveAsync`, key `DatabaseDefaults.MigrationAdvisoryLockKey`):
