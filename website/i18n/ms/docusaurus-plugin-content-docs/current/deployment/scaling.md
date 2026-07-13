---
description: "cMind berkembang secara melaras dengan usaha operator yang minima. Dua beban kerja berkod berat — pelaksanaan run/backtest, salin-perdagangan — kedua-duanya gunakan pangkalan data sebagai titik koordinasi, jadi…"
---

# Skala melaras mendatar

cMind berkembang secara melaras dengan usaha operator yang minima. Dua beban kerja berkod berat — pelaksanaan
run/backtest, salin-perdagangan — kedua-duanya gunakan pangkalan data sebagai titik koordinasi, jadi menambah
replika tidak memerlukan penyelaras luaran (tiada ZooKeeper, tiada pemilihan pemimpin).

## Salin-perdagangan (pajak sendiri-penyembuhan)

Setiap nod menjalankan `CopyEngineSupervisor` (digerbang pada `App:Copy:Enabled`). Setiap kitaran
sepadan, penyelia:

1. **Menuntut** setiap profil berjalan yang tidak diperuntukkan *atau* pajak luput, dalam satu `UPDATE` atomik —
   dua penyelia yang berlumba tidak pernah menuntut profil yang sama, jadi profil disalin oleh tepat satu
   nod (tiada pesanan berganda).
2. **Memperbaharui** pajak pada profil yang dihosnya.
3. Menghos profil yang diperuntukkan, menolak rotación token akses ke hos yang berjalan di tempat (tiada
   penurunan strim acara).

Nod lumpuh → berhenti memperbaharui; sebaik `App:Copy:LeaseTtl` lulus, mana-mana nod yang hidup menuntut
semula profilnya pada kitaran seterusnya, membina semula keadaan dari sepadan tanpa menduaikan perdagangan. **Skala
keluar** = tambah replika; profil yang tidak diperuntukkan/diperolehi diambil secara automatik.

**Skala masuk beransur-ansur / pengemaskinian bergulir (S1)** = pada `SIGTERM`, `CopyEngineSupervisor.StopAsync`
**membebaskan pajak nod ini** (`AssignedNode`/`LeaseExpiresAt` → null) supaya yang hidup menuntut semula pada
kitaran sepadan *seterusnya* — **bukan** selepas penuh `LeaseTtl`. Hanya lumpuh keras menunggu TTL.
`terminationGracePeriodSeconds` ejen salin (lalai 30) memberi masa pembebasan untuk selesai sebelum pod dibunuh.

### Knob (`App:Copy`)

| Tetapan | Lalai | Nota |
|---------|-------|------|
| `Enabled` | `false` | Hidupkan penghosan salin untuk nod. |
| `ReconcileInterval` | `30s` | Kekerapan nod menuntut/memperbaharui/sepadan. |
| `LeaseTtl` | `120s` | Gracia sebelum profil nod senyap dituntut semula. Simpan beberapa selang sepadan supaya kitaran perlahan tidak menyebabkan pemindahan palsu. |
| `NodeName` | nama mesin | Tetapkan secara berbeza apabila dua penyelia berkongsi hos. |

Pada Kubernetes penyelia salin berjalan sebagai Deployment; tetapkan `replicas` kepada selari yang dikehendaki. Setiap
pod mendapat `NodeName` stabil (lalai: nama host pod), jadi pajak diagihkan setiap pod. Pangkalan data ialah
sumber kebenaran tunggal — tiada sesi melekit, tiada keadaan setiap pod untuk dipindahkan.

**Taburan seimbang (S4):** tetapkan `App:Copy:MaxProfilesPerNode` > 0 untuk mengehadkan berapa banyak profil berjalan
yang dihos oleh satu nod. Setiap penyelia kemudian menuntut **sebanyak-banyaknya** ruang selebihnya melalui tuntutan
terikat atomik `FOR UPDATE SKIP LOCKED`, jadi profil **tersebar** merentasi replika ganti-buat pertama
penyelia mengambil semua — tiada pod panas tunggal / SPOF. Tuntutan skip-locked mengekalkan jaminan "tepat satu nod
per profil" (tiada penghosan berganda) walaupun under klaim selari. `0` (lalai) =
tidak terikat (satu nod menghos semuanya, tidak berubah).

**Pada skala (S7/S8):** setiap pod mengherot sepadan sehingga 20% daripada `ReconcileInterval`
(`CopyEngineSupervisor.JitteredInterval`) supaya N replika tidak mencetuskan `UPDATE` menuntut/memperbaharui
serentak (thundering-herd Postgres). Apabila `copyAgent.replicas > 1` carta juga menyebar
replika merentasi nod (`topologySpreadConstraints`) dan menambah `PodDisruptionBudget`
(`minAvailable: 1`) supaya longkang/upgrade tidak pernah mengambil kapasiti salin kepada sifar.

## Pelaksanaan run/backtest

`NodeScheduler` memilih nod yang layak paling kurang beban sambil menghormati `MaxInstances`; ejen nod jarak jauh
mendaftar sendiri dan heartbeat (`App:Discovery`), `NodeHeartbeatMonitor` menanda nod tidak dapat dicapai
apabila heartbeat melebihi `Discovery:HeartbeatTtl`. Tambah ejen nod untuk menambah kapasiti pelaksanaan;
ejen yang mati dilalukan secara automatik.

## Migrasi pada skala keluar / penggunaan bergulir

Setiap replika Web/MCP menjalankan `OwnerSeeder` pada permulaan, yang menggunakan migrasi EF dan menyemai pemilik.
Untuk menjadikan itu selamat apabila N replika bermula sekaligus, migrasi + seeding berjalan di dalam **kunci
nasihat sesi Postgres** (`MigrationLock.RunExclusiveAsync`, kunci `DatabaseDefaults.MigrationAdvisoryLockKey`):
replika pertama yang memperolehnya bermigrasi dan menyemai; yang lain menyekat pada kunci, kemudian dapati migrasi
sudah digunakan (no-op) dan pemilik sudah ada. Tiada kerja migrasi atau pemilihan pemimpin yang berasingan.
Jika anda menambah seeding pertama-run, letakkannya **di dalam** blok yang dijaga yang sama supaya ia penulis-satu.

## Ketahanan HTTP ejen nod

Nod utama bercakap dengan setiap ejen `CtraderCliNode` melalui HTTP melalui tiga klien berpecah tujuan supaya nod
atau rangkaian yang tidak stabil tidak pernah merosakkan keadaan:

- **baca** (`status` / `report` / `stats`) — GET idempoten, dicuba semula pada kegagalan transient
  (exponential backoff + jitter, `NodeAgentHttp.ReadRetryCount`) dengan masa tunggu setiap percubaan dan keseluruhan.
- **tulis** (`start` / `stop` / `clean`) — POST bukan idempoten, had masa tetapi **tidak pernah dicuba semula**:`
  `start` yang dicuba semula boleh melancarkan container dua kali.
- **strim** (`logs`) — strim `docker logs -f` yang berpanjangan mendapat masa tunggu infiniti dan tiada
  saluran ketahanan, supaya tailing tidak pernah dipotong.

Nod yang kekal tidak dapat dicapai dikendalikan oleh heartbeat + [tuntutan semula contoh yatim](../operations/node-discovery.md);
lapisan HTTP hanya melicinkan masalah transient.

## Tier tanpa keadaan

Web (Blazor Server + API) dan pelayan MCP tanpa keadaan di belakang pangkalan data, replika secara bebas.
Auth berdasarkan kuki; skala Web secara mendatar di belakang pengimbus beban. Pelayan MCP ialah
proses/Deployment berasingan jadi ia berskala secara bebas daripada Web.

## Ketahanan sambungan pangkalan data

Setiap hos yang membuka pangkalan data menggunakan **strategi pelaksanaan yang mencuba semula** supaya
putus sambungan transient atau gagal lebihan Postgres yang diurus (RDS / Flexible Server patching) dicuba semula ganti-buat
memperluahkan sebagai ralat kepada pengguna:

- Web dan MCP mendaftarkan konteks melalui komponen Npgsql Aspire dengan `DisableRetry=false`
  dan `CommandTimeout` jelas (`DatabaseDefaults.CommandTimeoutSeconds`).
- CopyAgent (bukan Aspire) mendaftar melalui `UseAppNpgsql`, yang menggunakan yang sama
  `EnableRetryOnFailure(MaxRetryCount, MaxRetryDelay)` + masa tunggu perintah dari `DatabaseDefaults`.

Semua tulis ialah `SaveChanges` tunggal / `ExecuteUpdate` tunggal / pernyataan `ExecuteSql` tunggal, jadi
strategi mencuba semula adalah selamat (tiada transaksi pelbagai-pernyataan memerlukan pembungkus manual
`strategy.ExecuteAsync`). Jika anda menambah transaksi manual atau berbilang `SaveChanges` dalam satu operasi logik,
bungkusnya dalam `db.Database.CreateExecutionStrategy().ExecuteAsync(...)` — jika tidak ia membaling di bawah percubaan semula.

## Senarai semak untuk skala keluar

- [ ] Postgres bersaiz untuk beban sambungan tambahan (setiap replika Web/MCP/nod membuka kumpulan).
- [ ] `App:Copy:Enabled=true` pada setiap nod yang harus menghos profil salin.
- [ ] `App:Copy:NodeName` berbeza setiap penyelia yang berada bersama (K8s: lalai setiap pod baik).
- [ ] `LeaseTtl` ≥ 3× `ReconcileInterval`.
- [ ] Ejen nod digunakan di mana Dockeristimewa tersedia (AKS/EKS/EC2/VM, bukan Fargate).
- [ ] Web berbilang replika: tetapkan rentetan sambungan `signalr` (backplane Redis) **dan** hidupkan
      pertalian sesi ingress (sesi melekit) supaya litar Blazor sambung semula ke pod hidup. Pengecualian komponen
      ditangkap oleh `ErrorBoundary` `MainLayout` (percubaan semula mesra, litar kekal hidup).
