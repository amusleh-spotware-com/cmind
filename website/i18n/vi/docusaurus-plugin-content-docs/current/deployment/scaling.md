---
description: "cMind scales out với minimal operator effort. Two stateful workloads — run/backtest execution, copy-trading — both use database as coordination point, vì vậy…"
---

# Horizontal scaling

cMind scales out với minimal operator effort. Hai stateful workloads — run/backtest
execution, copy-trading — both use database as coordination point, vì vậy adding replicas needs
no external coordinator (no ZooKeeper, no leader election).

## Copy-trading (self-healing lease)

Each node runs `CopyEngineSupervisor` (gated on `App:Copy:Enabled`). Every reconcile cycle,
supervisor:

1. **Claims** every running profile unassigned *or* lease-lapsed, in one atomic `UPDATE` —
   two racing supervisors never both claim same profile, vì vậy profile copied by exactly one
   node (no double orders).
2. **Renews** lease on profiles it hosts.
3. Hosts assigned profiles, pushes access-token rotations to running host in place (no
   event-stream drop).

Node crash → stops renewing; once `App:Copy:LeaseTtl` passes, any surviving node reclaims
its profiles next cycle, rebuilds state from reconcile without duplicating trades. **Scaling
out** = add replicas; unassigned/free profiles picked up automatically.

**Graceful scale-in / rolling update (S1)** = on `SIGTERM`, `CopyEngineSupervisor.StopAsync`
**releases this node's leases** (`AssignedNode`/`LeaseExpiresAt` → null) vì vậy survivor reclaims them
its *very next* reconcile cycle — **not** after full `LeaseTtl`. Only hard crash waits the TTL.
Copy-agent's `terminationGracePeriodSeconds` (default 30) gives release time to finish before
pod killed.

### Knobs (`App:Copy`)

| Setting | Default | Notes |
|---------|---------|-------|
| `Enabled` | `false` | Turn copy hosting on for the node. |
| `ReconcileInterval` | `30s` | How often node claims/renews/reconciles. |
| `LeaseTtl` | `120s` | Grace before silent node's profiles reclaimed. Keep few reconcile intervals sao slow cycle doesn't cause spurious hand-off. |
| `NodeName` | machine name | Set distinctly when two supervisors share a host. |

On Kubernetes copy supervisors run as Deployment; set `replicas` to desired parallelism. Each
pod gets stable `NodeName` (default: pod hostname), vì vậy leases attributed per pod. Database là
single source of truth — no sticky sessions, no per-pod state to migrate.

**Balanced distribution (S4):** set `App:Copy:MaxProfilesPerNode` > 0 to cap how many running
profiles a node hosts. Each supervisor then claims **at most** its remaining headroom via atomic
`FOR UPDATE SKIP LOCKED` bounded claim, vì vậy profiles **spread** across replicas instead of first
supervisor grabbing all — no single hot pod / SPOF. Skip-locked claim keeps "exactly one node
per profile" guarantee (no double-hosting) even under concurrent claims. `0` (default) =
unbounded (one node hosts everything, unchanged).

**At scale (S7/S8):** each pod jitters reconcile by up to 20% of `ReconcileInterval`
(`CopyEngineSupervisor.JitteredInterval`) vì vậy N replicas don't fire claim/renew `UPDATE`
simultaneously (Postgres thundering-herd). When `copyAgent.replicas > 1` chart also spreads
replicas across nodes (`topologySpreadConstraints`) và adds `PodDisruptionBudget` (`minAvailable: 1`)
vì vậy drain/upgrade never takes copy capacity to zero.

## Run/backtest execution

`NodeScheduler` picks least-loaded eligible node honouring `MaxInstances`; remote node agents
self-register và heartbeat (`App:Discovery`), `NodeHeartbeatMonitor` marks node unreachable
when heartbeat exceeds `Discovery:HeartbeatTtl`. Add node agents to add execution capacity;
dead agent routed around automatically.

## Migrations on scale-out / rolling deploy

Every Web/MCP replica runs `OwnerSeeder` at startup, which applies EF migrations và seeds owner.
To make that safe when N replicas start at once, migrate + seed run inside a **Postgres session
advisory lock** (`MigrationLock.RunExclusiveAsync`, key `DatabaseDefaults.MigrationAdvisoryLockKey`):
first replica to acquire it migrates và seeds; the rest block on the lock, then find migrations
already applied (no-op) và owner already present. No separate migration job or leader election
needed. If you add first-run seeding, put it **inside** same guarded block vì vậy it is single-writer.

## Node-agent HTTP resilience

Main node talks to each `CtraderCliNode` agent over HTTP through three purpose-split clients sao a
flaky node or network never corrupts state:

- **read** (`status` / `report` / `stats`) — idempotent GETs, retried on transient failures
  (exponential backoff + jitter, `NodeAgentHttp.ReadRetryCount`) với per-attempt và total timeouts.
- **write** (`start` / `stop` / `clean`) — non-idempotent POSTs, timed out but **never retried**: a
  retried `start` could double-launch a container.
- **stream** (`logs`) — long-lived `docker logs -f` stream gets infinite timeout và no
  resilience pipeline, vì vậy tailing never cut off.

A node that stays unreachable handled by heartbeat + [orphaned-instance reclaim](../operations/node-discovery.md);
HTTP layer only smooths transient blips.

## Stateless tiers

Web (Blazor Server + API) và MCP server are stateless behind database, replicate freely.
Auth là cookie-based; scale Web horizontally behind load balancer. MCP server là separate
process/Deployment vì vậy it scales independently of Web.

## Database connection resilience

Every host that opens database uses a **retrying execution strategy** vì vậy a transient
disconnect or a managed-Postgres failover (RDS / Flexible Server patching) retried instead of
surfacing as error to user:

- Web và MCP register context through Aspire Npgsql component với `DisableRetry=false`
  và explicit `CommandTimeout` (`DatabaseDefaults.CommandTimeoutSeconds`).
- CopyAgent (non-Aspire) registers via `UseAppNpgsql`, which applies same
  `EnableRetryOnFailure(MaxRetryCount, MaxRetryDelay)` + command timeout from `DatabaseDefaults`.

All writes are single `SaveChanges` / single `ExecuteUpdate` / single `ExecuteSql` statements, vì vậy
retrying strategy safe (no multi-statement transaction needs manual `strategy.ExecuteAsync`
wrapping). If you add a manual transaction or multiple `SaveChanges` in one logical operation, wrap
it in `db.Database.CreateExecutionStrategy().ExecuteAsync(...)` — otherwise it throws under retry.

## Checklist for scaling out

- [ ] Postgres sized for added connection load (each Web/MCP/node replica opens a pool).
- [ ] `App:Copy:Enabled=true` on every node that should host copy profiles.
- [ ] Distinct `App:Copy:NodeName` per co-located supervisor (K8s: default per-pod fine).
- [ ] `LeaseTtl` ≥ 3× `ReconcileInterval`.
- [ ] Node agents deployed where privileged Docker available (AKS/EKS/EC2/VM, not Fargate).
- [ ] Multi-replica Web: set `signalr` connection string (Redis backplane) **and** enable ingress
      session affinity (sticky sessions) sao a Blazor circuit reconnects to a live pod. A component
      exception caught by `MainLayout` `ErrorBoundary` (friendly retry, circuit stays alive).
