# Commitment of Traders (COT)

cMind menghantar laporan **Commitment of Traders** bawaan — pecahan mingguan CFTC tentang siapa yang membeli dan menjual
di pasaran niaga hadapan AS (penjaga lindung nilai komersial, spekulator besar, dana), dengan carta sejarah interaktif, 
**indeks COT** yang dinormalkan, API REST yang disahkan untuk cBot dan alat MCP untuk pelanggan AI. Data datang terus dari 
**set data CFTC awam Socrata** — tiada kunci API, tiada pengagregat. Seperti kalendar ekonomi, ia adalah modul yang tidak 
bersandar yang boleh dilumpuhkan tanpa kesan ke teras perdagangan.

## What it gives you

- **Ketiga-tiga keluarga laporan, hanya hadapan dan hadapan+opsyen digabung:**
  - **Legacy** — Bukan Komersial (spekulator besar), Komersial (penjaga), Tidak Boleh Dilaporkan.
  - **Disaggregated** — Pengeluar/Peniaga, Peniaga Pertukaran, Uang Terurus, Laporan Lain.
  - **Traders in Financial Futures (TFF)** — Peniaga, Pengurus Aset, Dana Berlever, Laporan Lain.
- **Katalog pasaran yang dikurasi** — pasangan FX utama, emas/perak/tembaga, minyak mentah & gas asli, Perbendaharaan, 
  indeks ekuiti, kripto dan bijirin/produk lembut utama — setiap satu dipetakan ke kod kontrak CFTC yang stabil dan, di mana 
  jelas, ke simbol yang boleh didagangkan (cth. Euro FX → `EURUSD`, Gold → `XAUUSD`).
- **Indeks COT (0–100)** — di mana kedudukan bersih spekulator semasa berada dalam julat sejarahnya 
  (default ~3 tahun lookback). Bacaan berhampiran ekstrim menandakan kedudukan yang sesak yang sering mendahului pembalikan; 
  laporan berlabel **ekstrim panjang** (≥80) atau **ekstrim pendek** (≤20).
- **Ketepatan pada titik masa.** Laporan mingguan diukur pada Selasa tetapi hanya menjadi awam Jumaat berikutnya; 
  setiap bacaan menghormati saat pelepasan itu, jadi isyarat kedudukan yang diuji balik tidak pernah melihat laporan 
  sebelum ia diterbitkan (tanpa lihat ke hadapan).

## Using the page

Buka **Commitment of Traders** dari navigasi kiri. Pilih **pasaran**, **jenis laporan** (Legacy /
Disaggregated / Financial) dan tukar **Hadapan + opsyen** untuk bertukar antara hadapan sahaja dan varian gabung. 
Halaman menunjukkan:

- **Kedudukan bersih merentasi masa** — carta garisan interaktif kedudukan bersih (beli − jual) setiap kategori peniaga
  merentasi tetingkap sejarah.
- **Indeks COT** — carta garisan indeks 0–100, dengan bacaan terkini dan label ekstrimnya.
- **Potret terkini** — jadual beli / jual / bersih / % minat terbuka setiap kategori peniaga, ditambah
  jumlah minat terbuka dan tarikh laporan.

Setiap carta membawa butang bar alat **zoom masuk / keluar** (dan tetapkan semula), dan anda boleh seret merentasi paksi masa untuk zum. **Export CSV** memuat turun sejarah mingguan penuh pasaran dan jenis laporan yang dipilih sebagai fail siap hamparan. Gunakan **Compare markets** untuk menindih beberapa pasaran pada carta tunggal — carta perbandingan merancang kedudukan bersih spekulator setiap pasaran yang dipilih dan indeks COT bersebelahan, supaya anda boleh membaca kedudukan lintas-pasaran sepintas lalu.

## How the data flows

Pangkalan data ialah cache. Pekerja pengambilan mingguan menarik enam set data CFTC untuk pasaran terjejak, menggabungkan katalog pasaran
dan menambah setiap laporan baru **secara idempoten** (menjalankan semula tidak pernah menduplikasi potret). Selain itu, data adalah **dimuat atas permintaan**: kali pertama pasaran diminta ia diambil dari sumber CFTC dan disimpan, dan setiap permintaan berikutnya dilayani terus dari pangkalan data. Cache **menyegarkan semula kerana laporan mingguan baru dirilis** — sebaik sahaja laporan yang disimpan terbaru lebih daripada seminggu lama permintaan berikutnya secara limpah menarik dan menambah data terkini (dikurangkan supaya sumber tidak pernah dipukul). Beban pertama memenuhi sejarah beberapa tahun; gangguan sumber merosot kepada melayani data cache terbaik. Semuanya berjalan di luar kotak tanpa kunci; token aplikasi Socrata pilihan hanya menaikkan had kadar.

## Configuration

Semua kunci berada di bawah `App:Cot` (lihat [toggle ciri](./feature-toggles.md) dan
[tetapan pemilik label putih](./white-label-owner-settings.md)):

| Key | Default | Purpose |
|-----|---------|---------|
| `IngestionEnabled` | `true` | Sama ada pekerja pengambilan mingguan berjalan. |
| `PollInterval` | `6h` | Berapa kerap pekerja mengundi set data CFTC. |
| `BackfillYears` | `5` | Tahun sejarah ditarik pada larian pertama. |
| `ReconcileLookbackWeeks` | `4` | Minggu terkini disegerakkan semula setiap kitaran untuk menangkap revisi. |
| `SocrataAppToken` | — | Token pilihan yang menaikkan had kadar tanpa nama. |
| `CotIndexLookbackWeeks` | `156` | Laporan mingguan digunakan sebagai julat indeks COT (~3 tahun). |

## Gating

Ketelihatan ialah pintu dua peringkat, sama dengan kalendar ekonomi: pintu keras label putih
`App:Branding:EnableCot` (peringkat bina) **dan** togol ciri masa jalan `App:Features:Cot`. Dengan salah satu mati, 
pautan navigasi, halaman, API REST dan alat MCP semuanya hilang (API kembali `404`). Kerana sumber data tanpa kunci, 
tidak ada pintu kunci sumber data — diaktifkan bermakna boleh dilihat.

## For developers

- Domain: `Core.Cot` — agregat `CotMarket` dan `CotReport`, objek nilai `CotPositions`, 
  perkhidmatan domain `CotIndexCalculator`, dan port `ICotReports` / `ICotSource`.
- Infrastructure: `Infrastructure.Cot` — penghurai anti-rasuah `CftcSocrataSource`, pintu kadar,
  perkhidmatan tulis tambah sahaja, sisi baca dan pekerja pengambilan mingguan (skema EF `cot`).
- cBot & Akses AI: [API cBot COT](./cot-cbot-api.md) (REST, JWT `market:read`) dan alat MCP
  `CotMarkets`, `CotLatest`, `CotHistory`, `CotHealth`.
