---
title: 0005 — Klien AI menggunakan HTTP mentah, bukan SDK Anthropic
description: Mengapa IAiClient memanggil API Anthropic melalui HttpClient yang ditaip daripada SDK rasmi, dan mengapa AI sepenuhnya tergerbang pada kunci.
---

# 0005 — Klien AI menggunakan HTTP mentah, bukan SDK Anthropic

## Konteks

Setiap ciri-ciri AI (penjanaan strategi, pembaikan diri, pengawal risiko, post-mortem) memanggil API Anthropic. Bergantung SDK menambah permukaan transitif yang kami tidak kawal, pasang kitaran keluaran kami dengan mereka, dan menyembunyikan kontrak wayar yang tepat yang kami perlukan untuk mempertimbangkan ketahanan dan kos.

## Keputusan

`IAiClient` memanggil Anthropic melalui **HTTP mentah** melalui `HttpClient` yang ditaip — dengan sengaja **bukan** SDK. `AiFeatureService` ialah orkestra tunggal yang dikongsi oleh titik akhir Web, MCP `AiTools`, dan `AiRiskGuard`. Seluruh permukaan adalah **tergerbang pada `AppOptions.Ai.ApiKey`**: tanpa kunci, setiap ciri-ciri mengembalikan `AiResult.Fail` dan apl berjalan tanpa perubahan.

## Akibat

- Tiada kunci diperlukan untuk binaan, ujian, atau E2E — CI dan dev tempatan menjalankan apl penuh tanpa AI.
- Kami memiliki bentuk permintaan/respons, dasar percubaan/masa tamat, dan perakaunan token secara eksplisit.
- Ciri-ciri Anthropic baru mesti diwayar dengan tangan; kami berdagang kemudahan untuk kawalan dan permukaan bergantung yang lebih kecil. Lihat rujukan `claude-api` untuk id model dan parameter semasa.
