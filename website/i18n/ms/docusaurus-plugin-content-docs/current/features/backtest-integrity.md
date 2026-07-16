---
description: "Makmal Integriti Backtest — statistik overfitting berkualitas dana yang deterministik (Probabilistic & Deflated Sharpe, t-stat) yang mengubah backtest mentah menjadi verdit Robust / Rapuh / Overfit, membetulkan untuk berapa banyak konfigurasi yang anda cuba."
---

# Makmal Integriti Backtest

Platform ritel menunjukkan anda Sharpe backtest atau keuntungan bersih dan berhenti di sana. Institusi tidak pernah mempercayai backtest mentah — mereka bertanya sama ada hasilnya bertahan **perbetulkan untuk berat sebelah pemilihan dan bilangan konfigurasi yang dicuba**. Makmal Integriti Backtest membawa pemeriksaan itu ke cMind. Ia adalah **matematik deterministik** (tiada AI, tiada panggilan luaran), jadi verdit boleh dihasilkan semula dan setiap nombor boleh dijelaskan.

Bukanya di **cBots → Integriti** (`/quant/integrity`).

## Apa yang ia hitung

Memandangkan siri pulangan (atau lengkung ekuiti/baki) dan bilangan set parameter yang anda cuba untuk sampai padanya, penganalisis melaporkan:

- **Nisbah Sharpe** — bagi setiap tempoh dan tahunan (punca-kuasa-dua masa).
- **Nisbah Sharpe Probabilistik (PSR)** — kepercayaan bahawa *benar* Sharpe mengalahkan penanda aras, mengambil kira panjang jejak, kecondongan dan kurtosis (Bailey & López de Prado, 2012). Rekod pendek atau berekor gemuk membuatnya lebih rendah.
- **Nisbah Sharpe Deflated (DSR)** — PSR diukur terhadap **penanda aras yang deflated**: Sharpe yang anda harapkan daripada *terbaik N ujian rawak* di bawah nol (Teorem Strategi Palsu). Lebih banyak konfigurasi yang anda cuba, semakin tinggi palang — ini adalah apa yang menangkap overfitting.
- **t-statistik** bagi pulangan purata. Mengikuti Harvey, Liu & Zhu, tepi tulen harus membersihkan **t ≥ 3.0**, bukan buku teks 2.0.
- **Kecondongan / kurtosis** pulangan, yang memberi makan pembetulan PSR/DSR.

## Verdit

| Verdit | Makna | Peraturan |
|---|---|---|
| **Teguh** | Tepi bertahan ujian yang anda jalankan. | DSR ≥ 95% **dan** PSR ≥ 95% **dan** \|t\| ≥ 3.0 |
| **Rapuh** | Secara statistik hidup tetapi tidak meyakinkan — jangan saiz naik pada sini sahaja. | antara kedua |
| **Overfit** | Kemungkinan besar artefak berat sebelah pemilihan, bukan tepi sebenar. | DSR < 90% |

Setiap hasil membawa rasional bahasa Inggeris biasa supaya "mengapa" tidak pernah disembunyikan.

## Kebarangkalian Backtest Overfitting (merentasi ujian)

Memberi suatu kiraan ujian *bilangan* adalah bagus; memberi **siri out-of-sample sebenar setiap konfigurasi yang anda cuba** adalah lebih baik. Tampalkan mereka ke **grid ujian** pilihan (satu siri bagi setiap baris) dan cMind menjalankan **Pengesahan Silang Simetri Kombinatorik** (Bailey, Borwein, López de Prado & Zhu, 2015): ia membahagikan pemerhatian kepada kumpulan, dan untuk setiap cara memilih separuh sebagai dalam sampel ia memilih konfigurasi terbaik dalam sampel dan menyemak sama ada pemenang itu mendarat di separuh bawah **out-of-sample**. **Kebarangkalian Backtest Overfitting (PBO)** adalah pecahan pemisahan di mana pemenang gagal untuk umum. PBO berhampiran 0 bermakna konfigurasi terbaik adalah benar-benar terbaik; PBO 0.5 atau lebih bermakna proses pemilihan anda memilih hingar — verdit menjadi **Overfit** tanpa mengira betapa baik pemenangnya kelihatan.

```http
POST /api/quant/pbo
{ "trials": [[...], [...], ...] }
```

Apabila pengoptimal Console cTrader asli tiba, cMind akan memberi permukaan ujian penuhnya ke sini secara automatik.

## Ujian — bilangan yang penting

`Trials` adalah **berapa banyak set parameter yang anda uji** sebelum memilih yang ini. Menguji satu strategi dan menguji sepuluh ribu dan menyimpan yang terbaik adalah perkara yang sangat berbeza: yang kedua mengeluarkan Sharpe dalam sampel tinggi secara kebetulan. Memberi kiraan ujian yang jujur adalah keseluruhan poin — ia meningkatkan pesonggangan dan boleh menggerakkan backtest "hebat" ke **Overfit**. Apabila pengoptimal Console cTrader asli tiba, cMind memberinya saiz grid sebenar sapuan secara automatik.

## Input

- **Pulangan berkala** — satu nombor bagi setiap tempoh (cth. `0.01` = +1%). Sekurang-kurangnya dua. Medan mengesahkan semasa anda menaip: ia mengira nombor yang sah, bendera mana-mana token yang bukan nombor, dan hanya membolehkan **Analisis** sekali sekurang-kurangnya dua nilai bersih hadir (grid ujian membolehkan **Nilai overfitting** sekali dua siri empat-tambah nombor setiap satu sedia).
- **Lengkung ekuiti / baki** — cMind memperoleh pulangan mudah berturut-turut untuk anda.
- **Lurus dari larian backtest — tiada salinan-tampal.** Setiap backtest selesai mendedahkan perisai **Periksa integriti backtest** ikon pada baris **Backtest** dan pada paparan perincian contoh; satu klik menjalankan Makmal pada lengkung ekuiti tersimpan larian itu dan menunjukkan verdit dalam dialog. Ikon dilumpuhkan sehingga backtest selesai dan menghasilkan laporan, jadi ia tidak pernah kawalan mati. Di bawah tudung ini adalah `POST /api/quant/integrity/backtest/{instanceId}`, yang membaca lengkung ekuiti laporan tersimpan.

## API

```http
POST /api/quant/integrity
{ "returns": [0.006, 0.004, 0.006, ...], "trials": 250 }
```

Mengembalikan verdit, semua metrik, dan rasional. `POST /api/quant/integrity/backtest/{id}` menjalankan analisis yang sama pada backtest selesai yang anda miliki.

## Mengapa ia boleh dipercayai

Statistik adalah fungsi tulen dalam inti domain (`Core.Quant`) dengan sifar kebergantungan infrastruktur — mereka tidak boleh diambil turun oleh kegagalan rangkaian, dan mereka disematkan oleh ujian unit vektor emas terhadap formula yang diterbitkan. CDF normal/songsang adalah anggaran bentuk tertutup (Abramowitz-Stegun / Acklam), jadi input yang sama sentiasa menghasilkan verdit yang sama.
