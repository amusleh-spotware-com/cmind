---
title: 0001 — DDD Ketat dengan Core Tulen
description: Mengapa logik domain hidup pada agregat dalam projek Core tanpa pergantungan infrastruktur.
---

# 0001 — DDD Ketat dengan `Core` Tulen

## Konteks

Apl ini menggerakkan wang sebenar. Peraturan perniagaan yang tersebar di seluruh titik akhir, perkhidmatan latar belakang, dan komponen Razor rosak menjadi tingkah laku yang tidak dapat diuji, tidak konsisten — tepat di mana pepijat menelan modal pengguna.

## Keputusan

Logik domain hidup **pada agregat, objek nilai, dan perkhidmatan domain** dalam `src/Core`, yang dikompil dengan **pergantungan infrastruktur sifar** (tiada EF, HttpClient, Docker, atau ASP.NET). Titik akhir, alat MCP, komponen, dan `BackgroundService`s **mengorkestra** — mereka tidak pernah memutuskan. Peraturan:

- Tiada setter awam; perubahan keadaan melalui kaedah yang mendedahkan niat yang menjaga invariant.
- Agregat merujuk satu sama lain mengikut **ID kuat**, bukan harta navigasi.
- Satu `SaveChanges` bermutasi **satu** agregat; aliran agregat silang menggunakan peristiwa domain.
- Primitif melintasi sempadan domain dibungkus dalam objek nilai.
- Pelanggaran invariant membaling `DomainException` Core, bukan pengecualian rangka kerja.

## Akibat

- Peraturan domain boleh diuji unit tanpa pangkalan data atau hos web.
- Kemurnian `Core` dikuatkuasakan mesin oleh `ArchitectureGuardTests` dan akan gagal binaan jika rosak.
- Ada lebih banyak upacara (objek nilai, ID kuat, peristiwa domain) daripada model anemik — ini adalah kos sengaja menyimpan peraturan pergerakan wang betul dan di satu tempat.
