---
description: "Strategy Health & Alpha Decay — deteksi peluruhan deterministik yang membandingkan Sharpe strategi terbaru dengan catatan sebelumnya dan menemukan pergeseran rata-rata terbesar (CUSUM change-point), mengembalikan verdict Healthy / Degrading / Decayed."
---

# Strategy Health & Alpha Decay

Setiap edge meluruh — penelitian menunjukkan bahwa setengah umur dari strategi quant telah menurun dari bertahun-tahun menjadi berbulan-bulan, jadi *adaptasi mengalahkan penemuan*. Monitor Strategy Health memberi tahu Anda, dari riwayat return strategi sendiri, apakah edge masih ada.

Buka **cBots → Strategy Health** (`/quant/health`).

## What it does

Mengingat seri return (atau kurva ekuitas, tertua terlebih dahulu), ini:

- membagi riwayat menjadi setengah **sebelumnya** dan setengah **terbaru** serta membandingkan rasio Sharpe mereka;
- menjalankan pemindaian **CUSUM change-point** untuk menemukan observasi di mana rata-rata paling jelas bergeser (penutupan rezim), dilaporkan hanya ketika deviasi secara statistik penting;
- mengembalikan verdict:

| Verdict | Meaning |
|---|---|
| **Healthy** | Kinerja terbaru sejalan dengan (atau lebih baik dari) catatan sebelumnya. |
| **Degrading** | Sharpe terbaru jauh lebih lemah dari catatan sebelumnya — awasi dengan cermat. |
| **Decayed** | Edge telah hilang secara efektif dalam jendela terbaru — pertimbangkan untuk menghentikan sementara. |
| **Unknown** | Tidak cukup riwayat untuk menilai. |

- **Langsung dari backtest run — tanpa copy-paste.** Setiap backtest yang selesai mengekspos ikon **Check strategy health** di baris daftar **Backtest** dan di tampilan detail instancenya; satu klik menjalankan monitor pada kurva ekuitas simpanan run tersebut dan menampilkan verdict dalam dialog. Ikon dinonaktifkan hingga backtest selesai dan menghasilkan laporan, sehingga ini tidak pernah merupakan kontrol yang mati. Di balik layar ini adalah `POST /api/quant/health/backtest/{instanceId}`, yang membaca kurva ekuitas laporan simpanan.

```http
POST /api/quant/health
{ "returns": [...] }   // or { "equity": [...] }
```

## Why it is reliable

Ini adalah kode domain murni, deterministik (`Core.Health`) tanpa ketergantungan infrastruktur dan tanpa panggilan eksternal — unit-tested untuk kasus yang meluruh, merosot, sehat, dan terlalu singkat serta untuk lokalisasi change-point. Ini adalah pendamping manual untuk pemeriksaan kesehatan selalu-aktif yang mendukung agen otonom: statistik yang sama mendorong pemutus sirkuit yang mengurangi risiko strategi langsung yang edgenya memudar.
