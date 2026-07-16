---
description: "Bina, jalankan, backtest cTrader cBots (C# dan Python, kedua-duanya .NET) daripada IDE Monaco dalam pelayar, jalankan pada imej ghcr.io/spotware/ctrader-console rasmi."
---

# Build & backtest cBots

Bina, jalankan, backtest cTrader cBots (C# **dan** Python, kedua-duanya .NET) daripada IDE Monaco dalam pelayar, jalankan pada imej `ghcr.io/spotware/ctrader-console` rasmi.

## Build

- Halaman **Builder** hos editor Monaco; `CBotBuilder` mengkompil projek dengan `dotnet build` **dalam bekas yang boleh dibuang** (`AppOptions.BuildImage`, direktori kerja bind-mount di `/work`), jadi sasaran MSBuild pengguna tidak dipercayai tidak dapat mencapai hos. Pemulihan NuGet disimpan dalam cache merentasi binaan melalui volum bersama. Hos web memerlukan akses soket Docker.
- Templat pemula C# + Python disimpan dalam `src/Nodes/Builder/Templates/`.

## Run & backtest

- **Instances** = hierarki keadaan TPH (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/`Running`/`Stopping`/`Stopped`/`Failed`). Peralihan menggantikan entiti (perubahan id), id bekas dibawa.
- `NodeScheduler` memilih nod yang paling sedikit dibebani yang layak; `ContainerDispatcherFactory` mengarahkan ke ejen HTTP nod jauh atau penghantar Docker tempatan.
- Poller siap menyelaraskan bekas yang keluar (bekas backtest keluar sendiri melalui `--exit-on-stop`); laporan hadir → siap (simpan `ReportJson`), hilang → gagal.
- Log bekas langsung dialirkan ke pelayar melalui SignalR; lengkung ekuiti backtest diuraikan daripada laporan + dicarta.

## Backtest market data is cached per account

cTrader Console memuat turun data tik/bar sejarah ke dalam `--data-dir`-nya. Direktori itu ialah **cache stabil, berterusan yang dikunci pada akaun dagangan** (nombor akaunnya) — bind-mounted daripada cakera nod di laluan bekas sendirinya (`/mnt/data`), **mount berasingan, tidak bersarang** daripada direktori kerja per-instance. Jadi setiap backtest pada akaun yang sama **menggunakan semula** data yang telah dimuat turun daripada menjalankan muat turun semula setiap kali. (Dahulu direktori data tinggal di bawah direktori kerja per-instance, yang id-nya berubah setiap kali jalankan, yang memaksa muat turun segar setiap backtest.) Direktori kerja per-instance yang sementara masih memegang algo, param, kata laluan dan laporan; cache data bersama dikira dalam penggunaan backtest-data nod dan dipadamkan oleh tindakan pembersihan nod.

## Backtest settings

Dialog **Backtest** mendedahkan tetapan backtest konsol cTrader yang boleh disesuaikan pengguna, jadi anda tidak perlu menyentuh baris arahan:

- **Symbol / Timeframe** — timeframe ialah **dropdown bagi setiap tempoh cTrader** (`t1`…`t1000`, `m1`…`m45`, `h1`…`h12`, `D1`/`D2`/`D3`, `W1`, `Month1`, dan tempoh Renko/Julat/Heikin), dalam aksara kanon konsol, jadi anda sentiasa memilih `--period` yang sah.
- **From / To** — tetingkap backtest (`--start` / `--end`).
- **Data mode** — satu daripada tiga mod cTrader (`--data-mode`): **Tick data** (`tick`, tepat), **m1 bars** (`m1`, pantas), atau **Open prices only** (`open`, paling pantas).
- **Starting balance** — lalai kepada `10000` (`--balance`). **Baki 0 tidak melakukan dagangan dan menyebabkan cTrader mengeluarkan laporan kosong yang kemudiannya ranap** ("Message expected"), jadi baki bukan sifar selalu dihantar.
- **Commission** dan **Spread** — `--commission` / `--spread` (spread dalam pips).

Direktori data (`--data-file` / `--data-dir`) diurus oleh aplikasi itu sendiri (cache per-akaun, lihat di atas), tidak terdedah dalam dialog.

## Instance detail page

Membuka instance (`/instance/{id}`) menunjukkan status langsung, log dan — untuk backtest — lengkung ekuiti. **Tajuk tab pelayar** mencerminkan instance tertentu (**nama cBot · jenis · simbol**, contoh `TrendBot · Backtest · EURUSD`) jadi tab larian langsung dan tab backtest mudah dibezakan sepintas lalu. Satu larian dan backtest cBot yang sama dijejak sebagai **lineages** yang berbeza (id lineage stabil yang dibawa merentasi peralihan keadaan), jadi halaman mengikuti tepat satu instance dan tidak pernah mencampurkan data larian dengan backtest.

## Instance lifecycle controls

Setiap baris instance (dan halaman detailnya) mempunyai kawalan yang betul keadaan. Instance **aktif** menunjukkan **Stop**; satu **terminal** (Stopped / Completed / Failed) menunjukkan **Start (▶)** untuk melancarkannya semula dengan cBot yang sama, akaun, simbol, timeframe, set parameter dan imej (larian bermula semula sebagai larian, backtest sebagai backtest). Mengklik Stop menunjukkan notis "Stopping…" dan melumpuhkan ikon sehingga ia diselesaikan, dan larian yang baru dibuat muncul dalam senarai serta-merta — tiada pemuatan semula halaman.

Log konsol adalah **berterusan apabila instance ditamatkan** — untuk larian (pada Henti) dan untuk **backtest** (pada siap) sama — jadi log larian terakhir kekal boleh dilihat di halaman detail dan, melalui bar alat log, **disalin ke papan klip** (ikon Salin log) atau **dimuat turun** (ikon Muat turun log) walaupun selepas bekas hilang. Kedua-duanya bertindak pada log konsol penuh instance, bukan hanya ekor pada skrin.

`.algo` **dimuat naik** tidak pernah dibina di sini, jadi lajur **Last Build** di halaman cBots ditinggalkan kosong (ia menunjukkan masa binaan hanya untuk cBots yang anda bina dalam pelayar).

## Edit & re-run a stopped instance

Instance yang **dihenti** (jalankan atau backtest) mempunyai kawalan **Edit** — ikon pada barisnya dalam senarai **dan** di sebelah Start/Stop di halaman detailnya — yang membuka dialog **dipra-isi** dengan konfigurasi semasa. Anda boleh mengubah **akaun dagangan, simbol, timeframe, set parameter dan tag imej** (dan, untuk backtest, **tetingkap dan semua tetapan backtest** di atas), kemudian **Simpan & mulai** melancarkannya semula dengan tetapan baru (menggantikan instance yang dihenti). Kawalan adalah **dilumpuhkan semasa instance aktif** — hanya instance yang dihenti boleh disunting.

## Run from the code editor

Mengklik **Run** dalam editor kod membuka dialog bukannya menjalankan larian buta yang dikodkan keras:

- **Trading account** (diperlukan) — akaun cTrader yang cBot menyambung kepada.
- **Parameter set** (pilihan) — pilih set sedia ada, atau biarkan kosong untuk menjalankan dengan **nilai parameter lalai cBot**. Butang **+** di sebelah pemilih membuat set parameter baru dalam talian (lihat di bawah) dan memilihnya.
- **Symbol / Timeframe** lalai kepada `EURUSD` / `h1` dan boleh diubah; **Batal** atau **Jalankan**.

Pada **Jalankan** editor menyimpan + membina sumber semasa, memulakan instance di akaun yang dipilih dengan parameter yang dipilih, kemudian menyapu log bekas langsung. (Aliran log memajukan kuki auth pengguna yang log masuk ke hab SignalR `/hubs/logs`, jadi ia menyambung bukannya gagal dengan `Invalid negotiation response received`.)

## Parameter sets

**Parameter set** ialah set parameter cBot yang dipanggil, boleh digunakan semula yang disimpan sebagai objek JSON rata yang memetakan setiap nama parameter kepada nilai skalar, contoh `{"Period": 14, "Label": "trend"}`. Pada masa larian/backtest ia bertukar menjadi fail cTrader `params.cbotset` (`{ "Parameters": { … } }`). Anda boleh membuat/menyunting set sebagai JSON mentah daripada dialog **Parameter sets** cBot atau dalam talian daripada dialog Run.

Setiap set parameter **tergolong dalam cBot**: dialog New Parameter Set menyenaraikan semua cBots anda dan anda **mesti memilih satu** — penciptaan disekat sehingga cBot dipilih. **Nama** set adalah **unik per cBot**: membuat atau menamakan semula set kepada nama yang set lain cBot yang sama sudah gunakan ditolak (ralat yang jelas dalam dialog, `409 Conflict` pada API). Nama yang sama mungkin digunakan semula pada **cBot yang berbeza**.

JSON adalah **disahkan** pada simpan: ia mesti menjadi objek rata tunggal yang nilai-nilainya semua skalar (rentetan / nombor / bool). Punca bukan objek, tatasusunan, objek bersarang, nilai `null`, atau JSON tidak sesuai ditolak (ralat yang jelas dalam dialog, `400 Bad Request` pada API). Objek kosong `{}` dibenarkan dan bermakna "tiada penggantian".

## cTrader Console CLI notes

Backtest memerlukan `--data-mode` (lalai `m1`), tarikh sebagai `dd/MM/yyyy HH:mm`, dan hujah kedudukan JSON `params.cbotset`; `run` menolak `--data-dir` (backtest sahaja). Lihat `ContainerCommandHelpers`.

## Nodes & scale

Kapasiti pelaksanaan berskala dengan menambah ejen nod (daftar sendiri + denyut nadi). Lihat [penemuan nod](../operations/node-discovery.md) dan [penskalaan](../deployment/scaling.md).

## A trading account is required

Menjalankan atau memacu belakang cBot memerlukan akaun dagangan cTrader untuk menyambung kepada. Sehingga anda menambah satu di bawah **Trading accounts**, butang **Run New cBot** / **Backtest New cBot** dilumpuhkan (dengan petua alat) dan halaman menunjukkan gesaran yang menghubungkan ke persediaan akaun — anda tidak lagi mencapai ralat `stream connect failed` mentah daripada bot tanpa akaun.
