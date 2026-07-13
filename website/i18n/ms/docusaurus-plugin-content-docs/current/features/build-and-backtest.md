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
