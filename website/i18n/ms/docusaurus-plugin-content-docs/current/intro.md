---
slug: /intro
title: Selamat datang ke cMind
description: Pengenalan mesra kepada cMind — platform operasi dagangan untuk cTrader yang sumber terbuka dan boleh dihos sendiri.
sidebar_position: 1
---

# Selamat datang ke cMind 👋

:::warning[Perisian alfa — tidak bersedia untuk pengeluaran]
cMind sedang dalam pembangunan aktif. Jangkakan ketidaksempurnaan, perubahan yang memecahkan antara versi, dan ciri yang masih dalam proses. **Kami memerlukan penguji komuniti, pelapor pepijat, dan penyumbang awal** untuk membantu membentuknya. Jika anda menemui masalah, [laporkannya](https://github.com/amusleh-spotware-com/cmind/issues/new?template=bug_report.yml) — maklum balas dunia nyata anda adalah perkara paling berharga yang boleh anda sumbangkan sekarang.
:::

Jadi anda mahu membina bot dagangan, membacktest tanpa melelehkan komputer riba, menjalankannya merentas
beberapa mesin, mencerminkan dagangan ke belasan akaun, dan membiarkan AI mengawasi risiko sementara
anda tidur. **Anda berada di tempat yang betul.**

cMind ialah **platform operasi dagangan untuk cTrader yang sumber terbuka dan boleh dihos sendiri**.
Anggaplah ia sebagai keseluruhan meja dagangan anda — penulisan, pelaksanaan, armada pengkomputeran,
copy trading, dan teras AI — dikemas dalam satu aplikasi yang tenang, gelap, mesra mudah alih yang anda
miliki dari hujung ke hujung.

:::tip[Dalam satu ayat]
Bina → backtest → jalankan → salin strategi cTrader anda pada skala besar, dengan AI terbina dalam, pada
pelayan anda sendiri, di bawah jenama anda sendiri.
:::

## Apa yang sebenarnya boleh dilakukannya?

| Anda mahu… | cMind melakukannya | Baca lanjut |
|---|---|---|
| Menulis cBot dalam pelayar | Monaco IDE + templat C#/Python, binaan berkotak pasir | [Bina & backtest](./features/build-and-backtest.md) |
| Backtest merentas mesin | Armada nod pulih-sendiri memilih mesin paling lapang | [Penskalaan](./deployment/scaling.md) |
| Menyalin satu akaun ke banyak | Pencerminan teguh dengan penyegerakan semula, tanpa dagangan berganda | [Copy trading](./features/copy-trading.md) |
| Biarkan AI buat kerja berat | Penjanaan strategi, pembaikan sendiri, pengawal risiko, post-mortem | [Teras AI](./features/ai.md) |
| Kekal dalam peraturan prop firm | Penjejakan ekuiti langsung + simulasi peraturan cabaran | [Prop firm](./features/prop-firm.md) |
| Sahkan kelebihan backtest | Pembetulan PSR / DSR / t-stat overfitting | [Backtest Integrity Lab](./features/backtest-integrity.md) |
| Fahami tabiat dagangan anda | Pengesanan kebocoran tingkah laku + jurulatih AI | [Jurnal Dagangan](./features/trading-journal.md) |
| Jejaki peristiwa makro untuk strategi | Kalendar tepat-masa, pemadaman berita, API cBot | [Kalendar ekonomi](./features/economic-calendar.md) |
| Skor kekuatan makro mata wang | Pandangan ke hadapan AI merentas semua pasangan | [Kekuatan mata wang](./features/currency-strength.md) |
| Selamatkan akaun dengan 2FA | Aplikasi pengesah TOTP + kod sandaran | [Pengesahan dua faktor](./features/two-factor-auth.md) |
| Biarkan pemilik menala pada masa jalan | Setiap pilihan white-label secara langsung dalam Tetapan → Deployment | [Tetapan pemilik](./features/white-label-owner-settings.md) |
| Jalankan dalam mana-mana bahasa | 23 bahasa termasuk RTL — binaan gagal jika kunci tiada | [Penyetempatan](./features/localization.md) |
| Melancarkannya sebagai produk *anda* | White-label penuh: nama, warna, logo, favicon | [White-label](./features/white-label.md) |
| Menjalankannya pada telefon anda | PWA boleh dipasang, mengutamakan mudah alih | [PWA](./features/pwa.md) |
| Memacunya daripada klien AI | Pelayan MCP terbina dalam (HTTP + SSE) | [MCP](./features/mcp.md) |

## Laluan 5 minit ⏱️

Jika anda ada Docker dan lima minit, anda boleh mula bermain dengan instance cMind sebenar sekarang juga:

```bash
git clone https://github.com/amusleh-spotware-com/cmind.git
cd cmind
cp .env.example .env        # set OWNER_EMAIL + OWNER_PASSWORD
docker compose up --build
```

Kemudian buka **<http://localhost:8080>**, log masuk, dan anda pun bermula. Panduan lengkap (dengan
penyelesaian masalah untuk apabila Docker pasti ada pendapatnya) terdapat di
**[Menjalankan secara setempat](./deployment/local.md)**.

## Baru di sini? Ikut jalan bata kuning 🟡

1. **[Untuk siapa ini?](./audience.md)** — pastikan anda jenis masalah kami.
2. **[Menjalankan secara setempat](./deployment/local.md)** — hidupkan instance sebenar.
3. **[Ciri](./features/README.md)** — lawatan penuh tentang apa yang ada di dalam.
4. **[Deploy secara serius](./deployment/cloud.md)** — Docker, Kubernetes, Azure, AWS.
5. **[Jadikan ia milik anda](./white-label-for-business.md)** — terapkan white-label untuk perniagaan anda.
6. **[Menyumbang](./contributing.md)** — PR (manusia *dan* dibantu AI) amat dialu-alukan.

## Sepatah kata tentang wang 💸

cMind menggerakkan **modal sebenar**. Kami memandangnya serius — setiap perubahan dihantar dengan ujian
unit, integrasi, dan hujung-ke-hujung, termasuk laluan kegagalan (sambungan terputus, pesanan ditolak,
nod mati). Anda juga patut memandangnya serius: **uji dahulu pada akaun demo**, dan baca
[nota pematuhan](./features/compliance.md) sebelum menghalakannya ke apa-apa yang sebenar. Dagangan
berisiko; perisian ini ialah alat, bukan nasihat kewangan.

Baiklah — cukuplah mukadimah. Mari bina sesuatu. →
