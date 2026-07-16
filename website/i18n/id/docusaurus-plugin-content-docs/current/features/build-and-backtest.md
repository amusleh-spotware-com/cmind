---
description: "Bangun, jalankan, backtest cTrader cBots (C# dan Python, keduanya .NET) dari IDE Monaco dalam browser, jalankan pada image resmi ghcr.io/spotware/ctrader-console."
---

# Build & backtest cBots

Bangun, jalankan, backtest cTrader cBots (C# **dan** Python, keduanya .NET) dari IDE Monaco dalam browser, jalankan pada image resmi `ghcr.io/spotware/ctrader-console`.

## Build

- **Halaman Builder** menyediakan editor Monaco; `CBotBuilder` mengkompilasi project dengan `dotnet build` **dalam container sekali pakai** (`AppOptions.BuildImage`, work dir bind-mount di `/work`), sehingga MSBuild target dari user yang tidak terpercaya tidak dapat menjangkau host. NuGet restore di-cache antar build melalui shared volume. Web host memerlukan akses Docker socket.
- Template pemula C# + Python tersimpan di `src/Nodes/Builder/Templates/`.

## Run & backtest

- **Instances** = hierarki state TPH (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/`Running`/`Stopping`/`Stopped`/`Failed`). Transisi mengganti entity (perubahan id), container id dibawa ke depan.
- `NodeScheduler` memilih node yang paling sedikit beban; `ContainerDispatcherFactory` mengarahkan ke HTTP agent node jarak jauh atau local Docker dispatcher.
- Completion pollers melakukan rekonsiliasi container yang keluar (backtest container self-exit melalui `--exit-on-stop`); laporan hadir → completed (menyimpan `ReportJson`), hilang → failed.
- Live container logs streaming ke browser melalui SignalR; equity curves backtest diurai dari laporan + di-chart.

## Backtest market data is cached per account

Konsol cTrader mengunduh data tick/bar historis ke `--data-dir`-nya. Direktori tersebut adalah **cache stabil, persisten yang dikunci pada trading account** (nomor akun-nya) — bind-mounted dari disk node di path container-nya sendiri (`/mnt/data`), mount **terpisah, tidak bersarang** dari work dir per-instance. Jadi setiap backtest di akun yang sama **menggunakan kembali** data yang sudah diunduh, daripada mengunduhnya lagi setiap run. (Sebelumnya data dir berada di bawah work dir per-instance, yang id-nya berubah setiap run, memaksa unduhan baru setiap backtest.) Work dir per-instance yang bersifat ephemeral masih menyimpan algo, params, password dan laporan; cache data bersama dihitung dalam penggunaan backtest-data node dan dihapus oleh aksi node-clean.

## Backtest settings

Dialog **Backtest** menampilkan setiap setting yang diterima CLI backtest cTrader, sehingga Anda tidak perlu menyentuh baris perintah:

- **From / To** — jendela backtest (`--start` / `--end`).
- **Data mode** — salah satu dari tiga mode cTrader (`--data-mode`): **Tick data** (`tick`, akurat), **m1 bars** (`m1`, cepat), atau **Open prices only** (`open`, tercepat).
- **Starting balance** — default ke `10000` (`--balance`). **Balance 0 tidak menempatkan trades dan membuat cTrader mengeluarkan laporan kosong yang kemudian crash** ("Message expected"), jadi balance non-zero selalu dikirim.
- **Commission** dan **Spread** — `--commission` / `--spread` (spread dalam pips).
- **Data file** (opsional) — path sisi node ke file data historis (`--data-file`); biarkan kosong untuk menggunakan data yang diunduh/di-cache.
- **Expose environment variables** — toggle yang melewatkan environment variables host ke cBot (`--environment-variables` flag).

## Instance detail page

Membuka instance (`/instance/{id}`) menampilkan status live, logs, dan — untuk backtest — equity curve. **Judul tab browser** mencerminkan instance spesifik (**nama cBot · jenis · simbol**, mis. `TrendBot · Backtest · EURUSD`) sehingga tab live-run dan tab backtest dapat dibedakan sekilas. Run dan backtest dari cBot yang sama dilacak sebagai **lineages** yang berbeda (stable lineage id dibawa antar state transitions), sehingga halaman mengikuti satu instance dan tidak pernah mencampurkan data run dengan backtest.

## Instance lifecycle controls

Setiap baris instance (dan halaman detailnya) memiliki kontrol yang sesuai state. Instance **aktif** menampilkan **Stop**; instance **terminal** (Stopped / Completed / Failed) menampilkan **Start (▶)** untuk me-launch ulang dengan cBot, akun, simbol, timeframe, parameter set dan image yang sama (run restart sebagai run, backtest sebagai backtest). Mengklik Stop menampilkan pemberitahuan "Stopping…" dan menonaktifkan icon sampai terpecahkan, dan run yang baru dibuat muncul di list segera — tidak perlu reload halaman.

Console logs **persisten ketika instance terminates** — untuk run (di Stop) dan untuk **backtest** (di completion) — sehingga logs dari run terakhir tetap dapat dilihat di halaman detail dan, melalui toolbar log, **disalin ke clipboard** (Copy logs icon) atau **diunduh** (Download logs icon) bahkan setelah container hilang. Keduanya bertindak pada full console log instance, bukan hanya tail di-screen.

**.algo** yang **uploaded** tidak pernah dibangun di sini, jadi kolom **Last Build**-nya di halaman cBots dibiarkan kosong (hanya menampilkan build time untuk cBots yang Anda bangun di browser).

## Edit & re-run a stopped instance

Instance yang **stopped** (run atau backtest) memiliki kontrol **Edit** — icon di baris-nya dalam list **dan** di samping Start/Stop di halaman detailnya — yang membuka dialog **prefilled** dengan konfigurasi saat ini-nya. Anda dapat mengubah **trading account, simbol, timeframe, parameter set dan image tag** (dan, untuk backtest, **window dan semua backtest settings** di atas), kemudian **Save & start** meluncurkan ulang dengan setting baru (mengganti instance yang stopped). Kontrol **dinonaktifkan saat instance aktif** — hanya instance yang stopped yang dapat diedit.

## Run from the code editor

Mengklik **Run** di code editor membuka dialog, daripada menjalankan run blind yang hard-coded:

- **Trading account** (required) — akun cTrader yang terhubung ke cBot.
- **Parameter set** (opsional) — pilih set yang sudah ada, atau biarkan kosong untuk menjalankan dengan **default parameter values** cBot. Tombol **+** di samping selector membuat parameter set baru secara inline (lihat di bawah) dan memilihnya.
- **Symbol / Timeframe** default ke `EURUSD` / `h1` dan dapat diubah; **Cancel** atau **Run**.

Di **Run** editor menyimpan + membangun source saat ini, memulai instance pada akun yang dipilih dengan parameter yang dipilih, kemudian membuntut live container logs. (Stream log meneruskan auth cookie user yang signed-in ke hub SignalR `/hubs/logs`, sehingga terhubung daripada gagal dengan `Invalid negotiation response received`.)

## Parameter sets

**Parameter set** adalah named, reusable set dari cBot parameter overrides yang disimpan sebagai flat JSON object yang memetakan setiap nama parameter ke scalar value, mis. `{"Period": 14, "Label": "trend"}`. Pada run/backtest time diubah menjadi file `params.cbotset` cTrader (`{ "Parameters": { … } }`). Anda dapat membuat/mengedit set sebagai raw JSON dari dialog **Parameter sets** cBot atau inline dari Run dialog.

Setiap parameter set **milik cBot**: dialog New Parameter Set mendaftar semua cBots Anda dan Anda **harus memilih satu** — pembuatan diblokir sampai cBot dipilih. **Nama** set **unik per cBot**: membuat atau mengganti nama set ke nama yang sudah digunakan set lain dari cBot yang sama ditolak (error jelas di dialog, `409 Conflict` di API). Nama yang sama dapat digunakan ulang pada **cBot berbeda**.

JSON **divalidasi** saat save: harus berupa single flat object yang values-nya semua scalars (string / number / bool). Non-object root, array, nested object, `null` value, atau malformed JSON ditolak (error jelas di dialog, `400 Bad Request` di API). Empty object `{}` diizinkan dan berarti "no overrides".

## cTrader Console CLI notes

Backtests memerlukan `--data-mode` (default `m1`), dates sebagai `dd/MM/yyyy HH:mm`, dan `params.cbotset` JSON positional arg; `run` menolak `--data-dir` (backtest-only). Lihat `ContainerCommandHelpers`.

## Nodes & scale

Kapasitas eksekusi scale dengan menambahkan node agents (self-register + heartbeat). Lihat [node discovery](../operations/node-discovery.md) dan [scaling](../deployment/scaling.md).

## A trading account is required

Menjalankan atau membacktest cBot memerlukan trading account cTrader untuk terhubung. Sampai Anda menambahkan satu di bawah **Trading accounts**, tombol **Run New cBot** / **Backtest New cBot** dinonaktifkan (dengan tooltip) dan halaman menampilkan prompt yang menautkan ke account setup — Anda tidak lagi menerima raw `stream connect failed` error dari bot tanpa akun.
