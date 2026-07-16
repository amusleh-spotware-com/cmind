---
description: "Build, run, backtest cBots cTrader (C# dan Python, keduanya .NET) dari Monaco IDE in-browser, jalankan pada image resmi ghcr.io/spotware/ctrader-console."
---

# Build & backtest cBots

Build, run, backtest cBots cTrader (C# **dan** Python, keduanya .NET) dari Monaco IDE
in-browser, jalankan pada image resmi `ghcr.io/spotware/ctrader-console`.

## Build

- Halaman **Builder** menampung editor Monaco; `CBotBuilder` kompilasi proyek dengan
  `dotnet build` **dalam container sekali pakai** (`AppOptions.BuildImage`, work dir bind-mount
  pada `/work`), sehingga MSBuild pengguna tak terpercaya tidak dapat menjangkau host. NuGet restore
  di-cache lintas build melalui volume bersama. Web host perlu akses soket Docker.
- Template starter C# + Python tinggal di `src/Nodes/Builder/Templates/`.

## Run & backtest

- **Instances** = hirarki state TPH (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/
  `Running`/`Stopping`/`Stopped`/`Failed`). Transisi ganti entitas (perubahan id),
  container id dibawa maju.
- `NodeScheduler` pilih node paling sedikit beban; `ContainerDispatcherFactory` rute ke
  agen HTTP node jarak jauh atau dispatcher Docker lokal.
- Pollers penyelesaian sesuaikan container keluar (backtest container self-exit via
  `--exit-on-stop`); laporan ada → selesai (simpan `ReportJson`), hilang → gagal.
- Log container live alir ke browser atas SignalR; kurva ekuitas backtest parse dari
  laporan + grafik.

## Data pasar backtest di-cache per akun

cTrader Console unduh data tick/bar historis ke dalam `--data-dir` miliknya. Direktori itu adalah
**cache stabil, persisten dikunci pada akun perdagangan** (nomor akunnya) — bind-mounted dari disk
node pada path containernya sendiri (`/mnt/data`), **mount terpisah, tidak bersarang** dari work dir
per-instance. Jadi setiap backtest pada akun sama **gunakan kembali** data yang sudah diunduh
bukan mengunduh kembali setiap run. (Dulu data dir tinggal di bawah work dir per-instance, yang id
berubah setiap run, yang paksa unduhan segar setiap backtest.) Work dir ephemeral per-instance masih
pegang algo, params, password dan laporan; cache data bersama hitung dalam penggunaan backtest-data
node dan hapus oleh aksi node-clean.

## Pengaturan backtest

Dialog **Backtest** tunjuk pengaturan backtest cTrader Console yang dapat disesuaikan pengguna,
jadi Anda tidak perlu sentuh command line:

- **Symbol / Timeframe** — timeframe adalah **dropdown setiap period cTrader** (`t1`…`t1000`,
  `m1`…`m45`, `h1`…`h12`, `D1`/`D2`/`D3`, `W1`, `Month1`, dan period Renko/Range/Heikin), dalam
  casing kanonik console, jadi Anda selalu pilih `--period` yang valid.
- **From / To** — jendela backtest (`--start` / `--end`).
- **Data mode** — salah satu dari tiga mode cTrader (`--data-mode`): **Tick data** (`tick`, akurat),
  **m1 bars** (`m1`, cepat), atau **Open prices only** (`open`, tercepat).
- **Starting balance** — default ke `10000` (`--balance`). Saldo **0 tidak tempat trade dan buat
  cTrader keluarkan laporan kosong yang kemudian crash** ("Message expected"), jadi saldo
  non-zero selalu kirim.
- **Commission** — `--commission`.
- **Spread** — `--spread`, **field numerik dalam pips yang tidak bisa bawah 0**. Itu **tersembunyi dalam mode Tick
  data**, di mana cTrader ambil spread dari data tick itu sendiri (tidak ada `--spread` kirim).

Direktori data (`--data-file` / `--data-dir`) dikelola oleh app itu sendiri (cache per-account, lihat
atas), tidak tunjuk dalam dialog.

:::note cTrader crash pada backtest kosong
Jika backtest hasilkan **tidak ada hasil** — tidak ada trade, atau tidak ada data pasar untuk tanggal/simbol yang dipilih —
report writer cTrader Console sendiri lempar `Message expected` dan keluar tanpa laporan. App tidak bisa
perbaiki bug upstream itu, tapi deteksi dan tandai instance **Failed** dengan alasan dapat tindak
("tidak ada hasil backtest untuk rentang terpilih…") bukan raw stack trace. Pilih rentang tanggal lebih luas
yang ada data pasar dan coba lagi.
:::

## Halaman detail instance

Buka instance (`/instance/{id}`) tunjuk status live, log dan — untuk backtest — kurva
ekuitas. **Judul tab browser** cermin instance spesifik (**nama cBot · kind · symbol**, mis.
`TrendBot · Backtest · EURUSD`) jadi tab live-run dan tab backtest beda sekali pandang.
Run dan backtest dari cBot sama lacak sebagai **lineages** berbeda (id lineage stabil
dibawa lintas transisi state), jadi halaman ikuti instance tepat saja dan tidak
pernah campur data run dengan backtest.

## Kontrol lifecycle instance

Setiap baris instance (dan halaman detail-nya) punya kontrol state-correct. Instance **aktif**
tunjuk **Stop**; yang **terminal** (Stopped / Completed / Failed) tunjuk **Start (▶)**
untuk luncurkan lagi dengan cBot, account, symbol, timeframe, parameter set dan image sama
(run mulai ulang sebagai run, backtest sebagai backtest). Klik Stop tunjuk pemberitahuan
"Stopping…" dan nonaktif ikon sampai selesai, dan run baru buat muncul dalam daftar segera — tidak ada reload halaman.

Log konsol **pertahan saat instance terminates** — untuk run (pada Stop) dan untuk
**backtest** (pada completion) — jadi log run terakhir tetap lihat pada halaman detail
dan, lewat toolbar log, **salin ke clipboard** (ikon Copy logs) atau **unduh** (ikon Download logs)
bahkan setelah container hilang. Keduanya tindak pada log konsol lengkap instance, bukan hanya ekor
on-screen.

`.algo` yang **upload** tidak pernah build di sini, jadi kolom **Last Build** di halaman cBots
biarkan kosong (tunjuk waktu build hanya untuk cBots yang Anda build di browser).

## Edit & re-run instance yang dihenti

Instance **dihenti** (run atau backtest) punya kontrol **Edit** — ikon pada barisnya dalam daftar
**dan** di sebelah Start/Stop pada halaman detail-nya — yang buka dialog **prefill** dengan konfigurasi saat ini.
Anda bisa ubah **trading account, symbol, timeframe, parameter set dan image tag** (dan, untuk
backtest, **window dan semua pengaturan backtest** atas), lalu **Save & start** luncurkan lagi dengan
pengaturan baru (ganti instance dihenti). Kontrol **nonaktif saat instance aktif** —
hanya instance dihenti yang bisa edit.

## Run dari editor kode

Klik **Run** dalam editor kode buka dialog bukan tembak run buta hard-coded:

- **Trading account** (required) — akun cTrader yang cBot terhubung.
- **Parameter set** (optional) — pilih set ada, atau biarkan kosong untuk run dengan
  **nilai parameter default** cBot. Tombol **+** di sebelah selector buat parameter set baru
  inline (lihat bawah) dan pilih.
- **Symbol / Timeframe** default ke `EURUSD` / `h1` dan bisa ubah; **Cancel** atau **Run**.

Pada **Run** editor simpan + build source saat ini, mulai instance pada account terpilih
dengan parameter terpilih, lalu tail log container live. (Aliran log teruskan cookie auth
pengguna signin ke hub SignalR `/hubs/logs`, jadi terhubung bukan gagal dengan
`Invalid negotiation response received`.)

## Parameter sets

**Parameter set** adalah set parameter override cBot bernama, reusable disimpan sebagai flat JSON
object petakan setiap nama parameter ke nilai skalar, mis. `{"Period": 14, "Label": "trend"}`. Pada
run/backtest time ubah menjadi file cTrader `params.cbotset`
(`{ "Parameters": { … } }`). Anda bisa buat/edit set sebagai raw JSON dari dialog **Parameter
sets** cBot atau inline dari dialog Run.

Setiap parameter set **milik cBot**: dialog New Parameter Set daftar semua cBot Anda dan Anda
**harus pilih satu** — creation blokir sampai cBot dipilih. Set **name unik per cBot**:
buat atau rename set ke nama yang set lain dari cBot sama sudah pakai ditolak (error jelas dalam dialog, `409 Conflict` di API). Nama sama dapat dipakai ulang pada cBot **berbeda**.

JSON **validasi** pada save: harus single flat object yang nilai semua skalar
(string / number / bool). Root non-object, array, nested object, nilai `null`, atau JSON salah format
ditolak (error jelas dalam dialog, `400 Bad Request` di API). Object kosong `{}`
diizinkan dan berarti "tidak ada override".

## Catatan cTrader Console CLI

Backtest butuh `--data-mode` (default `m1`), tanggal sebagai `dd/MM/yyyy HH:mm`, dan
argumen positional JSON `params.cbotset`; `run` tolak `--data-dir` (backtest-only). Lihat
`ContainerCommandHelpers`.

## Nodes & scale

Kapasitas eksekusi scale dengan tambah agen node (self-register + heartbeat). Lihat
[node discovery](../operations/node-discovery.md) dan [scaling](../deployment/scaling.md).
## Trading account diperlukan

Run atau backtest cBot butuh akun perdagangan cTrader untuk terhubung. Sampai Anda tambah satu di bawah
**Trading accounts**, tombol **Run New cBot** / **Backtest New cBot** nonaktif (dengan
tooltip) dan halaman tunjuk prompt hubung ke pengaturan account — Anda tidak lagi kena raw
`stream connect failed` error dari bot tanpa account.
