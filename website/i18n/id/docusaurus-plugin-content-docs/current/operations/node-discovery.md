---
description: "Node CLI cTrader join cluster oleh self-registration + heartbeat — tidak ada manual entry. Same pattern seperti Consul/Nomad/kubeadm agent: agent boot tahu main node location + shared cluster secret, kemudian continuously announce sendiri."
---

# Auto-discovery node

Node CLI cTrader join cluster oleh **self-registration + heartbeat** — tidak ada manual entry. Same pattern seperti Consul/Nomad/kubeadm agent: agent boot tahu main node location + shared cluster secret, kemudian continuously announce sendiri.

> Verified end-to-end pada Docker Compose dan `kind` Kubernetes cluster: agent self-register, muncul di DB reachable, auto-marked unreachable saat heartbeat stop past TTL, return online saat resume.

## Bagaimana cara kerjanya

```
Agent CtraderCliNode                         Main (Web)
------------------                         ----------
POST /api/nodes/register  ── join token ──▶ verify token (constant-time)
  { name, baseUrl, mode,                    verify protocol version
    maxInstances, dataDir,                   upsert CtraderCliNode by name
    protocolVersion }                        stamp LastHeartbeatAt, IsReachable=true
        ▲                                     └─ CtraderCliNode.SelfRegister / RecordHeartbeat
        │  setiap HeartbeatInterval            NodeHeartbeatMonitor (background):
        └──────────────────────────────────── jika now - LastHeartbeatAt > HeartbeatTtl
                                                 → CtraderCliNode.MarkUnreachable() (NodeWentOffline)
```

- **Registration == heartbeat.** Agent re-POST pada `HeartbeatIntervalSeconds`. Call pertama create node (`NodeRegistered` event); call kemudian refresh liveness. Resumed heartbeat setelah outage flip node kembali reachable (`NodeCameOnline`).
- **Liveness reconciliation.** `NodeHeartbeatMonitor` mark node yang last heartbeat melebihi `HeartbeatTtl` unreachable. Scheduler (`IsActive`/`AcceptsRun`/`AcceptsBacktest` gated pada reachability) stop placing work sampai merge kembali.
- **Orphaned-instance reclaim.** `NodeInstanceReclaimer` (background) transition instance non-terminal stranded di unreachable node ke **Failed** (`FailureReason = "Node unreachable - instance reclaimed"`, `InstanceFailed` domain event → user notification), jadi crashed/partitioned node tidak dapat pernah leave instance stuck "Running" selamanya. Reclaim hanya fire sekali last heartbeat node stale beyond `HeartbeatTtl + InstanceReclaimGrace`, memberi brief-blip kesempatan recover first. Reclaimed **run tidak auto-reschedule**: partitioned-but-alive node mungkin masih execute container dan tidak ada container-level fencing, jadi re-launching berisiko double execution — user restart reclaimed run deliberately. Backtest self-exit, jadi reclaimed backtest simply re-run.
- **Identitas adalah node name.** Main upsert oleh `NodeName`, jadi pod yang IP/URL berubah di restart simpan identity, re-register `AdvertiseUrl` baru.
- **Mode fixed di first registration.** Mode node (`Run`/`Backtest`/`Mixed`) adalah persisted type, tidak dapat berubah di heartbeat; re-registration dengan mode berbeda honoured untuk liveness tetapi mode change ignored (logged sebagai warning). Untuk ubah mode: delete node, biarkan re-register.

## Konfigurasi

Main (Web) — `App:Discovery`:

| Key | Default | Meaning |
|-----|---------|---------|
| `Enabled` | `false` | Master switch untuk register endpoint + monitor. |
| `JoinToken` | — | Shared cluster secret (≥ 32 char) agent harus present. |
| `HeartbeatTtl` | `00:01:30` | Grace sebelum silent node marked unreachable. |
| `InstanceReclaimGrace` | `00:01:00` | Extra margin beyond `HeartbeatTtl` sebelum stranded instance di unreachable node di-reclaim (failed). |
| `MonitorInterval` | `00:00:30` | Seberapa sering monitor dan instance-reclaimer sweep. |
| `HeartbeatInterval` | `00:00:30` | Value dikembalikan ke agent sebagai suggested cadence. |

Agent (CtraderCliNode) — `NodeAgent`:

| Key | Meaning |
|-----|---------|
| `MainUrl` | Base URL dari main node. Kosong = manual registration mode (loop no-op). |
| `AdvertiseUrl` | URL main gunakan untuk reach **agent ini**. |
| `NodeName` | Unique name; default ke nama mesin jika blank. |
| `Mode` | `Run` / `Backtest` / `Mixed`. |
| `MaxInstances` | Capacity hint honoured oleh scheduler. |
| `HeartbeatIntervalSeconds` | Re-register cadence. |
| `JwtSecret` | Harus equal main's `JoinToken` — keduanya registration bearer dan dispatch JWT signing key. |

## Security model (v1)

Auto-registered node bagikan **satu cluster secret** (`JoinToken` == setiap agent's `JwtSecret`). Main sign setiap dispatch request sebagai 5-menit HS256 JWT dengan secret itu; agent validate. Requirement:
