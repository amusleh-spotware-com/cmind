---
description: "Bangun, jalankan, backtest cBot cTrader (C# dan Python, keduanya .NET) dari Monaco IDE dalam browser, jalankan di gambar ghcr.io/spotware/ctrader-console resmi."
---

# Membangun & backtest cBot

Bangun, jalankan, backtest cBot cTrader (C# **dan** Python, keduanya .NET) dari Monaco
IDE dalam browser, jalankan di gambar `ghcr.io/spotware/ctrader-console` resmi.

## Membangun

- **Halaman Builder** menyimpan editor Monaco; `CBotBuilder` mengkompilasi proyek dengan
  `dotnet build` **dalam kontainer sekali pakai** (`AppOptions.BuildImage`, direktori kerja bind-mount
  di `/work`), sehingga target MSBuild pengguna yang tidak terpercaya tidak dapat menjangkau host. Pemulihan NuGet di-cache
  di seluruh build melalui volume bersama. Host web memerlukan akses soket Docker.
- Template pemula C# + Python berada di `src/Nodes/Builder/Templates/`.

## Menjalankan & backtest

- **Instance** = hierarki status TPH (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/
  `Running`/`Stopping`/`Stopped`/`Failed`). Transisi mengganti entitas (perubahan id),
  id kontainer dibawa ke depan.
- `NodeScheduler` memilih node yang paling sedikit dibebani; `ContainerDispatcherFactory` mengarahkan ke
  agen HTTP node jarak jauh atau dispatcher Docker lokal.
- Poller penyelesaian menyelaraskan kontainer yang keluar (kontainer backtest keluar sendiri melalui
  `--exit-on-stop`); laporan ada → selesai (simpan `ReportJson`), hilang → gagal.
- Log kontainer langsung mengalir ke browser melalui SignalR; kurva ekuitas backtest diurai dari
  laporan + dibuat grafik.

## Data pasar backtest di-cache per akun

cTrader Console mengunduh data tick/bar historis ke `--data-dir`-nya. Direktori itu adalah
**cache stabil dan persisten yang dikunci pada akun perdagangan** (nomor akun-nya) — bind-mount dari disk node
di jalur kontainernya sendiri (`/mnt/data`), **mount terpisah, non-bersarang** dari direktori kerja
per-instance. Jadi setiap backtest pada akun yang sama **menggunakan kembali** data yang sudah diunduh
alih-alih mengunduhnya lagi setiap kali berjalan. (Sebelumnya
direktori data berada di bawah direktori kerja per-instance, yang id-nya berubah setiap kali berjalan, yang memaksa pengunduhan segar setiap backtest.) Direktori kerja per-instance yang singkat masih menyimpan algo, param, kata sandi
dan laporan; cache data bersama dihitung dalam penggunaan data backtest node dan dihapus oleh
tindakan pembersihan node.

## Pengaturan backtest

Dialog **Backtest** menampilkan setiap pengaturan yang diterima CLI backtest cTrader Console, sehingga Anda tidak perlu
menyentuh baris perintah:

- **From / To** — jendela backtest (`--start` / `--end`).
- **Data mode** — `m1` (batang 1 menit) atau `tick` (`--data-mode`).
- **Starting balance** — default ke `10000` (`--balance`). **Keseimbangan 0 tidak melakukan perdagangan dan membuat
  cTrader mengeluarkan laporan kosong yang kemudian mogok** ("Message expected"), jadi keseimbangan bukan nol selalu
  dikirim.
- **Commission** dan **Spread** (`--commission` / `--spread`, spread dalam pips).
- **Advanced options** — kotak `name=value` bentuk bebas per baris untuk opsi backtest lainnya yang cTrader
  dukung (misalnya `applyCommissionAutomatically=true`); setiap baris menjadi argumen CLI `--name value`.

## Halaman detail instance

Membuka instance (`/instance/{id}`) menampilkan status, log langsung dan — untuk backtest — kurva ekuitas-nya.
Judul **tab browser** mencerminkan instance spesifik (**nama cBot · jenis · simbol**, misalnya
`TrendBot · Backtest · EURUSD`) sehingga tab jalankan-langsung dan tab backtest dapat dibedakan sekilas.
Jalankan dan backtest cBot yang sama dilacak sebagai **lineage** yang berbeda (id lineage stabil dibawa
di seluruh transisi status), jadi halaman mengikuti tepat satu instance dan tidak pernah mencampur data jalankan dengan
backtest-nya.

## Kontrol siklus hidup instance

Setiap baris instance (dan halaman detailnya) memiliki kontrol yang benar-benar sesuai dengan status. Instance **aktif** menampilkan
**Stop**; yang **terminal** (Stopped / Completed / Failed) menampilkan **Start (▶)** untuk meluncurkannya kembali dengan
cBot, akun, simbol, timeframe, set parameter, dan gambar yang sama (jalankan dimulai kembali sebagai jalankan, backtest sebagai backtest). Mengklik Stop menampilkan pemberitahuan "Stopping…" dan menonaktifkan ikon sampai
terselesaikan, dan jalankan yang baru dibuat muncul di daftar segera — tanpa muat ulang halaman.

Log konsol adalah **persisten ketika instance berakhir** — untuk jalankan (pada Stop) dan untuk
**backtest** (pada penyelesaian) — jadi log jalankan terakhir tetap dapat dilihat di halaman detail dan,
melalui toolbar log, **disalin ke clipboard** (ikon Copy logs) atau **diunduh** (ikon Download logs)
bahkan setelah kontainer hilang. Keduanya bertindak pada log konsol lengkap instance, bukan hanya
ekor on-screen.

`.algo` **yang diunggah** tidak pernah dibangun di sini, jadi kolom **Last Build**-nya di halaman cBot dibiarkan
kosong (hanya menampilkan waktu build untuk cBot yang Anda bangun di browser).

## Sunting & jalankan ulang instance yang dihentikan

Instance yang **dihentikan** (jalankan atau backtest) memiliki kontrol **Edit** — ikon di barisnya dalam daftar **dan**
di samping Start/Stop di halaman detailnya — yang membuka dialog **diprefill** dengan konfigurasi saat ini-nya.
Anda dapat mengubah **akun perdagangan, simbol, timeframe, set parameter dan tag gambar** (dan, untuk
backtest, **jendela dan semua pengaturan backtest** di atas), kemudian **Simpan & mulai** meluncurkannya kembali dengan
pengaturan baru (mengganti instance yang dihentikan). Kontrol **dinonaktifkan saat instance aktif** —
hanya instance yang dihentikan yang dapat disunting.

## Jalankan dari editor kode

Mengklik **Run** dalam editor kode membuka dialog alih-alih memulai jalankan buta berkode keras:

- **Trading account** (diperlukan) — akun cTrader yang terhubung oleh cBot.
- **Parameter set** (opsional) — pilih set yang sudah ada, atau biarkan kosong untuk menjalankan dengan **nilai parameter default** cBot.
  Tombol **+** di samping pemilih membuat set parameter baru inline (lihat di bawah) dan memilihnya.
- **Symbol / Timeframe** default ke `EURUSD` / `h1` dan dapat diubah; **Cancel** atau **Run**.

Di **Run** editor menyimpan + membangun sumber saat ini, memulai instance pada akun yang dipilih
dengan parameter yang dipilih, kemudian mencatat log kontainer langsung. (Aliran log meneruskan cookie auth pengguna yang masuk ke
hub SignalR `/hubs/logs`, jadi tersambung daripada gagal dengan
`Invalid negotiation response received`.)

## Set parameter

**Parameter set** adalah set parameter override cBot yang dinamai dan dapat digunakan kembali disimpan sebagai objek JSON rata
yang memetakan setiap nama parameter ke nilai skalar, misalnya `{"Period": 14, "Label": "trend"}`. Pada
waktu jalankan/backtest diubah menjadi file cTrader `params.cbotset`
(`{ "Parameters": { … } }`). Anda dapat membuat/menyunting set sebagai JSON mentah dari dialog **Parameter
sets** cBot atau inline dari dialog Run.

Setiap set parameter **milik cBot**: dialog New Parameter Set mencantumkan semua cBot Anda dan Anda
**harus memilih satu** — pembuatan diblokir sampai cBot dipilih. **Nama** set adalah **unik per cBot**:
membuat atau mengganti nama set ke nama yang sudah digunakan set lain dari cBot yang sama ditolak (kesalahan jelas
dalam dialog, `409 Conflict` di API). Nama yang sama dapat digunakan kembali pada **cBot berbeda**.

JSON adalah **divalidasi** saat disimpan: harus menjadi objek rata tunggal yang nilainya semuanya skalar
(string / number / bool). Root non-objek, array, objek bersarang, nilai `null`, atau
JSON yang salah format ditolak (kesalahan jelas dalam dialog, `400 Bad Request` di API). Objek kosong `{}`
diizinkan dan berarti "tidak ada override".

## Catatan CLI cTrader Console

Backtest memerlukan `--data-mode` (default `m1`), tanggal sebagai `dd/MM/yyyy HH:mm`, dan
argumen positional JSON `params.cbotset`; `run` menolak `--data-dir` (hanya backtest). Lihat
`ContainerCommandHelpers`.

## Node & skala

Kapasitas eksekusi skala dengan menambahkan agen node (daftar-diri + heartbeat). Lihat
[node discovery](../operations/node-discovery.md) dan [scaling](../deployment/scaling.md).

## Akun perdagangan diperlukan

Menjalankan atau backtest cBot memerlukan akun perdagangan cTrader untuk terhubung. Sampai Anda menambahkan satu di bawah
**Trading accounts**, tombol **Run New cBot** / **Backtest New cBot** dinonaktifkan (dengan
tooltip) dan halaman menampilkan prompt yang menautkan ke pengaturan akun — Anda tidak lagi memberi error `stream connect failed` mentah
dari bot tanpa akun.
