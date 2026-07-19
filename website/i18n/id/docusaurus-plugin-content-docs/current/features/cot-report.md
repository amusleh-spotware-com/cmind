# Komitmen Pedagang (COT)

cMind dilengkapi dengan laporan **Komitmen Pedagang** bawaan — perincian mingguan CFTC tentang siapa yang long dan short di pasar futures AS (hedger komersial, spekulan besar, dana), dengan grafik historis interaktif, **indeks COT** yang dinormalisasi, API REST terautentikasi untuk cBots dan alat MCP untuk klien AI. Data berasal langsung dari **kumpulan data publik Socrata CFTC** — tanpa kunci API, tanpa agregator. Seperti kalender ekonomi, ini adalah modul yang dapat dipisahkan yang dapat dinonaktifkan tanpa efek pada inti perdagangan.

## Apa yang diberikannya

- **Ketiga keluarga laporan, hanya futures dan futures + opsi digabung:**
  - **Warisan** — Non-Komersial (spekulan besar), Komersial (hedger), Tidak Dilaporkan.
  - **Diagregasi** — Produsen/Pedagang, Dealer Swap, Uang Terkelola, Lainnya yang Dilaporkan.
  - **Pedagang dalam Futures Keuangan (TFF)** — Dealer, Manajer Aset, Dana Leverage, Lainnya yang Dilaporkan.
- **Katalog pasar yang dikurasi** — pasangan FX utama, emas/perak/tembaga, minyak mentah dan gas alam, Obligasi, indeks saham, kripto dan biji-bijian/komoditas lunak utama — masing-masing dipetakan ke kode kontrak CFTC stabil dan, jika jelas, ke simbol yang dapat diperdagangkan (mis. Euro FX → `EURUSD`, Emas → `XAUUSD`).
- **Indeks COT (0–100)** — di mana posisi neto spekulan saat ini berada dalam rentang historisnya (default ~3 tahun lookback). Pembacaan di dekat ekstrem menandai posisi yang ramai yang sering mendahului pembalikan; laporan menandai **ekstrem panjang** (≥80) atau **ekstrem pendek** (≤20).
- **Kebenaran point-in-time.** Laporan mingguan diukur pada hari Selasa tetapi hanya menjadi publik pada hari Jumat berikutnya; setiap pembacaan menghormati momen rilis itu, sehingga sinyal posisi yang diuji tidak pernah melihat laporan sebelum dipublikasikan (tidak ada antisipasi).

## Menggunakan halaman

Buka **Komitmen Pedagang** dari navigasi kiri. Pilih **pasar**, **jenis laporan** (Warisan / Diagregasi / Keuangan) dan toggle **Futures + opsi** untuk beralih antara hanya futures dan varian gabungan. Halaman menampilkan:

- **Posisi neto seiring waktu** — grafik garis interaktif dari posisi neto (panjang − pendek) setiap kategori pedagang di seluruh jendela sejarah.
- **Indeks COT** — grafik garis dari indeks 0–100, dengan pembacaan terbaru dan labelnya yang ekstrem.
- **Snapshot terbaru** — tabel panjang / pendek / neto / % minat terbuka per kategori pedagang, plus total minat terbuka dan tanggal laporan.

Setiap bagan memiliki tombol toolbar **perbesar / perkecil** (dan atur ulang), dan Anda dapat menyeret melintasi sumbu waktu untuk memperbesar. **Ekspor CSV** mengunduh riwayat mingguan lengkap pasar yang dipilih dan jenis laporan sebagai file siap spreadsheet. Gunakan **Bandingkan pasar** untuk menumpangkan beberapa pasar pada satu bagan — bagan perbandingan memplot posisi neto spekulan dan indeks COT setiap pasar yang dipilih berdampingan, sehingga Anda dapat membaca posisi lintas pasar sekilas.

## Bagaimana data mengalir

Basis data adalah cache. Pekerja ingesti mingguan menarik enam kumpulan data CFTC untuk pasar yang dilacak, memperbarui katalog pasar dan menambahkan setiap laporan baru **secara idempoten** (menjalankan kembali tidak pernah menduplikasi snapshot). Selain itu, data **dimuat sesuai permintaan**: pertama kali pasar diminta, data diambil dari sumber CFTC dan disimpan, dan setiap permintaan berikutnya dilayani langsung dari basis data. Cache **menyegarkan saat laporan mingguan baru dirilis** — begitu laporan tersimpan terbaru berusia lebih dari seminggu, permintaan berikutnya secara transparan menarik dan menambahkan data terbaru (dibatasi sehingga sumber tidak pernah dihajar). Beban pertama mengisi kembali beberapa tahun sejarah; pemadaman sumber menurun untuk melayani data cache terbaik. Semuanya berjalan langsung tanpa kunci; token aplikasi Socrata opsional hanya meningkatkan batas laju.

## Konfigurasi

Semua kunci berada di bawah `App:Cot` (lihat [toggle fitur](./feature-toggles.md) dan [pengaturan pemilik label putih](./white-label-owner-settings.md)):

| Kunci | Default | Tujuan |
|-----|---------|---------|
| `IngestionEnabled` | `true` | Apakah pekerja ingesti mingguan berjalan. |
| `PollInterval` | `6h` | Seberapa sering pekerja menyelidiki kumpulan data CFTC. |
| `BackfillYears` | `5` | Tahun sejarah ditarik pada run pertama. |
| `ReconcileLookbackWeeks` | `4` | Minggu terakhir disinkronkan ulang setiap siklus untuk menangkap revisi. |
| `SocrataAppToken` | — | Token opsional yang meningkatkan batas laju anonim. |
| `CotIndexLookbackWeeks` | `156` | Laporan mingguan digunakan sebagai rentang indeks COT (~3 tahun). |

## Gating

Visibilitas adalah gerbang dua tingkat, identik dengan kalender ekonomi: gerbang keras label putih `App:Branding:EnableCot` (tingkat build) **dan** toggle fitur runtime `App:Features:Cot`. Dengan salah satu dimatikan, tautan navigasi, halaman, API REST, dan alat MCP semuanya hilang (API mengembalikan `404`). Karena sumber data tidak memiliki kunci, tidak ada gerbang kunci sumber data — diaktifkan berarti terlihat.

## Untuk pengembang

- Domain: `Core.Cot` — `CotMarket` dan `CotReport` agregat, objek nilai `CotPositions`, layanan domain `CotIndexCalculator`, dan port `ICotReports` / `ICotSource`.
- Infrastruktur: `Infrastructure.Cot` — pengurai anti-korupsi `CftcSocrataSource`, gerbang laju, layanan tulis hanya-tambah, sisi baca, dan pekerja ingesti mingguan (skema EF `cot`).
- Akses cBot & AI: [API cBot COT](./cot-cbot-api.md) (REST, JWT `market:read`) dan alat MCP `CotMarkets`, `CotLatest`, `CotHistory`, `CotHealth`.
