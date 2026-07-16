---
description: "Bina, jalankan, backtest cTrader cBots (C# dan Python, kedua-duanya .NET) daripada Monaco IDE dalam pelayar, jalankan pada imej rasmi ghcr.io/spotware/ctrader-console."
---

# Build & backtest cBots

Bina, jalankan, backtest cTrader cBots (C# **dan** Python, kedua-duanya .NET) daripada Monaco
IDE dalam pelayar, jalankan pada imej rasmi `ghcr.io/spotware/ctrader-console`.

## Build

- Halaman **Builder** mengoskan editor Monaco; `CBotBuilder` menyusun projek dengan
  `dotnet build` **dalam bekas yang dapat dibuang** (`AppOptions.BuildImage`, direktori kerja diikat pada
  `/work`), supaya sasaran MSBuild pengguna yang tidak dipercayai tidak sampai ke hos. Pemulihan NuGet disimpan cache
  merentasi binaan melalui volum bersama. Hos web memerlukan akses soket Docker.
- Templat permulaan C# + Python terletak di `src/Nodes/Builder/Templates/`.

## Run & backtest

- **Instances** = hierarki keadaan TPH (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/
  `Running`/`Stopping`/`Stopped`/`Failed`). Peralihan menggantikan entiti (perubahan id),
  id bekas dibawa ke hadapan.
- `NodeScheduler` memilih nod yang paling ringan; `ContainerDispatcherFactory` mengarahkan ke
  ejen HTTP nod jauh atau penghala Docker tempatan.
- Pengumpul siap menyamankan bekas yang telah keluar (bekas backtest keluar sendiri melalui
  `--exit-on-stop`); laporan hadir → siap (simpan `ReportJson`), hilang → gagal.
- Log bekas langsung mengalir ke pelayar melalui SignalR; lengkung ekuiti backtest diuraikan daripada
  laporan + dicarta.

## Backtest market data is cached per account

cTrader Console memuat turun data pembukaan tick/bar bersejarah ke dalam `--data-dir` miliknya. Direktori itu ialah
**cache stabil, berterusan yang ditandakan pada akaun perdagangan** (nomor akaunnya) — diikat daripada
disk nod di laluan bekas miliknya (`/mnt/data`), **pemasangan yang berasingan, tidak bersarang** daripada
direktori kerja setiap instans. Jadi setiap backtest pada akaun yang sama **menggunakan semula** data yang telah dimuat turun
dan bukannya memuat turunnya semula setiap perlakuan. (Lebih awal, direktori data
tinggal di bawah direktori kerja setiap instans, yang id-nya berubah setiap larian, yang memaksa
pemuat turun segar setiap backtest.) Direktori kerja ephemeral setiap instans masih memegang algo, param, kata laluan
dan laporan; cache data bersama dikira dalam penggunaan data-backtest nod dan dibersihkan oleh
tindakan pembersihan nod.

## Backtest settings

Dialog **Backtest** mendedahkan tetapan backtest cTrader Console yang boleh diubah suai pengguna, jadi anda tidak perlu
menyentuh baris arahan:

- **Symbol / Timeframe** — timeframe ialah **menu bawah setiap tempoh cTrader** (`t1`…`t1000`,
  `m1`…`m45`, `h1`…`h12`, `D1`/`D2`/`D3`, `W1`, `Month1`, dan tempoh Renko/Range/Heikin), dalam
  huruf besar kanonik konsol, jadi anda sentiasa memilih `--period` yang sah.
- **From / To** — tetingkap backtest (`--start` / `--end`).
- **Data mode** — salah satu daripada tiga mod cTrader (`--data-mode`): **Tick data** (`tick`, tepat),
  **m1 bars** (`m1`, cepat), atau **Open prices only** (`open`, paling cepat).
- **Starting balance** — lalai kepada `10000` (`--balance`). **Baki 0 tidak membuat dagangan dan menyebabkan
  cTrader mengeluarkan laporan kosong yang kemudian ranap** ("Mesej dijangkakan"), jadi baki bukan sifar
  sentiasa dihantar.
- **Commission** — `--commission`.
- **Spread** — `--spread`, **medan angka dalam pip yang tidak boleh pergi di bawah 0**. Ia **tersembunyi dalam Tick
  data mode**, di mana cTrader memperoleh penyebaran daripada data tick itu sendiri (tiada `--spread` dihantar).

Direktori data (`--data-file` / `--data-dir`) diurus oleh aplikasi itu sendiri (cache setiap akaun, lihat
atas), tidak didedahkan dalam dialog.

:::note cTrader crashes on an empty backtest
Jika backtest menghasilkan **tiada hasil** — tiada dagangan, atau tiada data pasaran untuk tarikh/simbol yang dipilih —
penulis laporan cTrader Console melempar `Message expected` dan keluar tanpa laporan. Aplikasi tidak boleh
membetulkan pepijat huliran itu, tetapi ia mengesannya dan menandakan instans **Gagal** dengan sebab yang boleh ditindakan
("tiada hasil backtest untuk julat yang dipilih…") dan bukannya jejak timbunan mentah. Pilih julat tarikh yang lebih luas
yang mempunyai data pasaran yang tersedia dan cuba semula.
:::

## Instance detail page

Membuka instans (`/instance/{id}`) menunjukkan statusnya yang langsung, log dan — untuk backtest — lengkung
ekuiti. **Tajuk tab pelayar** mencerminkan instans tertentu (**nama cBot · jenis · simbol**, cth.
`TrendBot · Backtest · EURUSD`) jadi tab perlakuan langsung dan tab backtest dapat dibezakan pada
pandangan pertama. Perlakuan dan backtest cBot yang sama dijejaki sebagai **keturunan** yang berbeza (id keturunan stabil dibawa
merentasi peralihan keadaan), jadi halaman mengikuti tepat satu instans dan tidak pernah mencampur data perlakuan dengan
backtest.

## Instance lifecycle controls

Setiap baris instans (dan halamannya detail) mempunyai kawalan yang tepat keadaan. Instans **aktif** menunjukkan
**Berhenti**; satu yang **terminal** (Stopped / Completed / Failed) menunjukkan **Start (▶)** untuk memulakan semulanya dengan
cBot yang sama, akaun, simbol, timeframe, set parameter dan imej (perlakuan bermula semula sebagai perlakuan, backtest sebagai
backtest). Mengklik Berhenti menunjukkan notis "Menghenti…" dan melumpuhkan ikon sehingga ia diselesaikan, dan perlakuan baru yang dibuat
muncul dalam senarai dengan segera — tiada muat semula halaman.

Log konsol **bertahan apabila instans ditamatkan** — untuk perlakuan (semasa Berhenti) dan untuk
**backtest** (semasa siap) sama-sama — jadi log perlakuan terakhir tetap boleh dilihat pada halaman detail dan,
melalui bar alat log, **disalin ke papan klip** (Ikon Salin log) atau **dimuat turun** (Ikon Muat turun log)
walaupun selepas bekas hilang. Kedua-duanya bertindak pada log konsol penuh instans, bukan hanya
ekor skrin.

**Backtest yang siap** juga mengabadikan **laporan cTrader** miliknya dalam kedua-dua format — yang mentah
**JSON** (yang sama yang lengkung ekuiti dan analisis AI baca) dan laporan **HTML** lengkap. Kedua-duanya
boleh dimuat turun daripada baris backtest **dan** halaman detail melalui ikon berdedikasi. Hanya **laporan perlakuan terakhir**
disimpan, dan ikon itu **dilumpuhkan** untuk sebarang backtest yang tidak bermula, berjalan atau
gagal (dan tidak pernah ditunjukkan untuk instans perlakuan) — hanya backtest yang siap mempunyai laporan untuk dimuat turun.

`.algo` yang **dimuat naik** tidak pernah dibina di sini, jadi lajur **Last Build** miliknya di halaman cBots dibiarkan
kosong (ia menunjukkan masa bina hanya untuk cBots yang anda bina dalam pelayar).

## Edit & re-run a stopped instance

Instans yang **berhenti** (perlakuan atau backtest) mempunyai kawalan **Edit** — ikon di baris miliknya dalam senarai **dan**
di sebelah Start/Stop pada halamannya detail — yang membuka dialog **yang telah diisi sebelumnya** dengan konfigurasi semasa miliknya.
Anda boleh mengubah **akaun perdagangan, simbol, timeframe, set parameter dan tag imej** (dan, untuk backtest,
**tetingkap dan semua tetapan backtest** atas), kemudian **Simpan & mulai** memulakan semulanya dengan
tetapan baru (menggantikan instans yang berhenti). Kawalan itu **dilumpuhkan semasa instans aktif** —
hanya instans yang berhenti boleh disunting.

## Run from the code editor

Mengklik **Run** dalam editor kod membuka dialog dan bukannya menembakkan perlakuan buta, berkod keras:

- **Trading account** (wajib) — akaun cTrader yang disambungkan cBot.
- **Parameter set** (pilihan) — pilih set sedia ada, atau biarkan kosong untuk menjalankan dengan **nilai parameter lalai** cBot.
  Butang **+** di sebelah pemilih membuat set parameter baharu dalam sebaris (lihat di bawah) dan memilihnya.
- **Symbol / Timeframe** lalai kepada `EURUSD` / `h1` dan boleh diubah; **Batalkan** atau **Jalankan**.

Pada **Jalankan** editor menyimpan + membina sumber semasa, memulakan instans pada akaun yang dipilih
dengan parameter yang dipilih, kemudian mengekor log bekas langsung. (Aliran log memajukan kuki auth pengguna yang ditandatangani masuk
kepada hab SignalR `/hubs/logs`, jadi ia disambungkan dan bukannya gagal dengan
`Invalid negotiation response received`.)

## Parameter sets

**Set parameter** ialah set terpilih, boleh digunakan semula bagi pengguna terbalik parameter cBot yang disimpan sebagai objek JSON rata
yang memetakan setiap nama parameter kepada nilai skalar, cth `{"Period": 14, "Label": "trend"}`. Pada
masa perlakuan/backtest ia diubah menjadi fail cTrader `params.cbotset`
(`{ "Parameters": { … } }`). Anda boleh membuat/menyunting set sebagai JSON mentah daripada dialog **Parameter
sets** cBot atau dalam sebaris daripada dialog Jalankan.

Setiap set parameter **kepunyaan cBot**: dialog Set Parameter Baru menyenaraikan semua cBots anda dan anda
**mesti memilih satu** — penciptaan disekat sehingga cBot dipilih. Nama set **adalah unik setiap cBot**:
membuat atau menamakan semula set kepada nama yang set lain dari cBot yang sama telah gunakan ditolak (ralat jelas
dalam dialog, `409 Conflict` di API). Nama yang sama boleh digunakan semula pada **cBot yang berbeza**.

JSON itu **disahkan** semasa disimpan: ia mesti menjadi objek rata tunggal yang nilainya semua skalar
(rentetan / nombor / bool). Punca bukan objek, tatasusunan, objek bersarang, nilai `null`, atau JSON yang tidak terbentuk
ditolak (ralat jelas dalam dialog, `400 Bad Request` di API). Objek kosong `{}`
dibenarkan dan bermakna "tiada penggantian".

## cTrader Console CLI notes

Backtest memerlukan `--data-mode` (lalai `m1`), tarikh sebagai `dd/MM/yyyy HH:mm`, dan
argumen posisi JSON `params.cbotset`; `run` menolak `--data-dir` (backtest sahaja). Lihat
`ContainerCommandHelpers`.

## Nodes & scale

Kapasiti pelaksanaan skala dengan menambah ejen nod (daftar sendiri + debaran jantung). Lihat
[node discovery](../operations/node-discovery.md) dan [scaling](../deployment/scaling.md).

## A trading account is required

Menjalankan atau melakukan backtest cBot memerlukan akaun perdagangan cTrader untuk disambungkan. Sehingga anda menambah satu di bawah
**Trading accounts**, butang **Run New cBot** / **Backtest New cBot** dilumpuhkan (dengan
petua alat) dan halaman menunjukkan gesaran yang menghubungkan kepada persediaan akaun — anda tidak lagi mencecah
ralat `stream connect failed` mentah daripada bot tanpa akaun.
