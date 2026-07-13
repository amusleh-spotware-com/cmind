---
description: "cMind масштабируется горизонтально с минимальными усилиями оператора. Два stateful workload — run/backtest execution и copy-trading — оба используют базу данных как точку координации."
---

# Horizontal scaling

cMind масштабируется горизонтально с минимальными усилиями оператора. Два stateful workload —
run/backtest execution, copy-trading — оба используют базу данных как точку координации, поэтому
добавление реплик не требует внешнего координатора (без ZooKeeper, без leader election).

## Copy-trading (self-healing lease)

Каждый узел запускает `CopyEngineSupervisor` (gated on `App:Copy:Enabled`). Каждый reconcile cycle,
supervisor:

1. **Claims** every running profile unassigned *or* lease-lapsed, в одном atomic `UPDATE` —
   два конкурирующих supervisor'а никогда оба не claim тот же profile, поэтому profile копируется точное одним
   узлом (без double orders).
2. **Renews** lease на профилях которые хостит.
3. Hosts assigned profiles, pushes access-token rotations to running host in place (no
   event-stream drop).

Node crash → stops renewing; once `App:Copy:LeaseTtl` passes, any surviving node reclaims
its profiles next cycle, rebuilds state from reconcile without duplicating trades. **Scaling
out** = добавить реплики; unassigned/free profiles picked up automatically.

**Graceful scale-in / rolling update (S1)** = on `SIGTERM`, `CopyEngineSupervisor.StopAsync`
**releases this node's leases** (`AssignedNode`/`LeaseExpiresAt` → null) so survivor reclaims them
its *very next* reconcile cycle — **not** after full `LeaseTtl`. Only hard crash waits the TTL.
Copy-agent's `terminationGracePeriodSeconds` (default 30) даёт release время закончить before
pod killed.

### Knobs (`App:Copy`)

| Настройка | По умолчанию | Заметки |
|---------|--------|---------|
| `Enabled` | `false` | Turn copy hosting on for the node. |
| `ReconcileInterval` | `30s` | Как часто node claims/renews/reconciles. |
| `LeaseTtl` | `120s` | Grace before silent node's profiles reclaimed. Keep few reconcile intervals so slow cycle doesn't cause spurious hand-off. |
| `NodeName` | machine name | Set distinctly when two supervisors share a host. |

On Kubernetes copy supervisors запускаются как Deployment; установите `replicas` к желаемому parallelism. Каждый
pod получает stable `NodeName` (default: pod hostname), so leases attributed per pod. Database is
single source of truth — no sticky sessions, no per-pod state to migrate.

**Balanced distribution (S4):** установите `App:Copy:MaxProfilesPerNode` > 0 to cap how many running
profiles a node hosts. Each supervisor then claims **at most** its remaining headroom via atomic
`FOR UPDATE SKIP LOCKED` bounded claim, so profiles **spread** across replicas instead of first
supervisor grabbing all — no single hot pod / SPOF. Skip-locked claim keeps "exactly one node
per profile" guarantee even under concurrent claims. `0` (default) =
unbounded (one node hosts everything, unchanged).

**At scale (S7/S8):** each pod jitters reconcile by up to 20% of `ReconcileInterval`
(`CopyEngineSupervisor.JitteredInterval`) so N replicas don't fire claim/renew `UPDATE`
simultaneously (Postgres thundering-herd). When `copyAgent.replicas > 1` chart also spreads
replicas across nodes (`topologySpreadConstraints`) и adds `PodDisruptionBudget` (`minAvailable: 1`)
so drain/upgrade никогда не takes copy capacity to zero.

## Run/backtest execution

`NodeScheduler` picks least-loaded eligible node honouring `MaxInstances`; remote node agents
self-register и heartbeat (`App:Discovery`), `NodeHeartbeatMonitor` marks node unreachable
when heartbeat exceeds `Discovery:HeartbeatTtl`. Add node agents to add execution capacity;
dead agent routed around automatically.

## Migrations on scale-out / rolling deploy

Every Web/MCP replica runs `OwnerSeeder` at startup, which applies EF migrations и seeds the owner.
To make that safe when N replicas start at once, migrate + seed run inside a **Postgres session
advisory lock** (`MigrationLock.RunExclusiveAsync`, key `DatabaseDefaults.MigrationAdvisoryLockKey`):
the first replica to acquire it migrates and seeds; the rest block on the lock, then find migrations
already applied (no-op) and the owner already present. No separate migration job or leader election
needed. If you add first-run seeding, put it **inside** the same guarded block so it is single-writer.

## Node-agent HTTP resilience

Main node talks to each `CtraderCliNode` agent over HTTP through three purpose-split clients so a
flaky node or network никогда не corrupts state:

- **read** (`status` / `report` / `stats`) — idempotent GETs, retried on transient failures
  (exponential backoff + jitter, `NodeAgentHttp.ReadRetryCount`) с per-attempt и total timeouts.
- **write** (`start` / `stop` / `clean`) — non-idempotent POSTs, timed out but **never retried**: a
  retried `start` could double-launch a container.
- **stream** (`logs`) — the long-lived `docker logs -f` stream gets an infinite timeout и no
  resilience pipeline, so tailing is never cut off.

A node that stays unreachable handled by heartbeat + [orphaned-instance reclaim](../operations/node-discovery.md);
HTTP layer only smooths transient blips.

## Stateless tiers

Web (Blazor Server + API) и MCP server stateless behind database, replicate freely.
Auth is cookie-based; scale Web horizontally behind load balancer. MCP server is separate
process/Deployment so it scales independently of Web.

## Database connection resilience

Every host that opens the database uses a **retrying execution strategy** so a transient
disconnect или managed-Postgres failover (RDS / Flexible Server patching) retried instead of
surfacing as an error to the user:

- Web и MCP register the context through the Aspire Npgsql component with `DisableRetry=false`
  и explicit `CommandTimeout` (`DatabaseDefaults.CommandTimeoutSeconds`).
- CopyAgent (non-Aspire) registers via `UseAppNpgsql`, which applies the same
  `EnableRetryOnFailure(MaxRetryCount, MaxRetryDelay)` + command timeout from `DatabaseDefaults`.

All writes are single `SaveChanges` / single `ExecuteUpdate` / single `ExecuteSql` statements, so the
retrying strategy is safe (no multi-statement transaction needs manual `strategy.ExecuteAsync`
wrapping). If you add a manual transaction or multiple `SaveChanges` in one logical operation, wrap
it in `db.Database.CreateExecutionStrategy().ExecuteAsync(...)` — otherwise it throws under retry.

## Checklist for scaling out

- [ ] Postgres sized for added connection load (each Web/MCP/node replica opens a pool).
- [ ] `App:Copy:Enabled=true` on every node that should host copy profiles.
- [ ] Distinct `App:Copy:NodeName` per co-located supervisor (K8s: default per-pod fine).
- [ ] `LeaseTtl` ≥ 3× `ReconcileInterval`.
- [ ] Node agents deployed where privileged Docker available (AKS/EKS/EC2/VM, not Fargate).
- [ ] Multi-replica Web: set the `signalr` connection string (Redis backplane) **and** enable ingress
      session affinity (sticky sessions) so a Blazor circuit reconnects to a live pod.
