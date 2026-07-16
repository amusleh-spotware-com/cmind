---
description: "Build dan backtest cBot (C# atau Python) di sandbox containerized — edit kode di Monaco editor, mulai/monitor/hentikan instance, lihat hasil."
---

# Build dan Backtest

Build dan backtest cBot (C# atau Python) di sandbox containerized — edit kode di Monaco editor,
mulai/monitor/hentikan instance, lihat hasil.

## Menyiapkan cBot

**cBots → New** membuka editor Monaco dengan template kosong. Anda dapat:

- **Upload** file `.cs` (C#) atau `.py` (Python) Anda sendiri.
- Mulai dari **template** yang disediakan.
- **Edit** langsung di browser — Monaco memberikan syntax highlighting dan IntelliSense dasar.

File disimpan di `CBotSourceProjects` table di database, dengan ID unik yang digunakan untuk
mereferensikan proyek di seluruh aplikasi.

## Meng-build

Klik **Build** untuk mengkompilasi cBot. Proses:

1. File sumber diambil dari database.
2. Dikirim ke `POST /api/cbots/build` bersama dengan bahasa (`csharp` atau `python`).
3. cBotBuilder membangun image Docker dengan MSBuild (C#) atau Python interpreter, dalam container
   sandbox dengan volume bersama ke `/work`.
4. Hasil build (sukses atau gagal dengan log error) dikembalikan.

Build menghasilkan **image tag** yang disimpan di `CBotSourceProject.BuildImageTag`. Image ini
digunakan untuk menjalankan instance.

## Menjalankan backtest

**cBots → Backtest** memungkinkan Anda mengkonfigurasi dan menjalankan backtest:

### Parameter

- **Instance name** — nama deskriptif untuk instance ini.
- **cBot** — pilih cBot yang sudah di-build.
- **Symbol** — simbol trading (mis. `EURUSD`).
- **Period** — periode waktu untuk backtest (mis. `H1`, `D1`).
- **Date range** — tanggal mulai dan selesai.
- **Initial balance** — balance awal untuk akun virtual.
- **Params** — parameter kustom untuk cBot (name/value pairs).

### Eksekusi

Klik **Run Backtest** untuk memulai:

1. Request dikirim ke `POST /api/backtest`.
2. `NodeScheduler` memilih node dengan kapasitas (node lokal atau remote agent).
3. Container Docker запускается dengan image cBot, trade terjadi over `FakeTradingSession`
   (simulasi pasar historis deterministik).
4. `BacktestInstance` dibuat di database dengan state `Running`.
5. Poller reconciling container yang exited, membaca exit code, menyimpan hasil.

### Monitoring

- **Logs** — `docker logs -f` di-stream ke UI via SignalR.
- **Progress** — persentase dan langkah saat ini (inisialisasi / running / finishing).
- **State** — `Pending` → `Running` → `Completed` / `Failed` / `Cancelled`.

## Hasil

Setelah selesai, hasil tersedia di **Instance table** (`/instances`):

- **Equity curve** — chart yang menunjukkan equity seiring waktu.
- **Statistics** — Sharpe, profit factor, win rate, max drawdown, dll.
- **Trades** — daftar trade individual dengan entry/exit points.
- **Logs** — log lengkap dari eksekusi.

Klik **Details** untuk melihat hasil lengkap dan download report.

## Copy ke akun lain

Setelah backtest berhasil, Anda dapat **menyalin** instance ke akun trading nyata:
klik **Copy to account** pada instance backtest, pilih akun target, dan instance akan dimulai
di akun tersebut menggunakan pengaturan yang sama.

## Edgenya

- Build/run container di-host web (membutuhkan Docker socket), dalam SDK container sekali pakai
  (bind-mount `/work`, shared `app-nuget-cache` volume) sehingga MSBuild yang tidak dipercaya
  tidak dapat mencapai host FS/net.
- `FakeTradingSession` menggunakan data OHLCV yang dihasilkan deterministically dari seed,
  bukan data pasar nyata — tidak ada risiko finansial, deterministik, replayable.
- `Instance` menggunakan TPH (Table Per Hierarchy) — state transition mengganti entity (ID berubah
  dari starting→running→terminal), container ID tetap stabil dan dibawa.

## Menjalankan dari editor kode

Mengklik **Jalankan** di editor kode membuka dialog alih-alih memicu eksekusi buta yang dikodekan secara kaku:

- **Akun trading** (wajib) — akun cTrader tempat cBot terhubung.
- **Set parameter** (opsional) — pilih set yang ada, atau biarkan kosong untuk menjalankan dengan **nilai parameter default** cBot. Tombol **+** di samping pemilih membuat set parameter baru secara inline (lihat di bawah) dan memilihnya.
- **Simbol / Kerangka waktu** secara default `EURUSD` / `h1` dan dapat diubah; **Batal** atau **Jalankan**.

Saat **Jalankan**, editor menyimpan dan membangun kode sumber saat ini, memulai instance pada akun yang dipilih dengan parameter yang dipilih, lalu mengikuti log kontainer secara langsung. (Aliran log meneruskan cookie autentikasi pengguna yang masuk ke hub SignalR `/hubs/logs`, sehingga terhubung alih-alih gagal dengan `Invalid negotiation response received`.)

## Set parameter

**Set parameter** adalah kumpulan penggantian parameter cBot yang diberi nama dan dapat digunakan kembali, disimpan sebagai objek JSON datar yang memetakan setiap nama parameter ke nilai skalar, mis. `{"Period": 14, "Label": "trend"}`. Saat menjalankan/backtest, ia diubah menjadi file cTrader `params.cbotset` (`{ "Parameters": { … } }`). Anda dapat membuat/mengedit set sebagai JSON mentah dari dialog **Set parameter** cBot atau secara inline dari dialog Jalankan.

JSON **divalidasi** saat menyimpan: harus berupa satu objek datar yang semua nilainya skalar (string / angka / bool). Akar non-objek, larik, objek bersarang, nilai `null`, atau JSON yang salah format akan ditolak (kesalahan yang jelas di dialog, `400 Bad Request` di API). Objek kosong `{}` diperbolehkan dan berarti "tanpa penggantian".

## Kontrol siklus hidup instance

Setiap baris instance (dan halaman detailnya) memiliki kontrol yang sesuai dengan status. Instance **aktif** menampilkan **Hentikan**; instance **terminal** (Dihentikan / Selesai / Gagal) menampilkan **Mulai (▶)** untuk meluncurkannya kembali dengan cBot, akun, simbol, kerangka waktu, set parameter, dan image yang sama (run dimulai ulang sebagai run, backtest sebagai backtest). Mengklik Hentikan menampilkan pemberitahuan "Menghentikan…" dan menonaktifkan ikon hingga selesai; run yang baru dibuat langsung muncul di daftar — tanpa memuat ulang halaman.

Log konsol **dipertahankan saat instance berakhir** — baik untuk run (saat dihentikan) maupun untuk **backtest** (saat selesai) — sehingga log run terakhir tetap dapat dilihat di halaman detail dan diunduh melalui ikon **Unduh log**, bahkan setelah kontainer hilang.
