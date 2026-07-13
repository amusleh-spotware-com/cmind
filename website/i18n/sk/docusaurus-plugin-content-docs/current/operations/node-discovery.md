---
description: "cTrader CLI nodes sa spájajú s cluster cez self-registration + heartbeat — žádná manual entry. Rovnaký pattern ako Consul/Nomad/kubeadm agenti: agent bot..."
---

# Node auto-discovery

cTrader CLI uzly sa spájajú s cluster cez **self-registration + heartbeat** — žádna manual entry. Rovnaký pattern ako Consul/Nomad/kubeadm agenti: agent bot poznajúci main node location + shared cluster secret, potom neustále sa oznamuje.

> Overené end-to-end na Docker Compose a `kind` Kubernetes cluster: agenti self-register, objaviť sa v DB reachable, auto-marked unreachable keď heartbeats stop past TTL, vrátia sa online keď resume.

## Ako to funguje

```
CtraderCliNode agent                         Main (Web)
------------------                         ----------
POST /api/nodes/register  ── join token ──▶ verify token (constant-time)
  { name, baseUrl, mode,                    verify protocol version
    maxInstances, dataDir,                   upsert CtraderCliNode by name
    protocolVersion }                        stamp LastHeartbeatAt, IsReachable=true
        ▲                                     └─ CtraderCliNode.SelfRegister / RecordHeartbeat
        │  every HeartbeatInterval            NodeHeartbeatMonitor (background):
        └──────────────────────────────────── if now - LastHeartbeatAt > HeartbeatTtl
                                                 → CtraderCliNode.MarkUnreachable() (NodeWentOffline)
```

- **Registration == heartbeat.** Agent re-POSTs na `HeartbeatIntervalSeconds`. Prvý call vytvorí node (`NodeRegistered` event); neskorší calls refresh liveness. Obnovený heartbeat po výpadku prevráti node späť reachable (`NodeCameOnline`).
- **Liveness reconciliation.** `NodeHeartbeatMonitor` označuje uzly, ktorých posledný heartbeat prekročil `HeartbeatTtl` unreachable. Scheduler (`IsActive`/`AcceptsRun`/`AcceptsBacktest` gated na reachability) zastaví placement práce, kým sa znova neoznamia.
- **Orphaned-instance reclaim.** `NodeInstanceReclaimer` (background) transition akákoľvek non-terminal instance stranded na unreachable node na **Failed** (`FailureReason = "Node unreachable - instance reclaimed"`, `InstanceFailed` domain event → user notification), takže crashed/partitioned node nikdy nemôže nechať instance stuck "Running" forever. Reclaim iba fires keď node poslední heartbeat je stale beyond `HeartbeatTtl + InstanceReclaimGrace`, dáva brief-blip šancu ozvať sa prvý. Reclaimed **runs nie sú auto-rescheduled**: partitioned-but-alive node môže stále byť executing kontajner a nie je container-level fencing, takže re-launching by risked double execution — používateľ restarts reclaimed run deliberately. Backtests self-exit, takže reclaimed backtest je jednoducho re-run.
- **Identity je node name.** Main upserts by `NodeName`, takže pod, ktorého IP/URL zmení na restart, udržiava identitu, re-registers new `AdvertiseUrl`.
- **Mode fixed at first registration.** Node mode (`Run`/`Backtest`/`Mixed`) je persisted type, nemôže sa zmeniť na heartbeat; re-registration s iným mode je honoured pre liveness ale mode change ignored (logged ako warning). Na zmenu mode: delete node, nechajte ho re-register.

## Konfigurácia

Main (Web) — `App:Discovery`:

| Kľúč | Default | Znamenie |
|-----|---------|---------|
| `Enabled` | `false` | Master switch pre register endpoint + monitor. |
| `JoinToken` | — | Shared cluster secret (≥ 32 chars) agenti musia present. |
| `HeartbeatTtl` | `00:01:30` | Grace pred silent node marked unreachable. |
| `InstanceReclaimGrace` | `00:01:00` | Extra margin beyond `HeartbeatTtl` pred stranded instance na unreachable node je reclaimed (failed). |
| `MonitorInterval` | `00:00:30` | Ako často monitor a instance-reclaimer sweep. |
| `HeartbeatInterval` | `00:00:30` | Value returned agentom ako suggested cadence. |

Agent (CtraderCliNode) — `NodeAgent`:

| Kľúč | Znamenie |
|-----|---------|
| `MainUrl` | Base URL main node. Empty = manual registration mode (loop no-op). |
| `AdvertiseUrl` | URL main uses na reach **this** agent. |
| `NodeName` | Unique name; defaults na machine name ak blank. |
| `Mode` | `Run` / `Backtest` / `Mixed`. |
| `MaxInstances` | Capacity hint honoured scheduler. |
| `HeartbeatIntervalSeconds` | Re-register cadence. |
| `JwtSecret` | Must equal main `JoinToken` — both registration bearer a dispatch JWT signing key. |

## Security model (v1)

Auto-registered uzly share **jeden cluster secret** (`JoinToken` == každého agent `JwtSecret`). Main signs každý dispatch request ako 5-minute HS256 JWT s tým secret; agent validates. Requirements:

- Udržujte `JoinToken` ≥ 32 chars a otočte ho (update main `App:Discovery:JoinToken` a každý agent `NodeAgent:JwtSecret` spolu).
- Terminate TLS pred main a agents v produkácii (reverse proxy / ingress).
- Agent stále len spúšťa obrazy matching `AllowedImagePrefix`.

**Hardening follow-up (nie v1):** issue unique per-node secret na registration (kubeadm-style bootstrap → per-node credential) takže single compromised agent nemôže forge dispatch tokens pre peers. Registration flow už vraciaci response body — natural place na hand back minted per-node secret.

## Manual nodes stále fungujú

`POST /api/nodes` (admin UI) pokračuje v registrácii pinned nodes s own per-node secret. Discovery je additive.

White-label deployment môže **skryť manual controls** (alebo celý Nodes surface) a spoľahnúť sa čisto na
auto-discovery: `App:Branding:NodesUi=Monitor` drops manual add/delete, `Hidden` odstraní nav, page a
manual API a `App:Branding:RestrictNodesToOwner` floors surface na owner-only. Self-register +
heartbeat endpoint tu je unaffected v každom mode. Pozrite
[White-label → Nodes UI visibility](../features/white-label.md#nodes-ui-visibility).
