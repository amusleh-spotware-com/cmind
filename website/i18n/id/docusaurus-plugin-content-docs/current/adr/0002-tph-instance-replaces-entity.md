---
title: 0002 — Status instance adalah TPH; transisi mengganti entity
description: Mengapa id instance berubah saat bergerak melalui lifecycle, dan mengapa id container adalah kunci stabil.
---

# 0002 — Status instance adalah TPH; transisi mengganti entity

## Konteks

Sebuah instance run/backtest bergerak melalui state (pending → scheduled → starting → running → terminal). Kami model state dengan EF Core **Table-Per-Hierarchy (TPH)**: setiap state adalah subtype (`StartingRunInstance`, `RunningRunInstance`, …). Kolom discriminator TPH EF **tidak dapat berubah** pada row yang ada.

## Keputusan

Transisi state **mengganti entity** dengan instance subtype baru daripada memutasi field status. Karena row diganti, **instance id berubah** melintasi starting → running → terminal. **Container id stabil** dan dibawa melintasi transisi; node agent HTTP dikunci oleh container id untuk status/report/stop/logs.

## Konsekuensi

- Setiap state adalah tipe yang berbeda dengan hanya field dan method yang valid dalam state tersebut — transisi ilegal dan akses field yang tidak masuk akal adalah kesalahan compile, bukan pengecekan runtime.
- Caller harus **tidak** cache instance id melintasi transisi; gunakan container id sebagai handle stabil untuk apa pun yang meliputi state.
- Logika transisi hidup di `InstanceTransitions`; perubahan id disengaja, bukan bug.
