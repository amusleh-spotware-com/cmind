---
description: "cMind berskala dengan usaha pengendali yang minimal. Dua beban kerja berkeadaan — pelaksanaan jalankan/ujian belakang, salinan perdagangan — keduanya menggunakan pangkalan data sebagai titik koordinasi, jadi…"
---

# Penskalaan mendatar

cMind berskala dengan usaha pengendali yang minimal. Dua beban kerja berkeadaan — pelaksanaan jalankan/ujian belakang, salinan perdagangan — keduanya menggunakan pangkalan data sebagai titik koordinasi, jadi menambah replika memerlukan tiada koordinator luaran (tiada ZooKeeper, tiada pemilihan pemimpin).

## Salinan perdagangan (pajakan penyembuhan sendiri)

Setiap nod menjalankan `CopyEngineSupervisor` (pintu pada `App:Copy:Enabled`). Setiap kitaran penyerasian, penyelia:

1. **Tuntut** setiap profil yang berjalan tidak ditugaskan *atau* pajakan-tamat tempoh, dalam satu `UPDATE` atom — dua penyelia yang berlumba tidak pernah kedua-duanya menuntut profil yang sama, jadi profil disalin oleh betul-betul satu nod (tiada pesanan berganda).
2. **Baharui** pajakan pada profil yang diurusnya.
3. Hos profil yang ditugaskan, tolak putaran token akses ke hos yang berjalan di tempat (tiada jatuh aliran peristiwa).

Kemalangan nod → berhenti memperbaharui; sebaik sahaja `App:Copy:LeaseTtl` berlalu, mana-mana nod yang bertahan menuntut semula profilnya kitaran seterusnya, membina semula keadaan daripada penyerasian tanpa menduakan dagangan. **Penskalaan keluar** = tambah replika; profil yang tidak ditugaskan/bebas diambil secara automatik.

**Skala-dalam yang anggun / kemas kini berguling (S1)** = pada `SIGTERM`, `CopyEngineSupervisor.StopAsync` **melepaskan pajakan nod ini** (`AssignedNode`/`LeaseExpiresAt` → null) jadi penyelamat menuntutnya **tepat seterusnya** kitaran penyerasian — **bukan** selepas `LeaseTtl` penuh. Hanya kemalangan keras menunggu TTL. `terminationGracePeriodSeconds` ejen salinan (lalai 30) memberikan masa pelepasan untuk selesai sebelum polong dibunuh.

### Kenop (`App:Copy`)

| Tetapan | Lalai | Catatan |
|---------|---------|-------|
| `Enabled` | `palsu` | Beralih salinan pengehosan untuk nod. |
| `ReconcileInterval` | `30s` | Seberapa kerap nod tuntut/baharui/penyerasian. |
| `LeaseTtl` | `120s` | Belas kasihan sebelum profil nod senyap dituntut semula. Simpan beberapa kitaran penyerasian jadi kitaran lambat tidak menyebabkan penyerahan palsu. |
| `NodeName` | nama mesin | Tetapkan secara berbeza apabila dua penyelia berkongsi hos. |

Di Kubernetes penyelia salinan berjalan sebagai Penempatan; tetapkan `replika` kepada paralelisme yang diinginkan. Setiap polong mendapat `NodeName` yang stabil (lalai: nama hos polong), jadi pajakan dikaitkan setiap polong. Pangkalan data adalah sumber kebenaran tunggal — tiada sesi melekit, tiada keadaan setiap polong untuk dimigrasikan.

**Taburan seimbang (S4):** tetapkan `App:Copy:MaxProfilesPerNode` > 0 untuk menghadkan berapa banyak profil yang berjalan hos nod. Setiap penyelia kemudian tuntut **paling banyak** ruang kepala yang tinggal melalui tuntutan terikat atom `FOR UPDATE SKIP LOCKED`, jadi profil **tersebar** merentasi replika daripada penyelia pertama menangkap semua — tiada polong panas tunggal / SPOF. Tuntutan skip-locked menjaga jaminan "betul-betul satu nod setiap profil" (tiada hos berganda) bahkan di bawah tuntutan serentak. `0` (lalai) = terikat (satu nod mengurus semuanya, tidak berubah).

**Pada skala (S7/S8):** setiap polong merengek penyerasian hingga 20% daripada `ReconcileInterval` (`CopyEngineSupervisor.JitteredInterval`) jadi N replika tidak tembakan tuntutan/baharui `UPDATE` serentak (Postgres thundering-herd). Apabila `copyAgent.replicas > 1` carta juga tersebar replika merentasi nod (`topologySpreadConstraints`) dan menambah `PodDisruptionBudget` (`minAvailable: 1`) jadi saliran/naik taraf tidak pernah mengambil kapasiti salinan kepada sifar.

## Pelaksanaan jalankan/ujian belakang

`NodeScheduler` memilih nod yang paling sedikit dimuatkan yang layak demi `MaxInstances`; ejen nod jauh pendaftaran sendiri dan denyut nadi (`App:Discovery`), `NodeHeartbeatMonitor` menandai nod tidak dapat dicapai apabila denyut nadi melebihi `Discovery:HeartbeatTtl`. Tambah ejen nod untuk menambah kapasiti pelaksanaan; ejen mati dihalakan secara automatik.

## Migrasi pada penskalaan / penempatan berguling

Setiap replika Web/MCP menjalankan `OwnerSeeder` pada permulaan, yang memohon migrasi EF dan benih pemilik. Untuk menjadikan itu selamat apabila N replika bermula pada saat yang sama, migrasikan + benih jalankan di dalam **kunci penasihat sesi Postgres** (`MigrationLock.RunExclusiveAsync`, kunci `DatabaseDefaults.MigrationAdvisoryLockKey`):
