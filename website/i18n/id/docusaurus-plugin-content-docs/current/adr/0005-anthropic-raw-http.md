---
title: 0005 — Klien AI menggunakan HTTP mentah, bukan SDK Anthropic
description: Mengapa IAiClient memanggil API Anthropic melalui HttpClient yang diketik daripada SDK resmi, dan mengapa AI sepenuhnya gated di kunci.
---

# 0005 — Klien AI menggunakan HTTP mentah, bukan SDK Anthropic

## Konteks

Setiap fitur AI (generasi strategi, self-repair, risk guard, post-mortem) memanggil API Anthropic. Ketergantungan SDK menambahkan permukaan transitif yang tidak kami kontrol, menghubungkan cadence rilis kami ke milik mereka, dan menyembunyikan kontrak wire yang tepat kami butuhkan untuk alasan tentang ketahanan dan biaya.

## Keputusan

`IAiClient` memanggil Anthropic melalui **HTTP mentah** melalui `HttpClient` yang diketik — deliberately **bukan** SDK. `AiFeatureService` adalah orchestrator tunggal yang dibagikan oleh endpoint Web, `AiTools` MCP, dan `AiRiskGuard`. Seluruh permukaan **gated di `AppOptions.Ai.ApiKey`**: tanpa kunci, setiap fitur mengembalikan `AiResult.Fail` dan aplikasi berjalan tidak berubah.

## Konsekuensi

- Tidak ada kunci yang diperlukan untuk build, test, atau E2E — CI dan dev lokal menjalankan aplikasi penuh tanpa AI.
- Kami memiliki bentuk request/response, kebijakan retry/timeout, dan akuntansi token secara eksplisit.
- Fitur Anthropic baru harus diwired dengan tangan; kami menukar kenyamanan untuk kontrol dan permukaan ketergantungan yang lebih kecil. Lihat referensi `claude-api` untuk id model saat ini dan parameter.
