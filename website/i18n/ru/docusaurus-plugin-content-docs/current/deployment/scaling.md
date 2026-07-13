---
description: "cMind масштабируется с минимальным operator усилием. Две stateful workloads — run/backtest выполнение, copy-trading — обе используют базу данных как coordination точка, поэтому…"
---

# Горизонтальное масштабирование

cMind масштабируется с минимальным operator усилием. Две stateful workloads — run/backtest выполнение, copy-trading — обе используют базу данных как coordination точка, поэтому добавление replicas требует нет external coordinator (нет ZooKeeper, нет leader election).

## Copy-trading (self-healing lease)

Каждый узел запускает `CopyEngineSupervisor` (gated на `App:Copy:Enabled`). Каждый reconcile цикл, supervisor:

1. **Claims** каждый running профиль unassigned *или* lease-lapsed, в один atomic `UPDATE` — два racing supervisors никогда не claim один и тот же профиль, поэтому профиль скопирован ровно один узел (нет double orders).
2. **Renews** lease на профилях это hosts.
3. Hosts assigned профили, pushes access-token ротации на running host на place (нет event-stream drop).

Node crash → stops renewing; один раз `App:Copy:LeaseTtl` passes, любой surviving узел reclaims его профили next cycle, rebuilds состояние из reconcile без duplicating trades. **Масштабирование out** = добавить replicas; unassigned/free профили picked up автоматически.

**Graceful scale-in / rolling update (S1)** = на `SIGTERM`, `CopyEngineSupervisor.StopAsync` **releases этот узел's leases** (`AssignedNode`/`LeaseExpiresAt` → null) поэтому survivor reclaims их *очень следующий* reconcile цикл — **не** после полного `LeaseTtl`. Только hard crash ждет TTL.
Copy-agent's `terminationGracePeriodSeconds` (default 30) дает release время для finish перед pod killed.

### Knobs (`App:Copy`)

| Setting | Default | Примечания |
|---------|---------|-------|
| `Enabled` | `false` | Turn copy hosting on для узла. |
| `ReconcileInterval` | `30s` | Как часто узел claims/renews/reconciles. |
| `LeaseTtl` | `120s` | Grace перед silent узел's профили reclaimed. Сохраняйте few reconcile интервалы поэтому slow цикл не вызывает spurious hand-off. |
| `NodeName` | машина имя | Установить distinctly когда двое supervisors разделяют хост. |

На Kubernetes copy supervisors запускаются как Deployment; установить `replicas` в desired параллелизм. Каждый pod получает stable `NodeName` (default: pod hostname), поэтому leases attributed per pod. База данных single источник истины — нет sticky сессий, нет per-pod состояние для migrate.

**Balanced distribution (S4):** установить `App:Copy:MaxProfilesPerNode` > 0 для cap сколько running профилей узел hosts. Каждый supervisor затем claims **at most** его оставшийся headroom через atomic `FOR UPDATE SKIP LOCKED` bounded claim, поэтому профили **spread** через replicas вместо first supervisor grabbing все — нет single hot pod / SPOF. Skip-locked claim сохраняет "ровно один узел per профиль" гарантия (нет double-hosting) даже под concurrent claims. `0` (default) = unbounded (один узел hosts все, без изменений).

**На масштабе (S7/S8):** каждый pod jitters reconcile на up to 20% `ReconcileInterval` (`CopyEngineSupervisor.JitteredInterval`) поэтому N replicas не fire claim/renew `UPDATE` одновременно (Postgres thundering-herd). Когда `copyAgent.replicas > 1` chart также spreads replicas через узлы (`topologySpreadConstraints`) и добавляет `PodDisruptionBudget` (`minAvailable: 1`) поэтому drain/upgrade никогда не берет copy вместимость в zero.

## Run/backtest выполнение

`NodeScheduler` выбирает least-loaded eligible узел honouring `MaxInstances`; remote узел агентов self-register и heartbeat (`App:Discovery`), `NodeHeartbeatMonitor` отмечает узел unreachable когда heartbeat превышает `Discovery:HeartbeatTtl`. Добавить узел агентов для добавления выполнения вместимости; dead агент routed вокруг автоматически.

## Миграции на scale-out / rolling deploy

Каждый Web/MCP replica запускает `OwnerSeeder` при запуске, который применяет EF миграции и seeds owner. Для сделать это safe когда N replicas запускаются одновременно, migrate + seed запускаются внутри **Postgres сессия advisory lock** (`MigrationLock.RunExclusiveAsync`, key `DatabaseDefaults.MigrationAdvisoryLockKey`): первый replica для acquire это migrates и seeds; rest block на lock, затем find миграции уже applied (no-op) и owner уже present. Нет отдельная migration работа или leader election требуется. Если вы добавляете first-run seeding, положите это **внутри** того же guarded block поэтому это single-writer.

## Node-agent HTTP resilience

Main узел talks в каждый `CtraderCliNode` agent над HTTP через три purpose-split clients поэтому flaky узел или сеть никогда не corrupts состояние:

- **read** (`status` / `report` / `stats`) — idempotent GETs, retried на transient failures (exponential backoff + jitter, `NodeAgentHttp.ReadRetryCount`) с per-attempt и total timeouts.
- **write** (`start` / `stop` / `clean`) — non-idempotent POSTs, timed out но **никогда не retried**: retried `start` мог double-launch контейнер.
- **stream** (`logs`) — long-lived `docker logs -f` stream получает infinite timeout и нет resilience pipeline, поэтому tailing это никогда не cut off.

Узел это остается unreachable это handled by heartbeat + [orphaned-instance reclaim](../operations/node-discovery.md); HTTP слой только smooths transient blips.

## Stateless tiers

Web (Blazor Server + API) и MCP сервер stateless позади базы данных, replicate свободно. Auth это cookie-based; scale Web горизонтально позади load balancer. MCP сервер это отдельный процесс/Deployment поэтому это scales независимо Web.

## База данных connection resilience

Каждый хост это открывает базу данных использует **retrying выполнение strategy** поэтому transient disconnect или managed-Postgres failover (RDS / Flexible Server patching) это retried вместо surfacing как ошибка пользователю:

- Web и MCP регистрируют контекст через Aspire Npgsql компонент с `DisableRetry=false` и explicit `CommandTimeout` (`DatabaseDefaults.CommandTimeoutSeconds`).
- CopyAgent (non-Aspire) регистрирует через `UseAppNpgsql`, которая применяет то же `EnableRetryOnFailure(MaxRetryCount, MaxRetryDelay)` + command timeout из `DatabaseDefaults`.

Все writes это single `SaveChanges` / single `ExecuteUpdate` / single `ExecuteSql` statements, поэтому retrying strategy это safe (нет multi-statement транзакция требует ручного `strategy.ExecuteAsync` wrapping). Если вы добавляете ручную транзакцию или множественные `SaveChanges` в один logical операция, обернуть это в `db.Database.CreateExecutionStrategy().ExecuteAsync(...)` — иначе это выбрасывает под retry.

## Checklist для масштабирования

- [ ] Postgres sized для добавлено connection нагрузку (каждый Web/MCP/node replica открывает pool).
- [ ] `App:Copy:Enabled=true` на каждом узле которой должно host copy профили.
- [ ] Distinct `App:Copy:NodeName` per co-located supervisor (K8s: default per-pod fine).
- [ ] `LeaseTtl` ≥ 3× `ReconcileInterval`.
- [ ] Node агентов deployed где privileged Docker доступно (AKS/EKS/EC2/VM, не Fargate).
- [ ] Multi-replica Web: установить `signalr` connection string (Redis backplane) **и** enable ingress session affinity (sticky сессии) поэтому Blazor circuit reconnects в live pod. Component исключение это caught `MainLayout` `ErrorBoundary` (friendly retry, circuit остается alive).
