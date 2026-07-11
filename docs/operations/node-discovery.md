# Node auto-discovery

External nodes join cluster by **self-registration + heartbeat** — no manual entry. Same pattern as Consul/Nomad/kubeadm agents: agent boots knowing main node location + shared cluster secret, then continuously announces itself.

> Verified end-to-end on Docker Compose and `kind` Kubernetes cluster: agents self-register, appear in DB reachable, auto-marked unreachable when heartbeats stop past TTL, return online when resume.

## How it works

```
ExternalNode agent                         Main (Web)
------------------                         ----------
POST /api/nodes/register  ── join token ──▶ verify token (constant-time)
  { name, baseUrl, mode,                    verify protocol version
    maxInstances, dataDir,                   upsert RemoteNode by name
    protocolVersion }                        stamp LastHeartbeatAt, IsReachable=true
        ▲                                     └─ RemoteNode.SelfRegister / RecordHeartbeat
        │  every HeartbeatInterval            NodeHeartbeatMonitor (background):
        └──────────────────────────────────── if now - LastHeartbeatAt > HeartbeatTtl
                                                 → RemoteNode.MarkUnreachable() (NodeWentOffline)
```

- **Registration == heartbeat.** Agent re-POSTs on `HeartbeatIntervalSeconds`. First call creates node (`NodeRegistered` event); later calls refresh liveness. Resumed heartbeat after outage flips node back reachable (`NodeCameOnline`).
- **Liveness reconciliation.** `NodeHeartbeatMonitor` marks nodes whose last heartbeat exceeds `HeartbeatTtl` unreachable. Scheduler (`IsActive`/`AcceptsRun`/`AcceptsBacktest` gated on reachability) stops placing work until they report again.
- **Identity is node name.** Main upserts by `NodeName`, so pod whose IP/URL changes on restart keeps identity, re-registers new `AdvertiseUrl`.
- **Mode fixed at first registration.** Node mode (`Run`/`Backtest`/`Mixed`) is persisted type, cannot change on heartbeat; re-registration with different mode honoured for liveness but mode change ignored (logged as warning). To change mode: delete node, let it re-register.

## Configuration

Main (Web) — `App:Discovery`:

| Key | Default | Meaning |
|-----|---------|---------|
| `Enabled` | `false` | Master switch for register endpoint + monitor. |
| `JoinToken` | — | Shared cluster secret (≥ 32 chars) agents must present. |
| `HeartbeatTtl` | `00:01:30` | Grace before silent node marked unreachable. |
| `MonitorInterval` | `00:00:30` | How often monitor sweeps. |
| `HeartbeatInterval` | `00:00:30` | Value returned to agents as suggested cadence. |

Agent (ExternalNode) — `NodeAgent`:

| Key | Meaning |
|-----|---------|
| `MainUrl` | Base URL of main node. Empty = manual registration mode (loop no-op). |
| `AdvertiseUrl` | URL main uses to reach **this** agent. |
| `NodeName` | Unique name; defaults to machine name if blank. |
| `Mode` | `Run` / `Backtest` / `Mixed`. |
| `MaxInstances` | Capacity hint honoured by scheduler. |
| `HeartbeatIntervalSeconds` | Re-register cadence. |
| `JwtSecret` | Must equal main's `JoinToken` — both registration bearer and dispatch JWT signing key. |

## Security model (v1)

Auto-registered nodes share **one cluster secret** (`JoinToken` == each agent's `JwtSecret`). Main signs each dispatch request as 5-minute HS256 JWT with that secret; agent validates. Requirements:

- Keep `JoinToken` ≥ 32 chars and rotate it (update main's `App:Discovery:JoinToken` and every agent's `NodeAgent:JwtSecret` together).
- Terminate TLS in front of main and agents in production (reverse proxy / ingress).
- Agent still only runs images matching `AllowedImagePrefix`.

**Hardening follow-up (not v1):** issue unique per-node secret at registration (kubeadm-style bootstrap → per-node credential) so single compromised agent cannot forge dispatch tokens for peers. Registration flow already returns response body — natural place to hand back minted per-node secret.

## Manual nodes still work

`POST /api/nodes` (admin UI) continues to register pinned nodes with own per-node secret. Discovery is additive.