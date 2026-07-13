---
title: 0004 — CBotBuilder berjalan di web host dalam container sandbox
description: Mengapa build cBot yang tidak dipercaya terjadi di web host di dalam container SDK sekali pakai daripada di node.
---

# 0004 — `CBotBuilder` berjalan di web host dalam container sandbox

## Konteks

Membuat cBot pengguna berarti menjalankan **MSBuild tidak dipercaya** — kode arbitrer pada waktu build (target, source generator, restore script). Itu memerlukan Docker socket untuk memutar container SDK. Node menjalankan trading container dan tidak boleh juga memegang privilege build.

## Keputusan

`CBotBuilder` berjalan **di web host** (yang sudah memiliki Docker socket), di dalam **container SDK sekali pakai** dengan:

- direktori `/work` yang di-bind-mount (hanya input/output build, bukan filesystem host);
- volume `app-nuget-cache` bersama untuk kinerja restore;
- tanpa akses jaringan host di luar apa yang restore butuhkan.

Jadi MSBuild tidak dipercaya tidak dapat mencapai filesystem atau jaringan host. Container run/backtest, sebaliknya, berjalan di node yang dipilih oleh `NodeScheduler`.

## Konsekuensi

- Privilege build (Docker socket) dibatasi ke web host; node hanya menjalankan image trading yang diizinkan.
- Setiap build terisolasi dalam container yang dapat dibuang — build berbahaya tidak dapat bertahan atau melarikan diri.
- Web host harus memiliki Docker socket tersedia; ini adalah requirement deployment, bukan opsional.
