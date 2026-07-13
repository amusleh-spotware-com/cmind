---
title: 0004 — CBotBuilder berjalan pada hos web dalam bekas pasir
description: Mengapa binaan cBot yang tidak dipercayai berlaku pada hos web dalam bekas SDK pakai buang daripada pada nod.
---

# 0004 — `CBotBuilder` berjalan pada hos web dalam bekas pasir

## Konteks

Membina cBot pengguna bermakna menjalankan **MSBuild tidak dipercayai** — kod sewenang-wenang pada masa binaan (sasaran, penjana sumber, skrip pemulihan). Ia memerlukan soket Docker untuk memutar bekas SDK. Nod menjalankan bekas dagangan dan tidak seharusnya juga memegang keistimewaan binaan.

## Keputusan

`CBotBuilder` berjalan **pada hos web** (yang sudah mempunyai soket Docker), dalam **bekas SDK pakai buang** dengan:

- direktori `/work` terikat-pasang (hanya input/output binaan, bukan sistem fail hos);
- volum `app-nuget-cache` bersama untuk prestasi pemulihan;
- tiada akses rangkaian hos luar apa yang pemulihan perlukan.

Jadi MSBuild tidak dipercayai tidak dapat mencapai sistem fail atau rangkaian hos. Bekas lari/backtest, sebaliknya, berjalan pada nod dipilih oleh `NodeScheduler`.

## Akibat

- Keistimewaan binaan (soket Docker) dihadkan kepada hos web; nod hanya menjalankan imej dagangan yang dibenarkan.
- Setiap binaan terpencil dalam bekas boleh buang — binaan berniat jahat tidak boleh bertahan atau keluar.
- Hos web mesti mempunyai soket Docker tersedia; ini adalah keperluan penempatan, bukan pilihan.
