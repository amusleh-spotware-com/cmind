---
description: "Bina, jalankan, backtest cTrader cBots (C# dan Python, kedua-duanya .NET) dari editor Monaco dalam pelayar, jalankan pada imej rasmi ghcr.io/spotware/ctrader-console."
---

# Bina & backtest cBots

Bina, jalankan, backtest cTrader cBots (C# **dan** Python, kedua-duanya .NET) dari editor Monaco dalam pelayar, jalankan pada imej rasmi `ghcr.io/spotware/ctrader-console`.

## Bina

- Laman **Builder** menghos editor Monaco; `CBotBuilder` mengkompil projek dengan `dotnet build` **dalam kontena yang boleh dibuang** (`AppOptions.BuildImage`, direktori kerja di-bind-mount pada `/work`), supaya sasaran MSBuild pengguna yang tidak dipercayai tidak dapat menjangkau hos. NuGet restore disimpan dalam cache merentasi binaan melalui volume bersama. Hos web memerlukan akses soket Docker.
- Templat pemula C# + Python berada dalam `src/Nodes/Builder/Templates/`.

## Jalankan & backtest

- **Instances** = hierarki keadaan TPH (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/`Running`/`Stopping`/`Stopped`/`Failed`). Peralihan menggantikan entiti (perubahan id), id kontena dibawa ke hadapan.
- `NodeScheduler` memilih nod yang paling sedikit dimuatkan yang layak; `ContainerDispatcherFactory` mengarahkan ke ejen HTTP nod jauh atau pengguna Docker tempatan.
- Penukul penyiapan menyelaraskan kontena yang sudah keluar (kontena backtest keluar sendiri melalui `--exit-on-stop`); laporan hadir → siap (simpan `ReportJson`), hilang → gagal.
- Log kontena langsung mengalir ke pelayar melalui SignalR; keluk ekuiti backtest disurih dari laporan + dicartakan.

## Data pasaran backtest disimpan dalam cache bagi setiap akaun

Konsol cTrader memuat turun data kutu/bar bersejarah ke dalam `--data-dir`-nya. Direktori itu adalah **cache stabil dan tekal yang disusun kunci pada akaun perdagangan** (nombor akaun-nya) — di-bind-mount dari cakera nod pada laluan kontena-nya sendiri (`/mnt/data`), **mount berasingan, tidak bersarang** daripada direktori kerja per-instance. Jadi setiap backtest pada akaun yang sama **menggunakan semula** data yang sudah dimuat turun dan bukannya memuat turun semula ia pada setiap larian. (Lebih awal direktori data berada di bawah direktori kerja per-instance, yang id-nya berubah setiap larian, yang memaksa muat turun baru setiap backtest.) Direktori kerja per-instance yang sementara masih memegang algo, param, kata laluan dan laporan; cache data bersama dikira dalam penggunaan data-backtest nod dan dibersihkan oleh tindakan pembersih nod.

## Tetapan backtest

Dialog **Backtest** mendedahkan setiap tetapan yang diterima CLI backtest konsol cTrader, supaya anda tidak perlu menyentuh baris arahan:

- **Dari / Hingga** — tetingkap backtest (`--start` / `--end`).
- **Mod data** — `m1` (bar 1-minit) atau `tick` (`--data-mode`).
- **Baki permulaan** — lalai kepada `10000` (`--balance`). Baki **0 tidak menempatkan perdagangan dan membuat cTrader memancarkan laporan kosong yang kemudian ranap pada-nya** ("Mesej dijangka"), jadi baki bukan sifar sentiasa dihantar.
- **Komisen** dan **Hamparan** (`--commission` / `--spread`, hamparan dalam pip).
- **Pilihan lanjutan** — kotak bentuk bebas `name=value` setiap baris untuk sebarang pilihan backtest lain yang cTrader sokong (mis. `applyCommissionAutomatically=true`); setiap baris menjadi hujah CLI `--name value`.

## Laman butiran instance

Membuka instance (`/instance/{id}`) menunjukkan statusnya yang langsung, log dan — untuk backtest — lengkung ekuiti. Tajuk **tab pelayar** mencerminkan instance tertentu (**nama cBot · jenis · simbol**, cth. `TrendBot · Backtest · EURUSD`) supaya tab larian langsung dan tab backtest boleh dibezakan sepintas lalu. Larian dan backtest cBot yang sama dijejak sebagai **keturunan** yang berbeza (id keturunan stabil yang dibawa merentas peralihan keadaan), supaya laman mengikut tepat satu instance dan tidak pernah mencampurkan data larian dengan data backtest.

## Kawalan kitaran hayat instance

Setiap baris instance (dan laman butirnya) mempunyai kawalan yang betul keadaan. Instance **aktif** menunjukkan **Henti**; satu yang **terminal** (Dihenti / Siap / Gagal) menunjukkan **Mulai (▶)** untuk melancarkannya semula dengan cBot, akaun, simbol, kerangka masa, set parameter dan imej yang sama (larian dimulakan semula sebagai larian, backtest sebagai backtest). Mengklik Henti menunjukkan notis "Menghenti…" dan melumpuhkan ikon sehingga ia diselesaikan, dan larian baru yang dibuat muncul dalam senarai serta-merta — tanpa muat semula halaman.

Log konsol **disimpan apabila instance ditamatkan** — untuk larian (pada Henti) dan untuk **backtest** (pada penyiapan) seumpama — supaya log larian terakhir tetap boleh dilihat pada laman butiran dan, melalui toolbar log, **disalin ke papan klip** (ikon Log Salin) atau **dimuat turun** (ikon Muat Turun Log) walaupun selepas kontena hilang. Kedua-duanya bertindak pada log konsol penuh instance, bukan hanya ekor di skrin.

`.algo` yang **dimuat naik** tidak pernah dibina di sini, jadi lajur **Binaan Terakhir**-nya pada halaman cBots dibiarkan kosong (ia menunjukkan masa binaan hanya untuk cBots yang anda bina dalam pelayar).

## Edit & jalankan semula instance yang dihenti

Instance **yang dihenti** (larian atau backtest) mempunyai kawalan **Edit** — ikon pada baris-nya dalam senarai **dan** di sebelah Mulai/Henti pada laman butirnya — yang membuka dialog **diisi awal** dengan konfigurasi semasanya. Anda boleh mengubah **akaun perdagangan, simbol, kerangka masa, set parameter dan teg imej** (dan, untuk backtest, **tetingkap dan semua tetapan backtest** di atas), kemudian **Simpan & mulai** melancarkannya semula dengan tetapan baru (menggantikan instance yang dihenti). Kawalan itu **dilumpuhkan semasa instance aktif** — hanya instance yang dihenti boleh diedit.

## Jalankan dari editor kod

Mengklik **Jalankan** dalam editor kod membuka dialog dan bukannya memicu larian buta yang dikodkan keras:

- **Akaun perdagangan** (diperlukan) — akaun cTrader yang cBot bersambung dengannya.
- **Set parameter** (pilihan) — pilih set sedia ada, atau biarkan kosong untuk jalankan dengan **nilai parameter lalai** cBot. Butang **+** di sebelah pemilih membuat set parameter baru dalam baris (lihat di bawah) dan memilihnya.
- **Simbol / Kerangka masa** lalai kepada `EURUSD` / `h1` dan boleh diubah; **Batal** atau **Jalankan**.

Pada **Jalankan** editor menyimpan + menyusun sumber semasa, memulakan instance pada akaun yang dipilih dengan parameter yang dipilih, kemudian mengikut log kontena langsung. (Aliran log memajukan kuki auth pengguna yang telah menandatangani ke hab SignalR `/hubs/logs`, supaya ia bersambung dan bukannya gagal dengan `Respons rundingan tidak sah diterima`.)

## Set parameter

Set **parameter** adalah set parameter cBot yang boleh digunakan semula dan dinamakan yang disimpan sebagai objek JSON rata yang memetakan setiap nama parameter kepada nilai skalar, cth. `{"Period": 14, "Label": "trend"}`. Pada masa larian/backtest ia ditukar kepada fail `params.cbotset` cTrader (`{ "Parameters": { … } }`). Anda boleh membuat/menyunting set sebagai JSON mentah dari dialog **Set parameter** cBot atau dalam baris dari dialog Jalankan.

Setiap set parameter **kepunyaan cBot**: dialog Set Parameter Baru menyenaraikan semua cBot anda dan anda **mesti memilih satu** — penciptaan disekat sehingga cBot dipilih. **Nama** set adalah unik bagi setiap cBot: mencipta atau menamakan semula set kepada nama yang sudah digunakan set lain cBot yang sama ditolak (ralat yang jelas dalam dialog, `409 Conflict` pada API). Nama yang sama boleh digunakan semula pada **cBot yang berbeza**.

JSON **disah** pada simpan: ia mesti menjadi objek rata tunggal yang nilai-nilainya adalah semua skalar (rentetan / nombor / bool). Akar bukan objek, tatasusunan, objek bertaraf, nilai `null`, atau JSON yang tidak terbentuk ditolak (ralat yang jelas dalam dialog, `400 Bad Request` pada API). Objek kosong `{}` dibenarkan dan bermaksud "tiada penggantian".

## Nota CLI Konsol cTrader

Backtest memerlukan `--data-mode` (lalai `m1`), tarikh sebagai `dd/MM/yyyy HH:mm`, dan hujah kedudukan JSON `params.cbotset`; `run` menolak `--data-dir` (backtest sahaja). Lihat `ContainerCommandHelpers`.

## Nod & skala

Kapasiti pelaksanaan berskala dengan menambah ejen nod (daftar sendiri + degup jantung). Lihat [penemuan nod](../operations/node-discovery.md) dan [penskalaan](../deployment/scaling.md).

## Akaun perdagangan diperlukan

Menjalankan atau membacktest cBot memerlukan akaun perdagangan cTrader untuk bersambung dengannya. Sehingga anda menambah satu di bawah **Akaun perdagangan**, butang **Jalankan cBot Baru** / **Backtest cBot Baru** dilumpuhkan (dengan tooltip) dan halaman menunjukkan gesaran yang memaut ke persediaan akaun — anda tidak lagi mendapat ralat mentah `stream connect failed` daripada bot tanpa akaun.
