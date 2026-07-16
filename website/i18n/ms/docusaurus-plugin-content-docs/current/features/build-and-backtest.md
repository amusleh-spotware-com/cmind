---
description: "Binaan, jalankan, ujian belakang cBots cTrader (C# dan Python, kedua-duanya .NET) daripada Monaco IDE dalam pelayar, jalankan pada imej ghcr.io/spotware/ctrader-console rasmi."
---

# Binaan & ujian belakang cBots

Binaan, jalankan, ujian belakang cBots cTrader (C# **dan** Python, kedua-duanya .NET) daripada Monaco IDE dalam pelayar, jalankan pada imej `ghcr.io/spotware/ctrader-console` rasmi.

## Binaan

- Halaman **Pembina** hos editor Monaco; `CBotBuilder` mengkompil projek dengan
  `dotnet build` **dalam bekas yang boleh dilupakan** (`AppOptions.BuildImage`, dir kerja diikat
  di `/work`), jadi target MSBuild pengguna tidak dipercayai tidak mencapai hos. Cache pemulihan NuGet
  merentasi pembinaan melalui volum bersama. Hos web memerlukan akses soket Docker.
- Templat permulaan C# + Python hidup dalam `src/Nodes/Builder/Templates/`.

## Jalankan & ujian belakang

- **Contoh** = hierarki keadaan TPH (`Run`/`Backtest` × `Pending`/`Scheduled`/`Starting`/
  `Running`/`Stopping`/`Stopped`/`Failed`). Peralihan menggantikan entiti (perubahan id),
  id bekas dibawa.
- `NodeScheduler` pilih nod yang paling sedikit dimuatkan yang layak; `ContainerDispatcherFactory` laluan ke
  ejen HTTP nod jauh atau penghantaran Docker tempatan.
- Pengundi penyelesaian menyerasikan bekas keluar (bekas ujian belakang berhenti sendiri melalui
  `--exit-on-stop`); laporan hadir → selesai (simpan `ReportJson`), hilang → gagal.
- Log bekas hidup aliran ke pelayar melalui SignalR; lengkung ekuiti ujian belakang diuraikan daripada
  laporan + carta.

## Catatan CLI Konsol cTrader

Ujian belakang memerlukan `--data-mode` (lalai `m1`), tarikh sebagai `dd/MM/yyyy HH:mm`, dan
`params.cbotset` JSON hujah kedudukan; `jalankan` tolak `--data-dir` (ujian belakang sahaja). Lihat
`ContainerCommandHelpers`.

## Nod & skala

Kapasiti pelaksanaan berskalakan dengan menambah ejen nod (daftar sendiri + denyut nadi). Lihat
[penemuan nod](../operations/node-discovery.md) dan [penskalaan](../deployment/scaling.md).

## Menjalankan dari editor kod

Mengklik **Jalankan** dalam editor kod membuka dialog dan bukannya mencetuskan larian buta yang dikodkan tetap:

- **Akaun dagangan** (wajib) — akaun cTrader yang cBot sambung.
- **Set parameter** (pilihan) — pilih set sedia ada, atau biarkan kosong untuk berjalan dengan **nilai parameter lalai** cBot. Butang **+** di sebelah pemilih mencipta set parameter baharu secara sebaris (lihat di bawah) dan memilihnya.
- **Simbol / Kerangka masa** lalai kepada `EURUSD` / `h1` dan boleh diubah; **Batal** atau **Jalankan**.

Semasa **Jalankan**, editor menyimpan dan membina kod sumber semasa, memulakan instans pada akaun yang dipilih dengan parameter yang dipilih, kemudian mengekori log kontena secara langsung. (Aliran log memajukan kuki pengesahan pengguna yang log masuk ke hab SignalR `/hubs/logs`, jadi ia bersambung dan bukannya gagal dengan `Invalid negotiation response received`.)

## Set parameter

**Set parameter** ialah set gantian parameter cBot yang dinamakan dan boleh guna semula, disimpan sebagai objek JSON rata yang memetakan setiap nama parameter kepada nilai skalar, cth. `{"Period": 14, "Label": "trend"}`. Semasa jalankan/backtest, ia ditukar kepada fail cTrader `params.cbotset` (`{ "Parameters": { … } }`). Anda boleh mencipta/menyunting set sebagai JSON mentah daripada dialog **Set parameter** cBot atau secara sebaris daripada dialog Jalankan.

JSON **disahkan** semasa menyimpan: ia mesti satu objek rata tunggal yang semua nilainya skalar (rentetan / nombor / bool). Akar bukan objek, tatasusunan, objek bersarang, nilai `null`, atau JSON tidak sah ditolak (ralat jelas dalam dialog, `400 Bad Request` di API). Objek kosong `{}` dibenarkan dan bermaksud "tiada gantian".

## Kawalan kitaran hayat instans

Setiap baris instans (dan halaman butirannya) mempunyai kawalan yang betul mengikut keadaan. Instans **aktif** memaparkan **Hentikan**; instans **terminal** (Dihentikan / Selesai / Gagal) memaparkan **Mula (▶)** untuk melancarkannya semula dengan cBot, akaun, simbol, kerangka masa, set parameter dan imej yang sama (larian dimulakan semula sebagai larian, backtest sebagai backtest). Mengklik Hentikan memaparkan pemberitahuan "Menghentikan…" dan melumpuhkan ikon sehingga selesai; larian yang baharu dicipta muncul dalam senarai serta-merta — tanpa memuat semula halaman.

Log konsol **dikekalkan apabila instans tamat** — untuk larian (semasa dihentikan) dan untuk **backtest** (semasa selesai) — jadi log larian terakhir kekal boleh dilihat pada halaman butiran dan boleh dimuat turun melalui ikon **Muat turun log**, walaupun selepas kontena hilang.
