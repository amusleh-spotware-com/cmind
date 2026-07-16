---
description: "Backtest Integrity Lab — deterministic, fund-grade overfitting statistics (Probabilistic & Deflated Sharpe, t-stat) yang mengubah backtest mentah menjadi verdict Robust / Fragile / Overfit, dengan koreksi untuk berapa banyak konfigurasi yang Anda coba."
---

# Backtest Integrity Lab

Platform retail menunjukkan Sharpe backtest atau net profit dan berhenti di sana. Institusi tidak pernah
mempercayai backtest mentah — mereka menanyakan apakah hasilnya bertahan **koreksi untuk selection bias
dan jumlah konfigurasi yang Anda coba**. Backtest Integrity Lab membawa pemeriksaan itu ke cMind.
Ini adalah **matematika deterministik** (tanpa AI, tanpa pemanggilan eksternal), jadi verdict dapat
direproduksi dan setiap angka dapat dijelaskan.

Bukalah di **cBots → Integrity** (`/quant/integrity`).

## Apa yang dihitungnya

Diberikan seri return (atau kurva equity/balance) dan jumlah parameter set yang Anda coba untuk
sampai ke sana, analis melaporkan:

- **Sharpe ratio** — per-period dan annualized (square-root-of-time).
- **Probabilistic Sharpe Ratio (PSR)** — kepercayaan bahwa *true* Sharpe mengalahkan benchmark,
  dengan memperhitungkan panjang track-record, skewness dan kurtosis (Bailey & López de Prado, 2012). Record yang pendek atau fat-tailed akan menurunkannya.
- **Deflated Sharpe Ratio (DSR)** — PSR diukur terhadap **benchmark yang deflated**: Sharpe yang Anda
  harapkan dari *terbaik dari N random trials* di bawah null (False Strategy Theorem). Semakin banyak
  konfigurasi yang Anda coba, semakin tinggi standarnya — ini yang menangkap overfitting.
- **t-statistic** dari mean return. Mengikuti Harvey, Liu & Zhu, edge yang genuine harus mengatasi **t ≥ 3.0**,
  bukan 2.0 dalam buku teks.
- **Skewness / kurtosis** dari returns, yang memberi input koreksi PSR/DSR.

## Verdict

| Verdict | Arti | Aturan |
|---|---|---|
| **Robust** | Edge bertahan dari trials yang Anda jalankan. | DSR ≥ 95% **dan** PSR ≥ 95% **dan** \|t\| ≥ 3.0 |
| **Fragile** | Secara statistik hidup tetapi tidak meyakinkan — jangan scale up hanya berdasarkan ini. | antara dua |
| **Overfit** | Kemungkinan besar artefak dari selection bias, bukan edge yang nyata. | DSR < 90% |

Setiap hasil membawa rationale dalam bahasa Inggris yang jelas sehingga "mengapa" tidak pernah tersembunyi.

## Probability of Backtest Overfitting (across trials)

Memberikan trial *count* sudah bagus; memberikan **series out-of-sample aktual dari setiap konfigurasi
yang Anda coba** lebih baik lagi. Tempel ke dalam **trial grid** opsional (satu series per baris) dan
cMind menjalankan **Combinatorially-Symmetric Cross-Validation** (Bailey, Borwein, López de Prado & Zhu, 2015):
ia membagi observasi menjadi grup, dan untuk setiap cara memilih setengah sebagai in-sample ia memilih
konfigurasi terbaik in-sample dan memeriksa apakah pemenang itu mendarat di setengah bawah **out-of-sample**.
**Probability of Backtest Overfitting (PBO)** adalah fraksi pemisahan di mana pemenang gagal untuk
generalisasi. PBO dekat 0 berarti konfigurasi terbaik benar-benar terbaik; PBO 0.5 atau lebih berarti
proses seleksi Anda memilih noise — verdict menjadi **Overfit** terlepas dari seberapa bagus pemenang
terlihat.

```http
POST /api/quant/pbo
{ "trials": [[...], [...], ...] }
```

Ketika native cTrader Console optimizer tiba, cMind akan memberi makan permukaan trial lengkapnya di sini
secara otomatis.

## Trials — angka yang penting

`Trials` adalah **berapa banyak parameter set yang Anda uji** sebelum memilih ini. Menguji satu strategy
dan menguji sepuluh ribu serta menyimpan yang terbaik adalah hal yang sangat berbeda: yang kedua
menghasilkan Sharpe in-sample yang tinggi secara kebetulan. Memberikan trial count yang jujur adalah
seluruh intinya — itu menaikkan deflasi dan dapat menggerakkan backtest "luar biasa" menjadi **Overfit**.
Ketika native cTrader Console optimizer tiba, cMind memberinya ukuran grid sebenarnya sweep secara
otomatis.

## Input

- **Periodic returns** — satu angka per period (misalnya `0.01` = +1%). Setidaknya dua. Field memvalidasi
  saat Anda mengetik: ia menghitung angka yang valid, menandai token apa pun yang bukan angka, dan hanya
  mengaktifkan **Analyze** setelah setidaknya dua nilai bersih hadir (trial grid mengaktifkan **Assess
  overfitting** setelah dua series dari empat-plus angka masing-masing siap).
- **Equity / balance curve** — cMind menjabarkan consecutive simple returns untuk Anda.
- **Langsung dari backtest run — tanpa copy-paste.** Setiap backtest yang selesai menampilkan shield
  **Check backtest integrity** icon di baris list **Backtest** dan di tampilan detail instance-nya;
  satu klik menjalankan Lab pada kurva equity tersimpan run itu dan menunjukkan verdict dalam dialog.
  Icon dinonaktifkan hingga backtest selesai dan menghasilkan laporan, jadi ini tidak pernah dead control.
  Di bawah hood ini adalah `POST /api/quant/integrity/backtest/{instanceId}`, yang membaca kurva equity
  laporan tersimpan.

## API

```http
POST /api/quant/integrity
{ "returns": [0.006, 0.004, 0.006, ...], "trials": 250 }
```

Mengembalikan verdict, semua metrik, dan rationale. `POST /api/quant/integrity/backtest/{id}` menjalankan
analisis yang sama pada backtest yang selesai yang Anda miliki.

## Mengapa dapat diandalkan

Statistiknya adalah pure functions di domain core (`Core.Quant`) dengan zero infrastructure dependencies —
mereka tidak dapat dimatikan oleh network blip, dan mereka disematkan oleh golden-vector unit tests
terhadap formula yang dipublikasikan. CDF normal/inverse adalah closed-form approximations (Abramowitz-Stegun / Acklam),
jadi input yang sama selalu menghasilkan verdict yang sama.
