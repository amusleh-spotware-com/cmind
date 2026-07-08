# Node auto-discovery

External nodes join the cluster by **self-registration + heartbeat** — no manual entry required.
This is the same pattern used by Consul/Nomad/kubeadm agents: an agent boots knowing where the
main node is and a shared cluster secret, then continuously announces itself.

> Verified end-to-end on both Docker Compose and a `kind` Kubernetes cluster: agents self-register,
> appear in the DB reachable, get auto-marked unreachable when heartbeats stop past the TTL, and
> return online when they resume.

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

- **Registration == heartbeat.** The agent re-POSTs on `HeartbeatIntervalSeconds`. First call
  creates the node (`NodeRegistered` event); later calls refresh liveness. A resumed heartbeat after
  an outage flips the node back reachable (`NodeCameOnline`).
- **Liveness reconciliation.** `NodeHeartbeatMonitor` marks nodes whose last heartbeat exceeds
  `HeartbeatTtl` as unreachable. The scheduler (`IsActive`/`AcceptsRun`/`AcceptsBacktest` are gated
  on reachability) stops placing work on them until they report again.
- **Identity is the node name.** The main upserts by `NodeName`, so a pod whose IP/URL changes on
  restart keeps its identity and re-registers its new `AdvertiseUrl`.
- **Mode is fixed at first registration.** A node's mode (`Run`/`Backtest`/`Mixed`) is its persisted
  type and cannot change on heartbeat; a re-registration with a different mode is honoured for
  liveness but the mode change is ignored (logged as a warning). To change mode, delete the node and
  let it re-register.

## Configuration

Main (Web) — `App:Discovery`:

| Key | Default | Meaning |
|-----|---------|---------|
| `Enabled` | `false` | Master switch for the register endpoint + monitor. |
| `JoinToken` | — | Shared cluster secret (≥ 32 chars) agents must present. |
| `HeartbeatTtl` | `00:01:30` | Grace before a silent node is marked unreachable. |
| `MonitorInterval` | `00:00:30` | How often the monitor sweeps. |
| `HeartbeatInterval` | `00:00:30` | Value returned to agents as the suggested cadence. |

Agent (ExternalNode) — `NodeAgent`:

| Key | Meaning |
|-----|---------|
| `MainUrl` | Base URL of the main node. Empty = manual registration mode (loop is a no-op). |
| `AdvertiseUrl` | URL the main uses to reach **this** agent. |
| `NodeName` | Unique name; defaults to the machine name if blank. |
| `Mode` | `Run` / `Backtest` / `Mixed`. |
| `MaxInstances` | Capacity hint honoured by the scheduler. |
| `HeartbeatIntervalSeconds` | Re-register cadence. |
| `JwtSecret` | Must equal the main's `JoinToken` — it is both the registration bearer and the dispatch JWT signing key. |

## Security model (v1)

Auto-registered nodes share **one cluster secret** (`JoinToken` == each agent's `JwtSecret`).
The main signs each dispatch request as a 5-minute HS256 JWT with that secret; the agent validates.
Requirements:

- Keep `JoinToken` ≥ 32 chars and rotate it (update the main's `App:Discovery:JoinToken` and every
  agent's `NodeAgent:JwtSecret` together).
- Terminate TLS in front of the main and the agents in production (reverse proxy / ingress).
- The agent still only runs images matching `AllowedImagePrefix`.

**Hardening follow-up (not v1):** issue a unique per-node secret at registration (kubeadm-style
bootstrap → per-node credential) so a single compromised agent cannot forge dispatch tokens for its
peers. The registration flow already returns a response body, which is the natural place to hand
back a minted per-node secret.

## Manual nodes still work

`POST /api/nodes` (admin UI) continues to register pinned nodes with their own per-node secret.
Discovery is additive.
