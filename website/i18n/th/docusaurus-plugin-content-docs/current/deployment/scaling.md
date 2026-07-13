---
description: "cMind scales out with minimal operator effort. Two stateful workloads — run/backtest execution, copy-trading — both use database as coordination point, so…"
---

# ปรับขนาดในแนวนอน

cMind ปรับขนาดออกด้วยความพยายามของผู้ดำเนินการน้อยที่สุด workloads stateful สองตัว — run/backtest
execution copy-trading — ทั้งคู่ใช้ database เป็นจุดประสานงาน ดังนั้นการเพิ่ม replicas ไม่จำเป็นต้อง
external coordinator (ไม่มี ZooKeeper ไม่มี leader election)

## Copy-trading (self-healing lease)

แต่ละ node เรียกใช้ `CopyEngineSupervisor` (gated on `App:Copy:Enabled`) ทุกรอบการ reconcile ตัวควบคุมดูแล:

1. **Claims** ทุก running profile ที่ไม่ได้รับมอบหมาย *หรือ* lease-lapsed ในอะตอม `UPDATE` หนึ่ง —
   supervisors สองตัวที่แข่งขันกันไม่มี claim profile เดียวกันเลยดังนั้น profile ถูก copied โดยโหนด
   เดียว (ไม่มีคำสั่งซ้ำ)
2. **Renews** lease บน profiles ที่เป็นเจ้าของ
3. Hosts assigned profiles ผลักดัน access-token rotations ไปยัง running host ที่เป็นอยู่ (ไม่มี
   event-stream drop)

Node crash → หยุดการต่ออายุ; เมื่อ `App:Copy:LeaseTtl` ผ่านไป node ที่เหลือใด ๆ จะ reclaim
profiles ของ node นั้น รอบถัดไป สร้างสถานะใหม่จาก reconcile โดยไม่สร้างสำเนาการซื้อขาย **Scaling
out** = เพิ่ม replicas; unassigned/free profiles เลือกอัตโนมัติ

**Graceful scale-in / rolling update (S1)** = บน `SIGTERM` `CopyEngineSupervisor.StopAsync`
**releases leases ของ node นี้** (`AssignedNode`/`LeaseExpiresAt` → null) ดังนั้น survivor reclaim พวกมัน
รอบ reconcile *ถัดไปเท่านั้น* — **ไม่ใช่** หลังจาก `LeaseTtl` เต็ม เฉพาะ hard crash รอ TTL
Copy-agent's `terminationGracePeriodSeconds` (default 30) ให้เวลา release ที่จะเสร็จก่อน
pod killed

### Knobs (`App:Copy`)

| Setting | Default | Notes |
|---------|---------|-------|
| `Enabled` | `false` | เปิด copy hosting สำหรับ node |
| `ReconcileInterval` | `30s` | ความถี่ node claims/renews/reconciles |
| `LeaseTtl` | `120s` | Grace ก่อน silent node's profiles reclaimed ให้ คุ้มรับ reconcile intervals ไม่กี่ตัว ดังนั้น slow cycle ไม่ทำให้เกิดการ hand-off spurious |
| `NodeName` | machine name | ตั้ง distinctly เมื่อ supervisors สองตัวแบ่งปันเจ้าของ |

บน Kubernetes copy supervisors เรียกใช้เป็น Deployment; ตั้ง `replicas` เป็นการขนานที่ต้องการ แต่ละ
pod ได้รับ stable `NodeName` (default: pod hostname) ดังนั้น leases ระบุ per pod Database เป็น
single source of truth — ไม่มี sticky sessions ไม่มี per-pod state ที่จะ migrate

**Balanced distribution (S4):** ตั้ง `App:Copy:MaxProfilesPerNode` > 0 เพื่อจำกัดจำนวน running
profiles ที่ node โฮสต์ Supervisor ตัวแต่ละตัว ออกอากาศ **ที่สุด** headroom ที่เหลือของมัน ผ่าน
atomic `FOR UPDATE SKIP LOCKED` bounded claim ดังนั้น profiles **spread** ข้าม replicas แทน
supervisor แรก ที่ยึด profiles ทั้งหมด — ไม่มี single hot pod / SPOF Skip-locked claim เก็บ
"โหนด exactly one ต่อ profile" guarantee (ไม่มี double-hosting) แม้ภายใต้ claims concurrent `0` (default)
= unbounded (โหนด one hosts สิ่งทั้งหมด ไม่เปลี่ยนแปลง)

**At scale (S7/S8):** แต่ละ pod jitters reconcile โดย up to 20% ของ `ReconcileInterval`
(`CopyEngineSupervisor.JitteredInterval`) ดังนั้น N replicas ไม่ยิง claim/renew `UPDATE`
simultaneously (Postgres thundering-herd) เมื่อ `copyAgent.replicas > 1` chart ยัง spread
replicas ข้าม nodes (`topologySpreadConstraints`) และเพิ่ม `PodDisruptionBudget` (`minAvailable: 1`)
ดังนั้น drain/upgrade ไม่เคยใช้ copy capacity เป็นศูนย์

## Run/backtest execution

`NodeScheduler` เลือก least-loaded eligible node honouring `MaxInstances`; remote node agents
ลงทะเบียนด้วยตนเองและ heartbeat (`App:Discovery`) `NodeHeartbeatMonitor` ทำเครื่องหมาย node unreachable
เมื่อ heartbeat เกิน `Discovery:HeartbeatTtl` เพิ่ม node agents เพื่อเพิ่มคุณสมบัติ execution;
dead agent route around อัตโนมัติ

## Migrations on scale-out / rolling deploy

ทุก Web/MCP replica เรียกใช้ `OwnerSeeder` ที่เริ่มต้น ซึ่งนำ EF migrations และ seeds owner
เพื่อให้มันปลอดภัยเมื่อ N replicas เริ่ม at once migrate + seed เรียกใช้ภายใน **Postgres session
advisory lock** (`MigrationLock.RunExclusiveAsync` key `DatabaseDefaults.MigrationAdvisoryLockKey`):
replica ตัวแรก acquire lock migrate และ seed; ส่วนที่เหลือ block on lock แล้ว find migrations
already applied (no-op) และ owner already present ไม่จำเป็นต้อง separate migration job หรือ leader election หากคุณเพิ่ม first-run seeding ให้ทำ **inside** guarded block เดียวกัน ดังนั้นมันเป็น single-writer

## Node-agent HTTP resilience

main node พูดคุยกับแต่ละ `CtraderCliNode` agent ผ่าน HTTP ผ่าน three purpose-split clients ดังนั้น
flaky node หรือ network ไม่เคยเสียหาย state:

- **read** (`status` / `report` / `stats`) — idempotent GETs retried on transient failures
  (exponential backoff + jitter `NodeAgentHttp.ReadRetryCount`) พร้อม per-attempt และ total timeouts
- **write** (`start` / `stop` / `clean`) — non-idempotent POSTs timed out แต่ **never retried**: a
  retried `start` อาจ double-launch container
- **stream** (`logs`) — the long-lived `docker logs -f` stream ได้รับ infinite timeout และ ไม่มี
  resilience pipeline ดังนั้น tailing ไม่เคยตัดออก

node ที่อยู่หนีไปได้ยาว ๆ ถูกจัดการโดย heartbeat + [orphaned-instance reclaim](../operations/node-discovery.md);
HTTP layer เฉพาะ smooths transient blips

## Stateless tiers

Web (Blazor Server + API) และ MCP server stateless อยู่หลัง database replicate freely
Auth cookie-based; ปรับขนาด Web แนวนอนอยู่หลัง load balancer MCP server separate
process/Deployment ดังนั้นมันปรับขนาดได้ independently ของ Web

## Database connection resilience

ทุก host ที่เปิด database ใช้ **retrying execution strategy** ดังนั้น transient
disconnect หรือ managed-Postgres failover (RDS / Flexible Server patching) ถูก retried แทน
surface เป็นข้อผิดพลาด user:

- Web และ MCP register context ผ่าน Aspire Npgsql component พร้อมคำจำกัด `DisableRetry=false`
  และ explicit `CommandTimeout` (`DatabaseDefaults.CommandTimeoutSeconds`)
- CopyAgent (non-Aspire) registers ผ่าน `UseAppNpgsql` ซึ่งใช้ เดียวกัน
  `EnableRetryOnFailure(MaxRetryCount, MaxRetryDelay)` + command timeout จาก `DatabaseDefaults`

ทั้งหมด writes single `SaveChanges` / single `ExecuteUpdate` / single `ExecuteSql` statements ดังนั้น
retrying strategy ปลอดภัย (ไม่มี multi-statement transaction ต้องการ manual `strategy.ExecuteAsync`
wrapping) หากคุณเพิ่ม manual transaction หรือ multiple `SaveChanges` ใน logical operation หนึ่ง
wrap ใน `db.Database.CreateExecutionStrategy().ExecuteAsync(...)` — มิฉะนั้นจะ throw under retry

## Checklist สำหรับ scaling out

- [ ] Postgres sized สำหรับ added connection load (แต่ละ Web/MCP/node replica เปิด pool)
- [ ] `App:Copy:Enabled=true` บน ทุก node ที่ควร host copy profiles
- [ ] Distinct `App:Copy:NodeName` per co-located supervisor (K8s: default per-pod fine)
- [ ] `LeaseTtl` ≥ 3× `ReconcileInterval`
- [ ] Node agents deployed ที่ privileged Docker available (AKS/EKS/EC2/VM ไม่ใช่ Fargate)
- [ ] Multi-replica Web: ตั้ง `signalr` connection string (Redis backplane) **และ** เปิด ingress
      session affinity (sticky sessions) ดังนั้น Blazor circuit reconnects ไปยัง live pod ที่เป็นอยู่ component
      exception ถูก caught โดย `MainLayout` `ErrorBoundary` (friendly retry circuit stays alive)
