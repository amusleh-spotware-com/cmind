---
description: "Backtest Integrity Lab — statistik overfitting deterministik, grade institusi (Probabilistic & Deflated Sharpe, t-stat) yang mengubah backtest mentah menjadi verdict Robust / Fragile / Overfit, dengan koreksi untuk berapa banyak konfigurasi yang Anda coba."
---

# Backtest Integrity Lab

Platform ritel menunjukkan Sharpe atau net profit backtest dan berhenti di situ. Institution tidak pernah
mempercayai backtest mentah — mereka bertanya apakah hasil tersebut bertahan **koreksi untuk selection bias
dan jumlah konfigurasi yang dicoba**. Backtest Integrity Lab membawa pemeriksaan itu ke cMind. Ini
**deterministic math** (tanpa AI, tanpa panggilan eksternal), sehingga verdict dapat direproduksi dan
setiap angka dapat dijelaskan.

Buka di **cBots → Integrity** (`/quant/integrity`).

## Apa yang dihitung

Dengan deret return (atau kurva equity/balance) dan jumlah set parameter yang Anda coba untuk sampai
padanya, analyzer melaporkan:

- **Sharpe ratio** — per-period dan diannualized (square-root-of-time).
- **Probabilistic Sharpe Ratio (PSR)** — kepercayaan bahwa *true* Sharpe mengungguli benchmark,
  memperhitungkan panjang track record, skewness dan kurtosis (Bailey & López de Prado, 2012). Record
  pendek atau fat-tailed menurunkannya.
- **Deflated Sharpe Ratio (DSR)** — PSR diukur terhadap **deflated benchmark**: Sharpe yang Anda
  harapkan dari *terbaik dari N percobaan acak* di bawah null (False Strategy Theorem). Semakin
  banyak konfigurasi yang Anda coba, semakin tinggi standarnya — ini yang menangkap overfitting.
- **t-statistic** dari mean return. Mengikuti Harvey, Liu & Zhu, edge genuine harus melewati **t ≥ 3.0**,
  bukan 2.0 textbook.
- **Skewness / kurtosis** dari return, yang mempengaruhi koreksi PSR/DSR.

## Verdict

| Verdict | Arti | Aturan |
|---|---|---|
| **Robust** | Edge bertahan dari percobaan yang Anda jalankan. | DSR ≥ 95% **dan** PSR ≥ 95% **dan** \|t\| ≥ 3.0 |
| **Fragile** | Secara statistik hidup tapi tidak meyakinkan — jangan tambah ukuran pada ini saja. | di antara keduanya |
| **Overfit** | Kemungkinan besar artefak selection bias, bukan edge nyata. | DSR < 90% |

Setiap hasil membawa rationale plain-English sehingga "kenapa" tidak pernah tersembunyi.

## Probability of Backtest Overfitting (across trials)

Memberikan *count* percobaan sudah baik; memberikan **seri out-of-sample aktual dari setiap konfigurasi
yang Anda coba** lebih baik. Tempelkan ke **trial grid** opsional (satu seri per baris) dan cMind
menjalankan **Combinatorially-Symmetric Cross-Validation** (Bailey, Borwein, López de Prado & Zhu,
2015): membagi observasi menjadi grup, dan untuk setiap cara memilih setengah sebagai in-sample ia
memilih konfigurasi terbaik in-sample dan memeriksa apakah pemenang itu masuk di **setengah bawah**
out-of-sample. **Probability of Backtest Overfitting (PBO)** adalah fraksi split di mana pemenang
gagal menggeneralisasi. PBO dekat 0 berarti konfigurasi terbaik genuinely terbaik; PBO 0.5 atau lebih
berarti proses seleksi Anda memilih noise — verdict menjadi **Overfit** terlepas dari sebaik apa
penampilan pemenang.

```http
POST /api/quant/pbo
{ "trials": [[...], [...], ...] }
```

Ketika native cTrader Console optimizer tiba, cMind akan memberikan seluruh surface trial di sini
secara otomatis.

## Trials — angka yang penting

`Trials` adalah **berapa banyak set parameter yang Anda uji** sebelum memilih ini. Menguji satu
strategi dan menguji sepuluh ribu dan menyimpan yang terbaik adalah hal yang sangat berbeda: yang kedua
membuat Sharpe in-sample tinggi secara kebetulan. Memberikan jumlah trial yang jujur adalah inti
— raise deflation dan bisa memindahkan backtest "bagus" ke **Overfit**. Ketika native cTrader Console
optimizer tiba, cMind memberikannya ukuran grid sweep yang sebenarnya secara otomatis.

## Input

- **Periodic returns** — satu angka per periode (mis. `0.01` = +1%). Minimal dua.
- **Kurva equity / balance** — cMind menurunkan consecutive simple returns untuk Anda.
- Atau jalankan langsung pada backtest yang selesai: `POST /api/quant/integrity/backtest/{instanceId}`
  membaca kurva equity stored report.

## API

```http
POST /api/quant/integrity
{ "returns": [0.006, 0.004, 0.006, ...], "trials": 250 }
```

Mengembalikan verdict, semua metrik, dan rationale. `POST /api/quant/integrity/backtest/{id}` menjalankan
analisis yang sama pada backtest selesai yang Anda miliki.

## Mengapa ini reliable

Statistik adalah pure functions di domain core (`Core.Quant`) dengan nol dependensi infrastruktur —
tidak dapat dijatuhkan oleh glitch jaringan, dan dipatri oleh golden-vector unit test terhadap formula
yang dipublikasikan. Normal CDF/inverse adalah aproximasi closed-form (Abramowitz-Stegun / Acklam),
sehingga input yang sama selalu menghasilkan verdict yang sama.
