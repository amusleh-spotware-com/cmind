---
description: "Yuran prestasi pengelola wang pada paras tinggi-air, model salinan perdagangan standard (cTrader Copy, Darwinex, ZuluTrade profit-share): penyedia mengenakanpercentage *baharu* keuntungan di atas puncak ekuiti setiap pengikut — tidak pernah pada baki pembukaan, dan tidak pernah dua kali untuk asas yang sudah dipulihkan."
---

# Yuran prestasi salinan (Fasa 4)

Yuran prestasi pengelola wang pada **paras tinggi-air**, model salinan perdagangan standard (cTrader Copy,
Darwinex, ZuluTrade profit-share): penyedia mengenakan peratusan *baharu* keuntungan di atas puncak ekuiti setiap pengikut — tidak pernah pada baki pembukaan, dan tidak pernah dua kali untuk asas yang sudah dipulihkan. **Pilihan masuk** melalui
`App:Copy:FeesEnabled` (lalai off).

## Model (paras tinggi-air)

Setiap destinasi (akaun pengikut), setiap penyelesaian:

1. **Penyelesaian pertama** menanam tinggi-air (HWM) pada ekuiti semasa → tiada caj (pengikut
   tidak pernah dicaj pada deposit mereka).
2. **Tinggi baharu** (ekuiti > HWM): `fee = performanceFeePercent × (ekuiti − HWM)`, kemudian `HWM ← ekuiti`.
3. **Pada atau di bawah puncak**: tiada caj, HWM tidak berubah — pengikut harus pulih dahulu melepasi puncak lama, jadi
   mereka tidak pernah dicaj dua kali untuk keuntungan yang sama.

Aritmetik yuran ialah invariant domain pada `CopyDestination.SettleFee(equity)` — agregat memmilikinya; perkhidmatan
penyelesaian hanya membekalkan ekuiti yang disiarkan dan merekodkan jumlah yang dikembalikan. `PerformanceFee` ialah
objek nilai dibatasi pada 50% jadi salah konfigurasi tidak boleh caj seluruh keuntungan pengikut.

## Cara ia menyelesaikan

```
CopyFeeSettlementService (BackgroundService, hanya bila FeesEnabled)
   │  setiap App:Copy:FeeSettlementInterval
   ├─ muat profil berjalan dengan destinasi berwibawa
   ├─ ICopyEquityReader.ReadEquityAsync(ctid)   ← OpenApiCopyEquityReader membuka sesi,
   │                                               mengira baki + P&L terapung (PropFirmEquityCalculator)
   ├─ destination.SettleFee(ekuiti)             ← Logik HWM pada agregat
   └─ pengekalkan HWM lanjutan + append CopyFeeAccrual (hanya pada tinggi baharu)
```

- `ICopyEquityReader` ialah abstraksi Core; pelaksanaan live (`OpenApiCopyEquityReader`) ialah satu-satunya
  bahagian infra — jadi logik penyelesaian + HWM diuji dengan pembaca palsu, tiada broker langsung.
- `CopyFeeAccrual` ialah log hanya-tambah (HWM-sebelum, ekuiti, fee %, jumlah yuran, settled-at) — log fakta untuk
  laporan yuran dan bil, bukan agregat.

## Konfigurasi & API

| Tetapan `App:Copy` | Lalai | Kesan |
|--------------------|---------|--------|
| `FeesEnabled` | `false` | Jalankan perkhidmatan penyelesaian. |
| `FeeSettlementInterval` | `1h` | Kekerapan ekuiti disiarkan dan yuran diselesaikan. |

Setiap destinasi: `PerformanceFeePercent` (0–50) ditetapkan pada destinasi (permintaan tambah/edit destinasi).

- `GET /api/copy/profiles/{id}/fees` — akruan yuran profil + jumlah dicaj.

## Ujian

- **Unit** (`CopyPerformanceFeeTests`) — invariant HWM: penyelesaian pertama menanam + tidak memcaj apa-apa; tinggi baharu mencaj hanya keuntungan di atas puncak; pada/di bawah puncak tidak mencaj dan puncak tidak berundur; selepas undur hanya pulihn di atas puncak lama yang dicaj; 0% tidak pernah mencaj; VO menolak peratusan di luar julat.
- **Integrasi** (`CopyFeeSettlementTests`, Postgres sebenar, pembaca ekuiti palsu) — seed→10k (tiada caj, mark种子), 12k (caj 400, mark avançado), 11k (tiada caj, mark dipegang); akruan dikekalkan dengan pemilik/jumlah yang betul.

Hos salinan tidak disentuh oleh yuran (penyelesaian ialah kerja DB berasingan), jadi suite tekanan DST salinan tidak terjejas (23/23).
