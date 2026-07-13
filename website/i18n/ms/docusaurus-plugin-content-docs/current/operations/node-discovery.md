---
description: "Nod CLI cTrader bergabung kluster dengan pendaftaran sendiri + denyut nadi — tiada kemasukan manual. Pola yang sama seperti ejen Consul/Nomad/kubeadm: ejen but mengetahui lokasi nod utama…"
---

# Penemuan auto nod

Nod CLI cTrader bergabung kluster dengan **pendaftaran sendiri + denyut nadi** — tiada kemasukan manual. Pola yang sama seperti ejen Consul/Nomad/kubeadm: ejen but mengetahui lokasi nod utama + rahsia kluster bersama, kemudian terus mengumumkan dirinya.

> Disahkan hujung ke hujung pada Docker Compose dan kluster `kind` Kubernetes: ejen pendaftaran sendiri, muncul dalam DB yang dapat dicapai, secara automatik ditandai tidak dapat dicapai apabila denyut nadi berhenti lalu TTL, kembali dalam talian apabila resume.

## Bagaimana ia berfungsi

```
Ejen CtraderCliNode                         Utama (Web)
------------------                         ----------
POST /api/nodes/register  ── token bergabung ──▶ mengesahkan token (masa tetap)
  { nama, baseUrl, mod,                    mengesahkan versi protokol
    maxInstances, dataDir,                  nilai CtraderCliNode mengikut nama
    protocolVersion }                        cap waktu LastHeartbeatAt, IsReachable=benar
        ▲                                     └─ CtraderCliNode.SelfRegister / RecordHeartbeat
        │  setiap HeartbeatInterval            NodeHeartbeatMonitor (latar belakang):
        └──────────────────────────────────── jika sekarang - LastHeartbeatAt > HeartbeatTtl
                                                 → CtraderCliNode.MarkUnreachable() (NodeWentOffline)
```

- **Pendaftaran == denyut nadi.** Ejen re-POST pada `HeartbeatIntervalSeconds`. Panggilan pertama membuat nod (`NodeRegistered` peristiwa); panggilan kemudian menyegarkan ketidakpastian. Denyut nadi yang disambung semula selepas ketiadaan membalikkan nod kembali dapat dicapai (`NodeCameOnline`).
- **Penyerasian ketidakpastian.** `NodeHeartbeatMonitor` menandai nod yang denyut nadi terakhirnya melebihi `HeartbeatTtl` tidak dapat dicapai. Penjadual (`IsActive`/`AcceptsRun`/`AcceptsBacktest` pintu di atas kebolehcapaian) berhenti meletakkan kerja sehingga mereka laporkan lagi.
- **Tuntutan contoh yatim piatu.** `NodeInstanceReclaimer` (latar belakang) peralihan mana-mana contoh terminal yang tersesat di nod yang tidak dapat dicapai untuk **Gagal** (`FailureReason = "Node tidak dapat dicapai - contoh dituntut kembali"`, `InstanceFailed` peristiwa domain → pemberitahuan pengguna), jadi nod yang jatuh/terbahagi tidak boleh pernah meninggalkan contoh yang tersekat "Berjalan" selama-lamanya. Tuntutan hanya tembakan sebaik sahaja denyut nadi terakhir nod sudah lama melampaui `HeartbeatTtl + InstanceReclaimGrace`, memberikan blip ringkas peluang untuk pulih terlebih dahulu. Contoh yang dituntut kembali **tidak dijadualkan semula secara automatik**: nod yang terbahagi-tetapi-hidup mungkin masih melaksanakan bekas dan tiada pagar peringkat bekas, jadi peluncuran semula akan mengambil risiko pelaksanaan berganda — pengguna memulai semula larian yang dituntut secara sengaja. Ujian belakang berhenti sendiri, jadi ujian belakang yang dituntut kembali hanya dijalankan semula.
- **Identiti adalah nama nod.** Utama nilai mengikut `NodeName`, jadi polong yang IP/URL berubah semasa mula semula menjaga identiti, pendaftaran semula `AdvertiseUrl` baru.
- **Mod ditetapkan pada pendaftaran pertama.** Mod nod (`Run`/`Backtest`/`Mixed`) adalah jenis yang bertahan, tidak boleh berubah pada denyut nadi; pendaftaran semula dengan mod yang berbeza dihormati untuk ketidakpastian tetapi perubahan mod diabaikan (dicatat sebagai amaran). Untuk menukar mod: padam nod, biarkan ia mendaftar semula.

## Konfigurasi

Utama (Web) — `App:Discovery`:

| Kunci | Lalai | Makna |
|-----|---------|---------|
| `Enabled` | `palsu` | Suis utama untuk titik akhir daftar + monitor. |
| `JoinToken` | — | Rahsia kluster bersama (≥ 32 char) ejen mesti persembahan. |
| `HeartbeatTtl` | `00:01:30` | Belas kasihan sebelum nod senyap ditandai tidak dapat dicapai. |
| `InstanceReclaimGrace` | `00:01:00` | Margin tambahan di luar `HeartbeatTtl` sebelum contoh tersesat di nod yang tidak dapat dicapai dituntut kembali (gagal). |
| `MonitorInterval` | `00:00:30` | Seberapa kerap monitor dan instance-reclaimer menyapu. |
| `HeartbeatInterval` | `00:00:30` | Nilai dikembalikan kepada ejen sebagai bilangan kuat yang disyorkan. |

Ejen (CtraderCliNode) — `NodeAgent`:

| Kunci | Makna |
|-----|---------|
| `MainUrl` | URL asas nod utama. Kosong = mod pendaftaran manual (gelung no-op). |
| `AdvertiseUrl` | URL utama digunakan untuk mencapai **ejen ini**. |
| `NodeName` | Nama unik; lalai kepada nama mesin jika kosong. |
| `Mode` | `Run` / `Backtest` / `Mixed`. |
| `MaxInstances` | Petunjuk kapasiti dihormati oleh penjadual. |
| `HeartbeatIntervalSeconds` | Pendaftaran semula bilangan kuat. |
| `JwtSecret` | Mesti sama dengan `JoinToken` utama — kunci pembawa pendaftaran dan penandatanganan JWT hantar kedua-duanya. |

## Model keselamatan (v1)

Nod yang terdaftar automatik berkongsi **satu rahsia kluster** (`JoinToken` == `JwtSecret` setiap ejen). Utama menandatangani setiap permintaan hantar sebagai JWT HS256 5-minit dengan rahsia itu; ejen mengesahkan. Keperluan:

- Simpan `JoinToken` ≥ 32 char dan pusingannya (kemaskini `App:Discovery:JoinToken` utama dan `NodeAgent:JwtSecret` setiap ejen bersama).
- Tamatkan TLS di hadapan utama dan ejen dalam produksi (proksi terbalik / masuk).
- Ejen masih hanya menjalankan imej yang sepadan dengan `AllowedImagePrefix`.

**Pengerasan susulan (bukan v1):** keluarkan rahsia unik setiap nod pada pendaftaran (kubeadm-gaya but → kredensial setiap nod) jadi ejen berkompromis tunggal tidak boleh merancang token penghantaran untuk rakan sejawatan. Aliran pendaftaran sudah mengembalikan badan respons — tempat semula jadi untuk menyerahkan rahsia setiap nod yang dicetak kembali.

## Nod manual masih berfungsi

`POST /api/nodes` (UI pentadbir) terus mendaftar nod yang disematkan dengan rahsia setiap nod sendiri. Penemuan adalah tambahan.

Penempatan label-putih boleh **sembunyikan kawalan manual** (atau seluruh permukaan Nod) dan bergantung semata-mata pada penemuan auto: `App:Branding:NodesUi=Monitor` jatuh tambah/padam manual, `Hidden` mengalih keluar nav, halaman dan API manual, dan `App:Branding:RestrictNodesToOwner` lantai permukaan di pemilik sahaja. Titik akhir pendaftaran sendiri + denyut nadi di sini tidak terjejas dalam setiap mod. Lihat
[Label-putih → Visibilitas UI Nod](../features/white-label.md#nodes-ui-visibility).
