---
description: "Bangun, jalankan, backtest cBot cTrader (C# dan Python, keduanya .NET) dari IDE Monaco di peramban, jalankan di citra resmi ghcr.io/spotware/ctrader-console."
---

# Bangun & backtest cBot

Bangun, jalankan, backtest cBot cTrader (C# **dan** Python, keduanya .NET) dari IDE Monaco
di peramban, jalankan di citra resmi `ghcr.io/spotware/ctrader-console`.

## Bangun

- Halaman **Builder** menampung editor Monaco; `CBotBuilder` mengompilasi proyek dengan
  `dotnet build` **dalam kontainer sekali pakai** (`AppOptions.BuildImage`, direktori kerja bind-mount
  di `/work`), jadi target MSBuild pengguna tidak terpercaya tidak dapat menjangkau host. Cache pemulihan NuGet
  di seluruh build melalui volume bersama. Host web memerlukan akses soket Docker.
- Template pemula C# + Python tersimpan di `src/Nodes/Builder/Templates/`.

## Jalankan & backtest

- **Instance** = hierarki status TPH (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/
  `Running`/`Stopping`/`Stopped`/`Failed`). Transisi mengganti entity (perubahan id),
  id kontainer dibawa sesuai.
- `NodeScheduler` memilih node yang memenuhi syarat dan paling sedikit beban; `ContainerDispatcherFactory` merutekan ke
  agen HTTP node jarak jauh atau dispatcher Docker lokal.
- Poller penyelesaian merekonsiliasi kontainer yang keluar (kontainer backtest keluar sendiri melalui
  `--exit-on-stop`); laporan ada → selesai (simpan `ReportJson`), tidak ada → gagal.
- Log kontainer langsung mengalir ke peramban melalui SignalR; kurva ekuitas backtest diuraikan dari
  laporan + dibuat bagan.

## Data pasar backtest disimpan dalam cache per akun

cTrader Console mengunduh data tick/bar historis ke `--data-dir` miliknya. Direktori itu adalah
**cache stabil, persisten yang dikunci pada akun perdagangan** (nomor akunnya) — bind-mounted dari
disk node di jalur kontainer miliknya sendiri (`/mnt/data`), **mount terpisah, tidak bersarang** dari
direktori kerja per-instance. Jadi setiap backtest pada akun yang sama **menggunakan kembali** data yang sudah
diunduh daripada mengunduhnya setiap kali. (Sebelumnya direktori data berada di bawah direktori kerja per-instance,
yang id-nya berubah setiap kali berjalan, yang memaksa unduhan segar setiap backtest.) Direktori kerja per-instance
yang tidak permanen tetap menyimpan algo, param, kata sandi dan laporan; cache data bersama dihitung dalam penggunaan
data backtest node dan dihapus oleh tindakan pembersihan node.

## Pengaturan backtest

Dialog **Backtest** menampilkan pengaturan backtest cTrader Console yang dapat disesuaikan pengguna, sehingga Anda tidak perlu
menyentuh baris perintah:

- **Simbol / Timeframe** — timeframe adalah **dropdown dari setiap periode cTrader** (`t1`…`t1000`,
  `m1`…`m45`, `h1`…`h12`, `D1`/`D2`/`D3`, `W1`, `Month1`, dan periode Renko/Range/Heikin), dalam
  huruf besar kanonik konsol, jadi Anda selalu memilih `--period` yang valid.
- **Dari / Ke** — jendela backtest (`--start` / `--end`).
- **Mode data** — salah satu dari tiga mode cTrader (`--data-mode`): **Data tick** (`tick`, akurat),
  **bilah m1** (`m1`, cepat), atau **Harga buka saja** (`open`, tercepat).
- **Saldo awal** — default ke `10000` (`--balance`). **Saldo 0 tidak melakukan perdagangan dan membuat
  cTrader mengeluarkan laporan kosong yang kemudian mogok** ("Pesan diharapkan"), jadi saldo bukan nol
  selalu dikirim.
- **Komisi** dan **Spread** — `--commission` / `--spread` (spread dalam pips).

Direktori data (`--data-file` / `--data-dir`) dikelola oleh aplikasi itu sendiri (cache per-akun, lihat
di atas), tidak dipaparkan dalam dialog.

## Halaman detail instance

Membuka instance (`/instance/{id}`) menampilkan statusnya yang aktif, log dan — untuk backtest — kurva ekuitas
miliknya. **Judul tab peramban** mencerminkan instance spesifik (**nama cBot · jenis · simbol**, mis.
`TrendBot · Backtest · EURUSD`) jadi tab jalankan-langsung dan tab backtest dapat dibedakan sekilas.
Sebuah jalankan dan backtest cBot yang sama dilacak sebagai **garis keturunan** yang berbeda (id garis keturunan stabil yang dibawa
di seluruh transisi status), jadi halaman mengikuti tepat satu instance dan tidak pernah mencampur data jalankan dengan
data backtest.

## Kontrol siklus hidup instance

Setiap baris instance (dan halaman detailnya) memiliki kontrol yang sesuai dengan status. Instance **aktif** menampilkan
**Hentikan**; instance **terminal** (Dihentikan / Selesai / Gagal) menampilkan **Jalankan (▶)** untuk meluncurkannya kembali dengan
cBot yang sama, akun, simbol, timeframe, parameter set dan citra (jalankan dimulai kembali sebagai jalankan, backtest sebagai backtest). Mengklik Hentikan menampilkan pemberitahuan "Menghentikan…" dan menonaktifkan ikon hingga diselesaikan, dan jalankan yang baru dibuat muncul dalam daftar segera — tidak ada muat ulang halaman.

Log konsol **persisten ketika instance berakhir** — untuk jalankan (saat Hentikan) dan untuk
**backtest** (saat selesai) sekaligus — jadi log jalankan terakhir tetap dapat dilihat di halaman detail dan,
melalui bilah alat log, **disalin ke clipboard** (ikon Salin log) atau **diunduh** (ikon Unduh log)
bahkan setelah kontainer hilang. Keduanya bekerja pada log konsol lengkap instance, bukan hanya ekor di layar.

Sebuah `.algo` yang diunggah tidak pernah dibangun di sini, jadi kolom **Pembuatan Terakhir** miliknya di halaman cBot dibiarkan
kosong (menampilkan waktu pembuatan hanya untuk cBot yang Anda bangun di peramban).

## Edit & jalankan kembali instance yang dihentikan

Instance **dihentikan** (jalankan atau backtest) memiliki kontrol **Edit** — ikon di baris miliknya dalam daftar **dan**
di samping Jalankan/Hentikan di halaman detailnya — yang membuka dialog **isian sebelumnya** dengan konfigurasi miliknya saat ini.
Anda dapat mengubah **akun perdagangan, simbol, timeframe, parameter set dan tag citra** (dan, untuk backtest, **jendela dan semua pengaturan backtest** di atas), kemudian **Simpan & jalankan** meluncurkannya kembali dengan
pengaturan baru (menggantikan instance yang dihentikan). Kontrol **dinonaktifkan saat instance aktif** —
hanya instance yang dihentikan dapat diedit.

## Jalankan dari editor kode

Mengklik **Jalankan** di editor kode membuka dialog daripada menjalankan jalankan buta, hard-coded:

- **Akun perdagangan** (diperlukan) — akun cTrader yang terhubung ke cBot.
- **Parameter set** (opsional) — pilih set yang ada, atau biarkan kosong untuk menjalankan dengan **nilai parameter default** cBot.
  Tombol **+** di samping selector membuat parameter set baru secara inline (lihat di bawah) dan memilihnya.
- **Simbol / Timeframe** default ke `EURUSD` / `h1` dan dapat diubah; **Batalkan** atau **Jalankan**.

Di **Jalankan** editor menyimpan + membangun sumber saat ini, memulai instance pada akun yang dipilih
dengan parameter yang dipilih, kemudian menampilkan log kontainer langsung. (Aliran log meneruskan cookie auth pengguna yang masuk ke
hub SignalR `/hubs/logs`, sehingga terhubung daripada gagal dengan `Invalid negotiation response received`.)

## Parameter set

**Parameter set** adalah set parameter override cBot yang bernama, dapat digunakan kembali yang disimpan sebagai objek JSON datar
memetakan setiap nama parameter ke nilai skalar, mis. `{"Period": 14, "Label": "trend"}`. Saat waktu jalankan/backtest diubah menjadi
file cTrader `params.cbotset` (`{ "Parameters": { … } }`). Anda dapat membuat/mengedit set sebagai JSON mentah dari dialog **Parameter
set** cBot atau secara inline dari dialog Jalankan.

Setiap parameter set **milik cBot**: dialog Parameter Set Baru mencantumkan semua cBot Anda dan Anda
**harus memilih satu** — pembuatan diblokir sampai cBot dipilih. **Nama** set unik per cBot:
membuat atau mengganti nama set menjadi nama yang sudah digunakan set lain dari cBot yang sama ditolak (kesalahan jelas
dalam dialog, `409 Conflict` di API). Nama yang sama dapat digunakan kembali pada **cBot yang berbeda**.

JSON **divalidasi** saat disimpan: ini harus menjadi objek datar tunggal yang nilainya semua skalar
(string / angka / bool). Root non-object, larik, objek bersarang, nilai `null`, atau JSON yang rusak
ditolak (kesalahan jelas dalam dialog, `400 Bad Request` di API). Objek kosong `{}`
diizinkan dan berarti "tidak ada override".

## Catatan CLI cTrader Console

Backtest memerlukan `--data-mode` (default `m1`), tanggal sebagai `dd/MM/yyyy HH:mm`, dan
JSON argumen positional `params.cbotset`; `run` menolak `--data-dir` (hanya backtest). Lihat
`ContainerCommandHelpers`.

## Node & skala

Kapasitas eksekusi skala dengan menambahkan agen node (pendaftaran mandiri + heartbeat). Lihat
[penemuan node](../operations/node-discovery.md) dan [penskalaan](../deployment/scaling.md).

## Akun perdagangan diperlukan

Menjalankan atau melakukan backtest cBot memerlukan akun perdagangan cTrader untuk terhubung. Sampai Anda menambahkan satu di bawah
**Akun perdagangan**, tombol **Jalankan cBot Baru** / **Backtest cBot Baru** dinonaktifkan (dengan tooltip) dan halaman menampilkan permintaan yang tertaut ke pengaturan akun — Anda tidak lagi mendapat kesalahan `stream connect failed` mentah
dari bot tanpa akun.
