---
description: "Makmal Integriti Backtest — statistik overfitting gred-Institusi yang deterministik (Probabilistic & Deflated Sharpe, t-stat) yang mengubah backtest mentah kepada verdict Robust / Rapuh / Overfit, membetulkan untuk berapa banyak konfigurasi yang anda cuba."
---

# Makmal Integriti Backtest

Platform runcit menunjukkan kepada anda Sharpe atau keuntungan bersih backtest dan berhenti di situ. Institusi tidak pernah mempercayai backtest mentah — mereka bertanya sama ada keputusan itu bertahan **pembetulan untuk pincang pemilihan dan bilangan konfigurasi yang dicuba**. Makmal Integriti Backtest membawa pemeriksaan itu ke cMind. Ia ialah **matematik deterministik** (tiada AI, tiada panggilan luaran), jadi verdict boleh dihasilkan semula dan setiap nombor boleh diterangkan.

Bukanya di **cBots → Integrity** (`/quant/integrity`).

## Apa yang dikira

Diberikan siri pulangan (atau lengkung ekuiti/imbangan) dan bilangan set parameter yang anda cuba untuk sampai
kepadanya, penganalisis melaporkan:

- **Nisbah Sharpe** — setiap tempoh dan tahunan (punktal-punca masa).
- **Probabilistic Sharpe Ratio (PSR)** — keyakinan bahawa Sharpe *sebenar* mengatasi penanda aras,
  memandangkan panjang rekod trek, kepencongan dan kutosis (Bailey & López de Prado, 2012). Rekod pendek atau
  berekor gemuk menurunkannya.
- **Deflated Sharpe Ratio (DSR)** — PSR diukur terhadap **penanda aras yang diturunkan**: Sharpe yang anda jangkakan dari *terbaik daripada N percubaan rawak* di bawah null (False Strategy Theorem). Lebih banyak
  konfigurasi yang anda cuba, semakin tinggi palang — ini yang menangkap overfitting.
- **t-statistic** bagi pulangan purata. Mengikuti Harvey, Liu & Zhu, kelebihan sebenar harus jelas **t ≥ 3.0**,
  bukan 2.0 buku teks.
- **Kepencongan / kutosis** pulangan, yang dimasukkan ke pembetulan PSR/DSR.

## Verdict

| Verdict | Makna | Peraturan |
|---|---|---|
| **Robust** | Kelebihan itu bertahan percubaan yang anda jalankan. | DSR ≥ 95% **dan** PSR ≥ 95% **dan** |t| ≥ 3.0 |
| **Rapuh** | Masih hidup secara statistik tetapi tidak meyakinkan — jangan besarkan berdasarkan ini sahaja. | antara kedua-duanya |
| **Overfit** | Kemungkinan besar artifak pincang pemilihan, bukan kelebihan sebenar. | DSR < 90% |

Setiap keputusan membawa justifikasi teks biasa supaya "kenapa" tidak pernah disembunyikan.

## Kebarangkalian Backtest Overfitting (merentasi percubaan)

Memberi bilangan percubaan adalah baik; memberi **seri di luar sampel sebenar setiap konfigurasi yang anda
cuba** adalah lebih baik. Tampal ke dalam **grid percubaan** pilihan (satu seri setiap baris) dan cMind menjalankan
**Combinatorially-Symmetric Cross-Validation** (Bailey, Borwein, López de Prado & Zhu, 2015): ia memecahkan
pemerhatian kepada kumpulan, dan untuk setiap cara memilih separuh sebagai dalam-contoh ia memilih konfigurasi
terbaik dalam-contoh dan semak sama ada pemenang itu mendarat di bahagian bawah **luar-contoh**. **Kebarangkalian Backtest Overfitting (PBO)** ialah pecahan belahan di mana pemenang gagal generalise. PBO hampir 0 bermakna konfigurasi terbaik memang terbaik; PBO 0.5 atau lebih bermakna proses pemilihan anda memilih bunyi — verdict menjadi **Overfit** walau betapa bagusnya pemenang kelihatan.

```http
POST /api/quant/pbo
{ "trials": [[...], [...], ...] }
```

Apabila pengoptimum cTrader Console asli mendarat, cMind akan memberi makan permukaan percubaan penuh di sini
secara automatik.

## Percubaan — nombor yang penting

`Trials` ialah **berapa banyak set parameter yang anda uji** sebelum memilih ini. Menguji satu strategi dan
menguji sepuluh ribu dan menyimpan yang terbaik adalah perkara yang sangat berbeza: yang kedua menghasilkan
Sharpe dalam-contoh tinggi secara kebetulan. Memberi bilangan percubaan yang jujur adalah intinya — ia meninggikan
penurunan dan boleh menggerakkan backtest "bagus" ke **Overfit**. Apabila pengoptimum cTrader Console asli mendarat,
cMind memberinya saiz grid sebenar pengesyoran secara automatik.

## Input

- **Pulangan berkala** — satu nombor setiap tempoh (cth `0.01` = +1%). Sekurang-kurangnya dua.
- **Lengkung ekuiti / imbangan** — cMind menerbitkan pulangan mudah berturut-turut untuk anda.
- Atau jalankannya terus pada backtest yang lengkap: `POST /api/quant/integrity/backtest/{instanceId}` membaca lengkung ekuiti laporan tersimpan.

## API

```http
POST /api/quant/integrity
{ "returns": [0.006, 0.004, 0.006, ...], "trials": 250 }
```

Mengembalikan verdict, semua metrik, dan justifikasi. `POST /api/quant/integrity/backtest/{id}` menjalankan analisis yang sama pada backtest lengkap yang anda miliki.

## Mengapa ia boleh dipercayai

Statistik ialah fungsi murni dalam domain teras (`Core.Quant`) dengan sifar kebergantungan infrastruktur — ia tidak boleh ditutup oleh gangguan rangkaian, dan ia dipancangkan oleh ujian unit vektor emas terhadap formula yang diterbitkan. Fungsi taburan normal / songsang (Abramowitz-Stegun / Acklam) adalah penghampiran bentuk tertutup,
jadi input yang sama sentiasa menghasilkan verdict yang sama.
