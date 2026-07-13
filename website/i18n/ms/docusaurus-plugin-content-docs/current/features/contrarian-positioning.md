---
description: "Kedudukan Runcit Kontrarian — mengubah % pedagang runcit yang panjang kepada pincang kontrarian (fade kerumunan apabila ia satu pihak),ditambah objek nilai isyarat titik-dalam-masa yang menjaga terhadap pincang pandang-masa-depan."
---

# Kedudukan Runcit Kontrarian

Kumpulan pedagang runcit ialah salah satu daripada beberapa isyarat sentimen yang benar-benar berguna dalam FX — sebagai
**indikator kontrarian**. Apabila majoriti pedagang runcit panjang, harga secara sejarah cenderung untuk turun,
dan begitu juga sebaliknya. Alat ini mengubah kedudukan kerumitan kepada bacaan yang boleh dikendalikan.

Buka **cBots → Contrarian Positioning** (`/quant/positioning`).

## Apa yang dilakukannya

Masukkan **% pedagang runcit yang panjang** (dari halaman sentimen broker anda atau suapan seperti FXSSI) dan
ia mengembalikan:

- **Pincang kontrarian** — **Bearish** apabila ≥ 60% panjang (kerumunan terlalu panjang), **Bullish** apabila ≤ 40% panjang (kerumunan terlalu pendek), **Neutral** dalam jalur 40–60%;
- **Kekuatan** — betapa satu pihak kerumunan itu (0 = imbang, 1 = sepenuhnya satu pihak), untuk menimbang isyarat.

```http
POST /api/quant/positioning
{ "longPercent": 72 }
```

## Titik-dalam-masa oleh konstruksi

Di bawah hud lapisan isyarat (`Core.Signals`) memodelkan `PointInTimeSignal` yang **di磅 dengan masa
ia boleh diketahui** dan enggan dibina tanpa nó. Mana-mana backtest atau ejen autonomic yang
menggunakan isyarat menyemak `IsKnownAt(decisionTime)` — jadi data masa depan tidak boleh bocor ke keputusan sejarah. Pincang pandang-masa-depan adalah pembunuh reproduksibiliti teratas dalam kewangan kuant; model domain menjadikannya secara struktural mustahil.

## Mengapa ia boleh dipercayai

Kod domain deterministik murni dengan tiada kebergantungan infrastruktur — ambang kontrarian dan penjaga titik-dalam-masa diuji unit, termasuk sempadan 40/60 dan penolakan di luar julat.
