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
