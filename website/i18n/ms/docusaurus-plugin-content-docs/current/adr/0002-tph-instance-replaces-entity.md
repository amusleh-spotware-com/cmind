---
title: 0002 — Keadaan contoh ialah TPH; peralihan menggantikan entiti
description: Mengapa id contoh berubah semasa ia bergerak melalui kitaran hayatnya, dan mengapa id bekas ialah kunci yang stabil.
---

# 0002 — Keadaan contoh ialah TPH; peralihan menggantikan entiti

## Konteks

Contoh larian/backtest bergerak melalui keadaan (tertunda → dijadualkan → dimulai → berjalan → terminal). Kami memodelkan keadaan dengan **Table-Per-Hierarchy (TPH)** EF Core: setiap keadaan ialah subtip (`StartingRunInstance`, `RunningRunInstance`, …). Lajur diskriminator TPH EF **tidak boleh berubah** pada baris sedia ada.

## Keputusan

Peralihan keadaan **menggantikan entiti** dengan contoh subtip baru daripada membuat mutasi medan status. Kerana baris digantikan, **id contoh berubah** merentas memulai → berjalan → terminal. **Id bekas adalah stabil** dan dibawa merentas peralihan; ejen nod HTTP diunci dengan id bekas untuk status/laporan/henti/log.

## Akibat

- Setiap keadaan ialah jenis yang berbeza dengan hanya medan dan kaedah yang sah dalam keadaan itu — peralihan haram dan akses medan tanpa makna ialah ralat kompilasi, bukan pemeriksaan masa larian.
- Pemanggilpernah cached id contoh merentas peralihan; gunakan id bekas sebagai pemegang stabil untuk apa pun yang merentang keadaan.
- Logik peralihan hidup dalam `InstanceTransitions`; perubahan id adalah sengaja, bukan pepijat.
