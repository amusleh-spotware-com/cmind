---
description: "cTrader CLI nodes เข้า cluster โดย self-registration + heartbeat — ไม่มี manual entry Same pattern เช่น Consul/Nomad/kubeadm agents: agent boots ทำให้ main node…"
---

# Node auto-discovery

cTrader CLI nodes เข้า cluster โดย **self-registration + heartbeat** — ไม่มี manual entry Same pattern เช่น Consul/Nomad/kubeadm agents: agent boots ทำให้ main node location + shared cluster secret แล้ว continuously announces ตัวเอง

> Verified end-to-end บน Docker Compose และ `kind` Kubernetes cluster: agents self-register ปรากฏใน DB reachable auto-marked unreachable เมื่อ heartbeats หยุดอดีต TTL return online เมื่อ resume

## วิธีการทำงาน

```
CtraderCliNode agent                         Main (Web)
------------------                         ----------
POST /api/nodes/register  ── join token ──▶ verify token (constant-time)
  { name, baseUrl, mode,                    verify protocol version
    maxInstances, dataDir,                   upsert CtraderCliNode โดย name
    protocolVersion }                        stamp LastHeartbeatAt, IsReachable=true
        ▲                                     └─ CtraderCliNode.SelfRegister / RecordHeartbeat
        │  every HeartbeatInterval            NodeHeartbeatMonitor (background):
        └──────────────────────────────────── if now - LastHeartbeatAt > HeartbeatTtl
                                                 → CtraderCliNode.MarkUnreachable() (NodeWentOffline)
```

- **Registration == heartbeat.** Agent re-POSTs บน `HeartbeatIntervalSeconds` First call creates node (`NodeRegistered` event); later calls refresh liveness Resumed heartbeat หลัง outage flips node back reachable (`NodeCameOnline`)
- **Liveness reconciliation.** `NodeHeartbeatMonitor` marks nodes ที่มี last heartbeat exceed `HeartbeatTtl` unreachable Scheduler (`IsActive`/`AcceptsRun`/`AcceptsBacktest` gated บน reachability) หยุด placing งาน จนกว่า พวกเขาจะรายงาน
- **Orphaned-instance reclaim.** `NodeInstanceReclaimer` (background) transitions ใด ๆ non-terminal instance stranded บน unreachable node ไป **Failed** (`FailureReason = "Node unreachable - instance reclaimed"` `InstanceFailed` domain event → user notification) ดังนั้น crashed/partitioned node สามารถไม่เคย ปล่อย instance stuck "Running" forever Reclaim ยิงเพียงครั้งเดียว node ของ last heartbeat เป็น stale ไกลกว่า `HeartbeatTtl + InstanceReclaimGrace` ให้ brief-blip โอกาส recover ก่อน Reclaimed **runs ไม่ใช่ auto-rescheduled**: partitioned-but-alive node อาจยังคงดำเนิน container และไม่มี container-level fencing ดังนั้น re-launching จึงเสี่ยงต่อ double execution — ผู้ใช้ restarts reclaimed run deliberately Backtests self-exit ดังนั้น reclaimed backtest เป็นเพียง re-run
- **Identity เป็น node name.** Main upserts โดย `NodeName` ดังนั้น pod ที่มี IP/URL changes บน restart keeps identity re-registers new `AdvertiseUrl`
- **Mode fixed ที่ first registration.** Node mode (`Run`/`Backtest`/`Mixed`) เป็น persisted type ไม่สามารถ change บน heartbeat; re-registration ด้วย mode ต่างไป honoured สำหรับ liveness แต่ mode change ignored (logged เป็น warning) เพื่อ change mode: delete node ให้มัน re-register

## Configuration

Main (Web) — `App:Discovery`:

| Key | Default | Meaning |
|-----|---------|---------|
| `Enabled` | `false` | Master switch สำหรับ register endpoint + monitor |
| `JoinToken` | — | Shared cluster secret (≥ 32 chars) agents ต้อง present |
| `HeartbeatTtl` | `00:01:30` | Grace ก่อน silent node marked unreachable |
| `InstanceReclaimGrace` | `00:01:00` | Extra margin ไกลกว่า `HeartbeatTtl` ก่อนที่ stranded instance บน unreachable node จะ reclaimed (failed) |
| `MonitorInterval` | `00:00:30` | How often monitor และ instance-reclaimer sweep |
| `HeartbeatInterval` | `00:00:30` | Value returned ไป agents เป็น suggested cadence |

Agent (CtraderCliNode) — `NodeAgent`:

| Key | Meaning |
|-----|---------|
| `MainUrl` | Base URL ของ main node Empty = manual registration mode (loop no-op) |
| `AdvertiseUrl` | URL main ใช้ เพื่อถึง **this** agent |
| `NodeName` | Unique name; defaults เป็น machine name ถ้า blank |
| `Mode` | `Run` / `Backtest` / `Mixed` |
| `MaxInstances` | Capacity hint honoured โดย scheduler |
| `HeartbeatIntervalSeconds` | Re-register cadence |
| `JwtSecret` | ต้อง equal main ของ `JoinToken` — ทั้ง registration bearer และ dispatch JWT signing key |

## Security model (v1)

Auto-registered nodes share **one cluster secret** (`JoinToken` == ทุก agent ของ `JwtSecret`) Main signs ทุก dispatch request เป็น 5-minute HS256 JWT ด้วย secret นั้น; agent validates Requirements:

- Keep `JoinToken` ≥ 32 chars และ rotate มัน (update main ของ `App:Discovery:JoinToken` และ ทุก agent ของ `NodeAgent:JwtSecret` together)
- Terminate TLS front ของ main และ agents ใน production (reverse proxy / ingress)
- Agent ยังคง เรียกใช้เฉพาะรูปภาพที่ match `AllowedImagePrefix`

**Hardening follow-up (ไม่ v1):** issue unique per-node secret ที่ registration (kubeadm-style bootstrap → per-node credential) ดังนั้น single compromised agent สามารถไม่ forge dispatch tokens สำหรับ peers Registration flow แล้ว returns response body — natural place เพื่อ hand back minted per-node secret

## Manual nodes ยังคง work

`POST /api/nodes` (admin UI) ต่อเนื่อง register pinned nodes ด้วย own per-node secret Discovery เป็น additive
