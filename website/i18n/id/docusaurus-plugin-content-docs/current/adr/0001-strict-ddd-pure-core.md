---
title: 0001 — DDD ketat dengan Core murni
description: Mengapa logika domain hidup di agregat dalam proyek Core tanpa ketergantungan infrastruktur.
---

# 0001 — DDD ketat dengan `Core` murni

## Konteks

Aplikasi ini menangani uang nyata. Aturan bisnis yang tersebar di endpoint, layanan latar belakang, dan komponen Razor membusuk menjadi perilaku yang tidak dapat diuji dan tidak konsisten — justru di mana bug mengorbankan modal pengguna.

## Keputusan

Logika domain hidup **pada agregat, value object, dan domain service** di `src/Core`, yang dikompilasi dengan **nol ketergantungan infrastruktur** (tanpa EF, HttpClient, Docker, atau ASP.NET). Endpoint, alat MCP, komponen, dan `BackgroundService` **mengorkestra** — mereka tidak pernah memutuskan. Aturan:

- Tidak ada public setter; perubahan state melalui intention-revealing method yang menjaga invariant.
- Agregat mereferensikan satu sama lain melalui **strong ID**, bukan navigation property.
- Satu `SaveChanges` memutasi **satu** agregat; alur cross-agregat menggunakan domain event.
- Primitive yang melewati batas domain dibungkus dalam value object.
- Pelanggaran invariant melempar Core `DomainException`, bukan framework exception.

## Konsekuensi

- Aturan domain dapat diuji unit tanpa database atau web host.
- Kemurnian `Core` ditegakkan oleh `ArchitectureGuardTests` dan akan gagal build jika dilanggar.
- Ada lebih banyak ceremonial (value object, strong ID, domain event) dibanding model anemia — ini adalah biaya yang disengaja untuk menjaga aturan uang-bergerak tetap benar dan di satu tempat.
