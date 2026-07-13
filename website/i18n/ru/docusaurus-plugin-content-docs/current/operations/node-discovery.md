---
description: "cTrader CLI узлы присоединяются кластер по self-registration + heartbeat — нет ручного entry. Тот же pattern как Consul/Nomad/kubeadm агентов: agent boots зная main node…"
---

# Node auto-discovery

cTrader CLI узлы присоединяются кластер по **self-registration + heartbeat** — нет ручного entry. Тот же pattern как Consul/Nomad/kubeadm агентов: agent boots зная main node location + shared cluster secret, затем непрерывно объявляет себя.

> Верифицировано end-to-end на Docker Compose и `kind` Kubernetes кластере: агентов self-register, появляются в БД reachable, auto-marked unreachable когда heartbeats остановятся past TTL, return online когда resume.

## Как это работает

```
CtraderCliNode agent                         Main (Web)
------------------                         ----------
POST /api/nodes/register  ── join token ──▶ verify token (constant-time)
  { name, baseUrl, mode,                    verify protocol версия
    maxInstances, dataDir,                   upsert CtraderCliNode по name
    protocolVersion }                        stamp LastHeartbeatAt, IsReachable=true
        ▲                                     └─ CtraderCliNode.SelfRegister / RecordHeartbeat
        │  каждый HeartbeatInterval          NodeHeartbeatMonitor (background):
        └──────────────────────────────────── если now - LastHeartbeatAt > HeartbeatTtl
                                                 → CtraderCliNode.MarkUnreachable() (NodeWentOffline)
```

- **Registration == heartbeat.** Agent re-POSTs на `HeartbeatIntervalSeconds`. Первый вызов создает node (`NodeRegistered` event); позже вызовы refresh liveness. Resumed heartbeat после outage flips node назад reachable (`NodeCameOnline`).
- **Liveness reconciliation.** `NodeHeartbeatMonitor` отмечает узлы чья last heartbeat превышает `HeartbeatTtl` unreachable. Scheduler (`IsActive`/`AcceptsRun`/`AcceptsBacktest` gated на reachability) останавливает размещение работы пока они не отчитаются снова.
- **Orphaned-instance reclaim.** `NodeInstanceReclaimer` (background) переходит любой non-terminal instance stranded на unreachable узле в **Failed** (`FailureReason = "Node unreachable - instance reclaimed"`, `InstanceFailed` domain event → user notification), поэтому crashed/partitioned узел может никогда оставить instance stuck "Running" вечно. Reclaim только fires один раз узел's last heartbeat stale beyond `HeartbeatTtl + InstanceReclaimGrace`, давая brief-blip шанс восстановиться первый. Reclaimed **runs не auto-rescheduled**: partitioned-but-alive узел может все еще выполнять container и есть нет container-level fencing, поэтому re-launching рисковать double execution — пользователь перезапускает reclaimed run deliberately. Backtests self-exit, поэтому reclaimed backtest это просто re-run.
- **Identity это node name.** Main upserts по `NodeName`, поэтому pod чьи IP/URL изменения на restart сохраняет идентичность, re-registers new `AdvertiseUrl`.
- **Mode fixed на первой регистрации.** Node mode (`Run`/`Backtest`/`Mixed`) persisted тип, не может менять на heartbeat; re-registration с different mode honoured для liveness но mode изменение ignored (logged как warning). Менять mode: delete node, пусть это re-register.

## Конфигурация

Main (Web) — `App:Discovery`:

| Ключ | Default | Значение |
|-----|---------|---------|
| `Enabled` | `false` | Master switch для register endpoint + monitor. |
| `JoinToken` | — | Shared cluster secret (≥ 32 chars) агентов должны present. |
| `HeartbeatTtl` | `00:01:30` | Grace перед silent node отмечен unreachable. |
| `InstanceReclaimGrace` | `00:01:00` | Лишний margin beyond `HeartbeatTtl` перед stranded instance на unreachable узле это reclaimed (failed). |
| `MonitorInterval` | `00:00:30` | Как часто monitor и instance-reclaimer sweep. |
| `HeartbeatInterval` | `00:00:30` | Значение возвращено агентам как suggested cadence. |

Agent (CtraderCliNode) — `NodeAgent`:

| Ключ | Значение |
|-----|---------|
| `MainUrl` | Base URL из main node. Empty = manual registration режим (loop no-op). |
| `AdvertiseUrl` | URL main использует для доступа **этот** agent. |
| `NodeName` | Уникальное имя; по умолчанию machine name если blank. |
| `Mode` | `Run` / `Backtest` / `Mixed`. |
| `MaxInstances` | Capacity hint honoured по scheduler. |
| `HeartbeatIntervalSeconds` | Re-register cadence. |
| `JwtSecret` | Должен равняться main's `JoinToken` — оба registration bearer и dispatch JWT signing key. |

## Security модель (v1)

Auto-registered узлы разделяют **один cluster secret** (`JoinToken` == каждый agent's `JwtSecret`). Main подписывает каждый dispatch запрос как 5-минутный HS256 JWT с тем секретом; agent валидирует. Требования:

- Сохраняйте `JoinToken` ≥ 32 chars и rotate это (update main's `App:Discovery:JoinToken` и каждый agent's `NodeAgent:JwtSecret` вместе).
- Terminate TLS перед main и agents в production (reverse proxy / ingress).
- Agent все еще только запускает образы matching `AllowedImagePrefix`.

**Hardening follow-up (не v1):** issue уникальный per-node secret на регистрации (kubeadm-style bootstrap → per-node credential) поэтому single compromised agent не может forge dispatch tokens для peers. Registration поток уже возвращает response body — natural место для hand back minted per-node secret.

## Manual узлы все еще работают

`POST /api/nodes` (admin UI) продолжает регистрировать pinned узлы с own per-node secret. Discovery это additive.

White-label развертывание может **скрыть manual controls** (или целый Nodes поверхность) и полагаться чисто на auto-discovery: `App:Branding:NodesUi=Monitor` drops manual add/delete, `Hidden` removes nav, page и manual API, и `App:Branding:RestrictNodesToOwner` floors поверхность на owner-only. Self-register + heartbeat endpoint здесь unaffected в каждом режиме. Смотрите [White-label → Nodes UI видимость](../features/white-label.md#nodes-ui-visibility).
