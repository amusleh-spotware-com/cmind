---
description: "Bina, jalankan, backtest cBot cTrader (C# dan Python, kedua-duanya .NET) daripada Monaco IDE dalam pelayar, jalankan pada imej rasmi ghcr.io/spotware/ctrader-console."
---

# Build & backtest cBots

Bina, jalankan, backtest cBot cTrader (C# **dan** Python, kedua-duanya .NET) daripada Monaco
IDE dalam pelayar, jalankan pada imej rasmi `ghcr.io/spotware/ctrader-console`.

## Build

- Halaman **Builder** mengandungi editor Monaco; `CBotBuilder` mengkompil projek dengan
  `dotnet build` **dalam bekas yang mudah dibuang** (`AppOptions.BuildImage`, direktori kerja bind-mount
  di `/work`), supaya sasaran MSBuild pengguna tidak dipercayai tidak dapat menjangkau hos. Pemulihan NuGet
  disimpan dalam cache merentasi binaan melalui volum bersama. Hos web memerlukan akses soket Docker.
- Templat pemula C# + Python terletak di `src/Nodes/Builder/Templates/`.

## Run & backtest

- **Instance** = hierarki keadaan TPH (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/
  `Running`/`Stopping`/`Stopped`/`Failed`). Peralihan menggantikan entiti (perubahan id),
  id bekas dibawa.
- `NodeScheduler` memilih nod yang paling sedikit dibebani yang layak; `ContainerDispatcherFactory` mengarahkan ke
  ejen HTTP nod jauh atau penghala Docker setempat.
- Penyapu penyiapan menyelaraskan bekas yang telah keluar (bekas backtest keluar sendiri melalui
  `--exit-on-stop`); laporan hadir → selesai (simpan `ReportJson`), hilang → gagal.
- Log bekas langsung dialirkan ke pelayar melalui SignalR; lengkung ekuiti backtest diperhitungkan daripada
  laporan + dicarta.

## Backtest market data is cached per account

cTrader Console memuat turun data tanda/bar bersejarah ke dalam `--data-dir`-nya. Direktori itu adalah
**cache yang stabil dan berterusan yang dikunci pada akaun perdagangan** (nombor akaunnya) — bind-mount daripada
disk nod di laluan bekas yang dimilikinya (`/mnt/data`), **mount yang berasingan dan tidak bersarang** daripada
direktori kerja per-instance yang mudah hilang. Jadi setiap backtest pada akaun yang sama **menggunakan semula** data yang telah dimuat turun
daripada memuat turun semula setiap larian. (Dahulu
direktori data tinggal di bawah direktori kerja per-instance, yang id-nya berubah setiap larian, yang memaksa muat turun
segar setiap backtest.) Direktori kerja per-instance yang mudah hilang masih memegang algo, param, kata laluan
dan laporan; cache data bersama dikira dalam penggunaan data-backtest nod dan dipadam oleh
tindakan nod-bersih.

## Backtest settings

Dialog **Backtest** mendedahkan setiap tetapan yang diterima oleh CLI backtest cTrader Console, supaya anda tidak pernah
perlu menyentuh baris arahan:

- **From / To** — tetingkap backtest (`--start` / `--end`).
- **Data mode** — salah satu daripada tiga mod cTrader (`--data-mode`): **Tick data** (`tick`, tepat),
  **m1 bars** (`m1`, pantas), atau **Open prices only** (`open`, paling pantas).
- **Starting balance** — lalai kepada `10000` (`--balance`). **Baki 0 tidak melakukan perdagangan dan menyebabkan
  cTrader mengeluarkan laporan kosong yang kemudian mengalami kemalangan** ("Message expected"), jadi baki bukan sifar sentiasa
  dihantar.
- **Commission** dan **Spread** — `--commission` / `--spread` (spread dalam pips).
- **Data file** (pilihan) — laluan sampingan nod ke fail data bersejarah (`--data-file`); biarkan kosong untuk
  menggunakan data yang dimuat turun/disimpan dalam cache.
- **Expose environment variables** — togol yang meneruskan pembolehubah persekitaran hos kepada cBot
  (bendera `--environment-variables`).

## Instance detail page

Membuka instance (`/instance/{id}`) menunjukkan statusnya yang langsung, log dan — untuk backtest — lengkung ekuiti.
Tajuk **tab pelayar** mencerminkan instance tertentu (**nama cBot · jenis · simbol**, contohnya
`TrendBot · Backtest · EURUSD`) supaya tab larian langsung dan tab backtest boleh dibezakan sekilas.
Larian dan backtest cBot yang sama dilacak sebagai **lineage** yang berbeza (id lineage yang stabil dibawa
merentasi peralihan keadaan), supaya halaman mengikuti persis satu instance dan tidak pernah mencampur data larian dengan
backtest.

## Instance lifecycle controls

Setiap baris instance (dan halaman detailnya) mempunyai kawalan yang sesuai dengan keadaan. Instance yang **aktif** menunjukkan
**Stop**; satu yang **terminal** (Stopped / Completed / Failed) menunjukkan **Start (▶)** untuk melancarkannya semula dengan
cBot yang sama, akaun, simbol, kerangka masa, set parameter dan imej (larian dimulai semula sebagai larian, backtest
sebagai backtest). Mengklik Stop menunjukkan notis "Stopping…" dan melumpuhkan ikon sehingga ia
diselesaikan, dan larian yang baru dibuat muncul dalam senarai serta-merta — tanpa muat ulang halaman.

Log konsol **berterusan apabila instance menamatkan** — untuk larian (di Stop) dan untuk
**backtest** (semasa siap) begitu juga — supaya log larian terakhir kekal boleh dilihat di halaman detail dan,
melalui bar alat log, **disalin ke papan klip** (ikon Salin log) atau **dimuat turun** (ikon Muat turun log)
walaupun selepas bekas hilang. Kedua-duanya bertindak pada log konsol penuh instance, bukan hanya
ekor pada skrin.

`.algo` yang **dimuat naik** tidak pernah dibina di sini, supaya lajur **Last Build** di halaman cBot dibiarkan
kosong (ia menunjukkan masa binaan hanya untuk cBot yang anda bina dalam pelayar).

## Edit & re-run a stopped instance

Instance **berhenti** (larian atau backtest) mempunyai kawalan **Edit** — ikon di baris-nya dalam senarai **dan**
di sebelah Start/Stop di halaman detailnya — yang membuka dialog **diisi sebelumnya** dengan konfigurasi semasanya.
Anda boleh mengubah **akaun perdagangan, simbol, kerangka masa, set parameter dan teg imej** (dan, untuk
backtest, **tetingkap dan semua tetapan backtest** di atas), kemudian **Simpan & mulai** melancarkannya semula dengan
tetapan baharu (menggantikan instance yang berhenti). Kawalan ini **dilumpuhkan sementara instance aktif** —
hanya instance yang berhenti boleh disunting.

## Run from the code editor

Mengklik **Run** dalam editor kod membuka dialog daripada menembak larian membuta yang dikodkan keras:

- **Trading account** (diperlukan) — akaun cTrader yang disambungkan oleh cBot.
- **Parameter set** (pilihan) — pilih set sedia ada, atau biarkan kosong untuk menjalankan dengan
  **nilai parameter lalai** cBot. Butang **+** di sebelah pemilih membuat set parameter baharu
  dalam talian (lihat di bawah) dan memilihnya.
- **Symbol / Timeframe** lalai kepada `EURUSD` / `h1` dan boleh diubah; **Batal** atau **Jalankan**.

Pada **Jalankan** editor menyimpan + membina sumber semasa, memulakan instance pada akaun yang dipilih
dengan parameter yang dipilih, kemudian menyapu log bekas langsung. (Aliran log meneruskan kuki auth pengguna yang
masuk kepada hub SignalR `/hubs/logs`, supaya ia bersambung daripada gagal dengan
`Invalid negotiation response received`.)

## Parameter sets

**Parameter set** adalah set parameter cBot yang diubah namun boleh digunakan semula yang disimpan sebagai objek JSON rata
yang memetakan setiap nama parameter kepada nilai skalar, contohnya `{"Period": 14, "Label": "trend"}`. Pada
masa larian/backtest ia diubah menjadi fail cTrader `params.cbotset`
(`{ "Parameters": { … } }`). Anda boleh membuat/menyunting set sebagai JSON mentah daripada dialog **Parameter
sets** cBot atau dalam talian daripada dialog Run.

Setiap set parameter **tergolong dalam cBot**: dialog Set Parameter Baharu menyenaraikan semua cBot anda dan anda
**mesti memilih satu** — penciptaan disekat sehingga cBot dipilih. **Nama** set adalah **unik setiap cBot**:
membuat atau menamakan semula set kepada nama yang set lain cBot yang sama sudah gunakan ditolak (ralat jelas
dalam dialog, `409 Conflict` di API). Nama yang sama boleh digunakan semula pada **cBot yang berbeza**.

JSON adalah **disahkan** semasa simpan: ia mestilah objek rata tunggal yang nilai-nilainya semua skalar
(string / nombor / bool). Akar bukan objek, tatasusunan, objek bersarang, nilai `null`, atau JSON yang salah bentuk
ditolak (ralat jelas dalam dialog, `400 Bad Request` di API). Objek kosong `{}`
dibenarkan dan bermakna "tiada ganti tindih".

## cTrader Console CLI notes

Backtest memerlukan `--data-mode` (lalai `m1`), tarikh sebagai `dd/MM/yyyy HH:mm`, dan
JSON `params.cbotset` hujah kedudukan; `run` tolak `--data-dir` (backtest sahaja). Lihat
`ContainerCommandHelpers`.

## Nodes & scale

Kapasiti pelaksanaan berskala dengan menambah ejen nod (daftar sendiri + denyut nadi). Lihat
[penemuan nod](../operations/node-discovery.md) dan [penskalaan](../deployment/scaling.md).

## A trading account is required

Menjalankan atau melakukan backtest cBot memerlukan akaun perdagangan cTrader untuk disambungkan. Sehingga anda menambah satu di bawah
**Trading accounts**, butang **Run New cBot** / **Backtest New cBot** dilumpuhkan (dengan
tooltip) dan halaman menunjukkan gesaran yang menghubung ke persediaan akaun — anda tidak lagi mencampak ralat
`stream connect failed` daripada bot tanpa akaun.
