# Horizontal scaling

cMind scales out with minimal operator effort. The two stateful workloads — run/backtest
execution and copy-trading — both use the database as the coordination point, so adding
replicas needs no external coordinator (no ZooKeeper, no leader election service).

## Copy-trading (self-healing lease)

Each node runs a `CopyEngineSupervisor` (gated on `App:Copy:Enabled`). Every reconcile
cycle a supervisor:

1. **Claims** every running profile that is unassigned *or* whose lease has lapsed, in one
   atomic `UPDATE` — two supervisors racing can never both claim the same profile, so a
   profile is copied by exactly one node (no double orders).
2. **Renews** the lease on the profiles it already hosts.
3. Hosts the profiles assigned to it, and pushes access-token rotations to the running host
   in place (no event-stream drop).

If a node crashes, it stops renewing; once `App:Copy:LeaseTtl` passes, any surviving node
reclaims its profiles on the next cycle and rebuilds state from a reconcile without
duplicating trades. **Scaling out** = add replicas; unassigned/free profiles are picked up
automatically.

**Graceful scale-in / rolling update (S1)** = on `SIGTERM`, `CopyEngineSupervisor.StopAsync`
**releases this node's leases** (`AssignedNode`/`LeaseExpiresAt` → null) so a survivor reclaims them
on its *very next* reconcile cycle — **not** after the full `LeaseTtl`. Only a hard crash waits the TTL.
The copy-agent's `terminationGracePeriodSeconds` (default 30) gives the release time to complete before
the pod is killed.

### Knobs (`App:Copy`)

| Setting | Default | Notes |
|---------|---------|-------|
| `Enabled` | `false` | Turn copy hosting on for the node. |
| `ReconcileInterval` | `30s` | How often a node claims/renews/reconciles. |
| `LeaseTtl` | `120s` | Grace before a silent node's profiles are reclaimed. Keep it a few reconcile intervals so a slow cycle doesn't cause a spurious hand-off. |
| `NodeName` | machine name | Set distinctly when two supervisors share a host. |

On Kubernetes the copy supervisors run as a Deployment; set `replicas` to the desired
degree of parallelism. Each pod gets a stable `NodeName` (default: pod hostname), so leases
are attributed per pod. The database is the single source of truth — no sticky sessions,
no per-pod state to migrate.

## Run/backtest execution

`NodeScheduler` picks the least-loaded eligible node honouring `MaxInstances`; remote node
agents self-register and heartbeat (`App:Discovery`), and `NodeHeartbeatMonitor` marks a
node unreachable when its heartbeat exceeds `Discovery:HeartbeatTtl`. Add node agents to add
execution capacity; a dead agent is routed around automatically.

## Stateless tiers

Web (Blazor Server + API) and the MCP server are stateless behind the database and can be
replicated freely. Auth is cookie-based; scale Web horizontally behind a load balancer.
The MCP server is a separate process/Deployment so it scales independently of Web.

## Checklist for scaling out

- [ ] Postgres sized for the added connection load (each Web/MCP/node replica opens a pool).
- [ ] `App:Copy:Enabled=true` on every node that should host copy profiles.
- [ ] Distinct `App:Copy:NodeName` per co-located supervisor (K8s: default per-pod is fine).
- [ ] `LeaseTtl` ≥ 3× `ReconcileInterval`.
- [ ] Node agents deployed where privileged Docker is available (AKS/EKS/EC2/VM, not Fargate).
