---
description: "cMind scale out dengan minimal operator effort. Dua stateful workload — run/backtest execution, copy-trading — keduanya gunakan database sebagai coordination point, jadi..."
---

# Horizontal scaling

cMind scale out dengan minimal operator effort. Dua stateful workload — run/backtest execution, copy-trading — keduanya gunakan database sebagai coordination point, jadi menambah replica tidak memerlukan external coordinator (tidak ada ZooKeeper, tidak ada leader election).

## Copy-trading (self-healing lease)

Setiap node menjalankan `CopyEngineSupervisor` (gated pada `App:Copy:Enabled`). Setiap reconcile cycle, supervisor:

1. **Claims** setiap running profile unassigned *atau* lease-lapsed, dalam satu atomic `UPDATE` — dua racing supervisor tidak pernah claim profile yang sama, jadi profile dicopy oleh exactly one node (tidak ada double order).
2. **Renews** lease pada profile yang di-host.
3. Host assigned profile, push access-token rotation ke running host in place (tidak ada event-stream drop).

Node crash → stop renew; sekali `App:Copy:LeaseTtl` pass, surviving node apa pun reclaim profile-nya next cycle, rebuild state dari reconcile tanpa duplicate trade. **Scale out** = tambah replica; unassigned/free profile diambil otomatis.

**Graceful scale-in / rolling update (S1)** = pada `SIGTERM`, `CopyEngineSupervisor.StopAsync` **release node lease-nya** (`AssignedNode`/`LeaseExpiresAt` → null) jadi survivor reclaim mereka *very next* reconcile cycle — **bukan** setelah full `LeaseTtl`. Hanya hard crash tunggu TTL. `terminationGracePeriodSeconds` copy-agent (default 30) beri release time untuk finish sebelum pod killed.

### Knob (`App:Copy`)

| Setting | Default | Notes |
|---------|---------|-------|
| `Enabled` | `false` | Turn copy hosting on untuk node. |
| `ReconcileInterval` | `30s` | Seberapa sering node claim/renew/reconcile. |
| `LeaseTtl` | `120s` | Grace sebelum silent node profile-nya di-reclaim. Simpan beberapa reconcile interval jadi slow cycle tidak cause spurious hand-off. |
| `NodeName` | nama mesin | Set distinctly saat dua supervisor bagikan host. |

Di Kubernetes copy supervisor berjalan sebagai Deployment; set `replicas` ke desired parallelism. Setiap pod mendapat stable `NodeName` (default: pod hostname), jadi lease attributed per pod. Database adalah single source of truth — tidak ada sticky session, tidak ada per-pod state untuk migrate.

**Balanced distribution (S4):** set `App:Copy:MaxProfilesPerNode` > 0 untuk cap berapa banyak running profile node host. Setiap supervisor kemudian claim **at most** remaining headroom-nya via atomic `FOR UPDATE SKIP LOCKED` bounded claim, jadi profile **spread** melintasi replica daripada first supervisor grab semua — tidak ada single hot pod / SPOF. Skip-locked claim simpan "exactly one node per profile" guarantee (tidak ada double-hosting) bahkan di bawah concurrent claim. `0` (default) = unbounded (satu node host semuanya, unchanged).

**Di skala (S7/S8):** setiap pod jitter reconcile oleh hingga 20% dari `ReconcileInterval` (`CopyEngineSupervisor.JitteredInterval`) sehingga N replica tidak fire claim/renew `UPDATE` simultaneously (Postgres thundering-herd). Saat `copyAgent.replicas > 1` chart juga spread replica melintasi node (`topologySpreadConstraints`) dan tambah `PodDisruptionBudget` (`minAvailable: 1`) jadi drain/upgrade tidak pernah take copy capacity ke zero.

## Run/backtest execution
