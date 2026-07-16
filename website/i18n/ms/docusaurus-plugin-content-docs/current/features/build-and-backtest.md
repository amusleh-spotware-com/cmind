---
description: "Bina, jalankan, backtest cBot cTrader (C# dan Python, kedua-duanya .NET) dari IDE Monaco dalam pelayar, jalankan pada imej rasmi `ghcr.io/spotware/ctrader-console`."
---

# Build & backtest cBots

Bina, jalankan, backtest cBot cTrader (C# **dan** Python, kedua-duanya .NET) dari IDE Monaco dalam pelayar, jalankan pada imej rasmi `ghcr.io/spotware/ctrader-console`.

## Build

- Halaman **Builder** mengandungi editor Monaco; `CBotBuilder` mengkompil projek dengan `dotnet build` **dalam kontena sekali buang** (`AppOptions.BuildImage`, direktori kerja bind-mount pada `/work`), jadi sasaran MSBuild pengguna yang tidak dipercayai tidak dapat menjangkau hos. Pemulihan NuGet disimpan cache merentasi pembinaan melalui volum kongsi. Hos web memerlukan akses soket Docker.
- Templat permulaan C# + Python tinggal dalam `src/Nodes/Builder/Templates/`.

## Run & backtest

- **Instances** = hierarki keadaan TPH (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/`Running`/`Stopping`/`Stopped`/`Failed`). Peralihan menggantikan entiti (perubahan id), id kontena dibawa.
- `NodeScheduler` memilih nod yang paling sedikit dibebankan yang layak; `ContainerDispatcherFactory` menghala ke agen HTTP nod jauh atau penghantar Docker tempatan.
- Penghantar penyelesaian menyelaraskan kontena yang keluar (kontena backtest keluar sendiri melalui `--exit-on-stop`); laporan hadir → selesai (simpan `ReportJson`), hilang → gagal.
- Log kontena langsung mengalir ke pelayar melalui SignalR; lengkung ekuiti backtest dianalisis daripada laporan + dicarta.

## Backtest market data is cached per account

Konsol cTrader memuat turun data tick/bar sejarah ke `--data-dir`-nya. Direktori tersebut ialah **cache stabil dan berterusan yang dipasung pada akaun perdagangan** (nombor akunnya) — bind-mounted daripada cakera nod pada laluan kontena sendiri (`/mnt/data`), **mount berasingan yang tidak bersarang** daripada direktori kerja per-instance. Jadi setiap backtest pada akaun yang sama **menggunakan semula** data yang telah dimuat turun daripada menjalankan muat turun semula setiap larian. (Lebih awal direktori data tinggal di bawah direktori kerja per-instance, yang id berubah setiap larian, yang memaksa muat turun segar setiap backtest.) Direktori kerja ephemeral per-instance masih memegang algo, param, kata laluan dan laporan; cache data kongsi dikira dalam penggunaan data backtest nod dan dibersihkan oleh tindakan pembersihan nod.

## Backtest settings

Dialog **Backtest** mendedahkan pengaturan backtest konsol cTrader yang dapat disesuaikan pengguna, jadi anda tidak perlu menyentuh baris perintah:

- **Symbol / Timeframe** — timeframe ialah **menu lungsur setiap tempoh cTrader** (`t1`…`t1000`, `m1`…`m45`, `h1`…`h12`, `D1`/`D2`/`D3`, `W1`, `Month1`, dan tempoh Renko/Range/Heikin), dalam perumahan kanonik konsol, jadi anda sentiasa memilih `--period` yang sah.
- **From / To** — tetingkap backtest (`--start` / `--end`).
- **Data mode** — satu daripada tiga mod cTrader (`--data-mode`): **Tick data** (`tick`, tepat), **m1 bars** (`m1`, cepat), atau **Open prices only** (`open`, paling cepat).
- **Starting balance** — lalai kepada `10000` (`--balance`). **Baki 0 menempatkan tiada perdagangan dan membuat cTrader memancarkan laporan kosong yang kemudian ranap** ("Message expected"), jadi baki bukan sifar sentiasa dihantar.
- **Commission** — `--commission`.
- **Spread** — `--spread`, **medan berangka dalam pips yang tidak boleh turun di bawah 0**. Ia **tersembunyi dalam mod Tick data**, di mana cTrader mendapatkan penyebaran daripada data tick itu sendiri (tiada `--spread` dihantar).

Direktori data (`--data-file` / `--data-dir`) diuruskan oleh aplikasi itu sendiri (cache per-akaun, lihat di atas), bukan didedahkan dalam dialog.

:::note cTrader crashes on an empty backtest
Jika backtest menghasilkan **tiada hasil** — tiada perdagangan, atau tiada data pasaran untuk tarikh/simbol yang dipilih — penulis laporan konsol cTrader sendiri membaling `Message expected` dan keluar tanpa laporan. Aplikasi tidak dapat membaiki pepijat hulu itu, tetapi ia mengesan dan menandai instance **Failed** dengan sebab yang boleh diambil tindakan ("tiada hasil backtest untuk julat terpilih…") daripada jejan tumpukan kasar. Pilih julat tarikh yang lebih luas yang mempunyai data pasaran tersedia dan cuba semula.
:::

## Instance detail page

Membuka instance (`/instance/{id}`) menunjukkan statusnya yang langsung, log dan — untuk backtest — lengkung ekuiti. **Tajuk tab pelayar** mencerminkan instance tertentu (**nama cBot · jenis · simbol**, cth. `TrendBot · Backtest · EURUSD`) jadi tab lari hidup dan tab backtest dapat dibezakan sekilas. Lari dan backtest cBot yang sama dijejaki sebagai **lineage** yang berbeza (id lineage stabil dibawa merentasi peralihan keadaan), jadi halaman mengikuti tepat satu instance dan tidak pernah mencampur data lari dengan backtest.

## Instance lifecycle controls

Setiap baris instance (dan halaman butirannya) mempunyai kawalan yang betul keadaan. Instance yang **aktif** menunjukkan **Stop**; yang **terminal** (Stopped / Completed / Failed) menunjukkan **Start (▶)** untuk melancarkan semula dengan cBot, akaun, simbol, timeframe, set parameter dan imej yang sama (lari dimulai semula sebagai lari, backtest sebagai backtest). Mengklik Stop menunjukkan notis "Stopping…" dan melumpuhkan ikon sehingga ia menyelesaikan, dan lari yang baru dibuat muncul dalam senarai serta-merta — tiada pemuatan semula halaman.

Log konsol adalah **bertahan apabila instance ditamatkan** — untuk lari (pada Stop) dan untuk **backtest** (selepas penyelesaian) sama seperti — jadi log lari terakhir tetap boleh dilihat pada halaman butiran dan, melalui bar alat log, **disalin ke papan klip** (Ikon Salin log) atau **dimuat turun** (Ikon Muat turun log) walaupun selepas kontena hilang. Kedua-duanya bertindak pada log konsol penuh instance, bukan hanya ekor pada skrin.

`.algo` yang **dimuat naik** tidak pernah dibina di sini, jadi lajur **Last Build** nya pada halaman cBots ditinggalkan kosong (ia menunjukkan waktu pembinaan hanya untuk cBot yang anda bina dalam pelayar).

## Edit & re-run a stopped instance

Instance yang **dihentikan** (jalankan atau backtest) mempunyai kawalan **Edit** — ikon pada barisnya dalam senarai **dan** di sebelah Start/Stop pada halaman butirannya — yang membuka dialog **prefilled** dengan konfigurasinya yang sekarang. Anda boleh mengubah **akaun perdagangan, simbol, timeframe, set parameter dan tag imej** (dan, untuk backtest, **tetingkap dan semua pengaturan backtest** di atas), kemudian **Save & start** melancarkan semula dengan tetapan baharu (menggantikan instance yang dihentikan). Kawalan **dilumpuhkan semasa instance aktif** — hanya instance yang dihentikan boleh disunting.

## Run from the code editor

Mengklik **Run** dalam editor kod membuka dialog daripada memicu lari buta yang dikodkan keras:

- **Trading account** (diperlukan) — akaun cTrader yang disambungkan oleh cBot.
- **Parameter set** (pilihan) — pilih set yang sedia ada, atau tinggalkan kosong untuk menjalankan dengan **nilai parameter lalai** cBot. Butang **+** di sebelah pemilih membuat set parameter baharu dalam talian (lihat di bawah) dan memilihnya.
- **Symbol / Timeframe** lalai kepada `EURUSD` / `h1` dan boleh diubah; **Cancel** atau **Run**.

Pada **Run** editor menyimpan + membina sumber semasa, memulakan instance pada akaun yang dipilih dengan parameter yang dipilih, kemudian mengekor log kontena langsung. (Aliran log memajukan kuki auth pengguna yang ditandatangani masuk ke hab SignalR `/hubs/logs`, jadi ia bersambung daripada gagal dengan `Invalid negotiation response received`.)

## Parameter sets

**Parameter set** ialah set parameter pengguna yang dinamakan, boleh digunakan semula yang disimpan sebagai objek JSON rata yang memetakan setiap nama parameter kepada nilai skalar, cth. `{"Period": 14, "Label": "trend"}`. Pada masa lari/backtest ia diubah menjadi fail `params.cbotset` cTrader (`{ "Parameters": { … } }`). Anda boleh membuat/menyunting set sebagai JSON kasar daripada dialog **Parameter sets** cBot atau dalam talian daripada dialog Run.

Setiap set parameter **kepunyaan cBot**: dialog New Parameter Set menyenaraikan semua cBot anda dan anda **mesti memilih satu** — penciptaan dihalang sehingga cBot dipilih. **Nama** set adalah **unik bagi setiap cBot**: membuat atau menamakan semula set kepada nama yang satu lagi set cBot yang sama sudah gunakan ditolak (ralat jelas dalam dialog, `409 Conflict` di API). Nama yang sama boleh digunakan semula pada **cBot yang berbeza**.

JSON **divalidasi** pada simpan: ia mestilah objek rata tunggal yang nilai-nilainya adalah semua skalar (string / nombor / bool). Akar bukan objek, array, objek bersarang, nilai `null`, atau JSON salah format ditolak (ralat jelas dalam dialog, `400 Bad Request` di API). Objek kosong `{}` dibenarkan dan bermaksud "tiada ganti".

## cTrader Console CLI notes

Backtest memerlukan `--data-mode` (lalai `m1`), tarikh sebagai `dd/MM/yyyy HH:mm`, dan argumen positional JSON `params.cbotset`; `run` tolak `--data-dir` (backtest sahaja). Lihat `ContainerCommandHelpers`.

## Nodes & scale

Kapasiti pelaksanaan skala dengan menambah agen nod (daftar sendiri + denyutan jantung). Lihat [node discovery](../operations/node-discovery.md) dan [scaling](../deployment/scaling.md).

## A trading account is required

Menjalankan atau backtest cBot memerlukan akaun perdagangan cTrader untuk disambungkan. Sehingga anda menambah satu di bawah **Trading accounts**, butang **Run New cBot** / **Backtest New cBot** dilumpuhkan (dengan tooltip) dan halaman menunjukkan gesaran yang menghubung ke persediaan akaun — anda tidak lagi mencecah ralat kasar `stream connect failed` daripada bot tanpa akaun.
