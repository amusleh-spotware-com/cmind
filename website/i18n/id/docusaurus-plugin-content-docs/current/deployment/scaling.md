---
description: "cMind dapat diskala keluar dengan usaha operator minimal. Dua beban kerja stateful — eksekusi run/backtest dan copy-trading — keduanya menggunakan database sebagai titik koordinasi, sehingga…"
---

# Skala horizontal

cMind dapat diskala keluar dengan usaha operator minimal. Dua beban kerja stateful — eksekusi run/backtest
dan copy-trading — keduanya menggunakan database sebagai titik koordinasi, sehingga menambahkan replika tidak
membutuhkan coordinator eksternal (tanpa ZooKeeper, tanpa leader election).

## Copy-trading (lease self-healing)

Setiap node menjalankan `CopyEngineSupervisor` (gate pada `App:Copy:Enabled`). Setiap siklus reconcile,
supervisor:

1. **Mengklaim** setiap profil berjalan yang belum ditugaskan *atau* lease-nya lapse, dalam satu `UPDATE`
   atomik — dua supervisor yang racing tidak akan keduanya mengklaim profil yang sama, sehingga profil
   dicopy oleh tepat satu node (tanpa order ganda).
2. **Memperbarui** lease pada profil yang dihostingnya.
3. Menghosting profil yang ditugaskan, mendorong rotasi access-token ke host yang berjalan di tempat
   (tanpa drop event-stream).

Node crash → berhenti memperbarui; begitu `App:Copy:LeaseTtl` terlewati, node sobrevivor apapun mengklaim
ulang profilnya di siklus reconcile berikutnya, membangun ulang state dari reconcile tanpa menduplikasi trade.
**Scale out** = tambahkan replika; profil bebas/tidak ditugaskan diambil secara otomatis.

**Scale-in graceful / rolling update (S1)** = pada `SIGTERM`, `CopyEngineSupervisor.StopAsync`
**merilis lease node ini** (`AssignedNode`/`LeaseExpiresAt` → null) sehingga survivornya mengklaim ulang
di siklus reconcile **berikutnya yang sangat prochain** — **bukan** setelah full `LeaseTtl`. Hanya
hard crash yang menunggu TTL. `terminationGracePeriodSeconds` copy-agent (default 30) memberi waktu
pelepasan untuk selesai sebelum pod dibunuh.

### Knob (`App:Copy`)

| Pengaturan | Default | Catatan |
|---------|---------|---------|
| `Enabled` | `false` | Aktifkan hosting copy untuk node. |
| `ReconcileInterval` | `30s` | Seberapa sering node mengklaim/memperbarui/reconcile. |
| `LeaseTtl` | `120s` | Waktu sebelum profil node yang diam diam diklaim ulang. Jaga beberapa interval reconcile agar siklus lambat tidak menyebabkan handover spurious. |
| `NodeName` | nama mesin | Set secara berbeda ketika dua supervisor berbagi host. |

Di Kubernetes, supervisor copy berjalan sebagai Deployment; set `replicas` ke paralelisme yang diinginkan.
Setiap pod mendapat `NodeName` stabil (default: hostname pod), sehingga lease attribut per pod. Database
adalah sumber kebenaran tunggal — tanpa sticky session, tanpa state per-pod yang perlu dimigrasikan.

**Distribusi seimbang (S4):** set `App:Copy:MaxProfilesPerNode` > 0 untuk membatasi berapa banyak profil
berjalan yang dihosting satu node. Setiap supervisor kemudian mengklaim **maksimal** headroom tersisa
melalui klaim atomik `FOR UPDATE SKIP LOCKED`, sehingga profil **tersebar** di seluruh replika alih-alih
supervisor pertama mengambil semua — tidak ada pod panas tunggal / SPOF. Klaim skip-locked menjaga
jaminan "tepat satu node per profil" (tanpa double-hosting) bahkan di bawah klaim konkuren. `0` (default)
= tak terbatas (satu node menghosting segalanya, tidak berubah).

**Pada skala besar (S7/S8):** setiap pod mengacak reconcile hingga 20% dari `ReconcileInterval`
(`CopyEngineSupervisor.JitteredInterval`) sehingga N replika tidak memicu klaim/perbarui `UPDATE`
secara simultan (Postgres thundering-herd). Ketika `copyAgent.replicas > 1`, chart juga menyebar
replika di seluruh node (`topologySpreadConstraints`) dan menambahkan `PodDisruptionBudget`
(`minAvailable: 1`) sehingga drain/upgrade tidak pernah membuat kapasitas copy menjadi nol.

## Eksekusi run/backtest

`NodeScheduler` memilih node eligible dengan beban paling sedikit dengan menghormati `MaxInstances`; remote
node agent mendaftarkan diri dan heartbeat (`App:Discovery`), `NodeHeartbeatMonitor` menandai node
tidak terjangkau ketika heartbeat melebihi `Discovery:HeartbeatTtl`. Tambahkan node agent untuk menambah
kapasitas eksekusi; agent mati di-route secara otomatis.

## Migrasi pada scale-out / rolling deploy

Setiap replika Web/MCP menjalankan `OwnerSeeder` saat startup, yang menerapkan migrasi EF dan menabur owner.
Agar ini aman ketika N replika mulai sekaligus, migrasi + seed berjalan di dalam **Postgres session
advisory lock** (`MigrationLock.RunExclusiveAsync`, key `DatabaseDefaults.MigrationAdvisoryLockKey`):
replika pertama yang mendapatkannya bermigrasi dan menabur; yang lain block pada lock, lalu menemukan
migrasi sudah diterapkan (no-op) dan owner sudah ada. Tidak butuh job migrasi terpisah atau leader election.
Jika menambahkan penaburan first-run, letakkan **di dalam** blok yang sama sehingga single-writer.

## Ketahanan HTTP node-agent

Node utama berkomunikasi dengan setiap agent `CtraderCliNode` melalui HTTP melalui tiga client yang
dibagi sesuai tujuan sehingga node atau jaringan yang tidak stabil tidak pernah merusak state:

- **read** (`status` / `report` / `stats`) — GET idempoten, di-retry pada kegagalan transient
  (exponential backoff + jitter, `NodeAgentHttp.ReadRetryCount`) dengan timeout per-attempt dan total.
- **write** (`start` / `stop` / `clean`) — POST non-idempoten, di-timeout tapi **tidak pernah di-retry**:
  retry `start` yang diulang bisa melaunch container dua kali.
- **stream** (`logs`) — stream `docker logs -f` yang jangka panjang mendapat timeout tak terbatas dan
  tanpa pipeline ketahanan, sehingga tailing tidak pernah terputus.

Node yang tetap tidak terjangkau ditangani oleh heartbeat + [orphaned-instance reclaim](../operations/node-discovery.md);
layer HTTP hanya menghaluskan glitch transient.

## Tier stateless

Web (Blazor Server + API) dan MCP server stateless di belakang database, bereplikasi dengan bebas.
Auth berbasis cookie; skala Web secara horizontal di belakang load balancer. MCP server adalah
proses/Deployment terpisah sehingga diskala secara independen dari Web.

## Ketahanan koneksi database

Setiap host yang membuka database menggunakan **retrying execution strategy** sehingga disconnect
transient atau failover managed-Postgres (RDS / Flexible Server patching) di-retry alih-alih
muncul sebagai error ke user:

- Web dan MCP mendaftarkan context melalui komponen Npgsql Aspire dengan `DisableRetry=false`
  dan `CommandTimeout` eksplisit (`DatabaseDefaults.CommandTimeoutSeconds`).
- CopyAgent (non-Aspire) mendaftarkan melalui `UseAppNpgsql`, yang menerapkan
  `EnableRetryOnFailure(MaxRetryCount, MaxRetryDelay)` + command timeout yang sama dari `DatabaseDefaults`.

Semua write adalah single `SaveChanges` / single `ExecuteUpdate` / single `ExecuteSql` statements,
sehingga strategi retry aman (tanpa transaksi multi-statement yang butuh `strategy.ExecuteAsync`
manual wrapping). Jika menambahkan transaksi manual atau multiple `SaveChanges` dalam satu operasi
logis, bungkus dalam `db.Database.CreateExecutionStrategy().ExecuteAsync(...)` — jika tidak,
akan throw saat retry.

## Checklist untuk scale out

- [ ] Postgres berukuran untuk beban koneksi tambahan (setiap replika Web/MCP/node membuka pool).
- [ ] `App:Copy:Enabled=true` pada setiap node yang harus hosting profil copy.
- [ ] `App:Copy:NodeName` berbeda per supervisor yang co-located (K8s: default per-pod sudah OK).
- [ ] `LeaseTtl` ≥ 3× `ReconcileInterval`.
- [ ] Node agent di-deploy di tempat yang Docker privileged tersedia (AKS/EKS/EC2/VM, bukan Fargate).
- [ ] Multi-replica Web: set string koneksi `signalr` (Redis backplane) **dan** aktifkan session
      affinity ingress (sticky sessions) sehingga circuit Blazor reconnect ke pod hidup. Komponen
      exception ditangkap oleh `MainLayout` `ErrorBoundary` (retry ramah, circuit tetap hidup).
