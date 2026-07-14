---
slug: /for-traders
title: cMind untuk trader cTrader
description: Mengapa trader cTrader harus self-host cMind — miliki stack dan data Anda, author, backtest, jalankan dan pantau cBot dalam satu konsol bertenaga AI, di laptop, VPS atau telepon Anda.
keywords:
  - cTrader
  - Perdagangan algoritmik
  - Platform perdagangan self-hosted
  - Backtesting cBot
  - Bot perdagangan AI
  - Perangkat lunak perdagangan open source
sidebar_position: 5
---

# cMind untuk trader cTrader 📈

Anda sudah berdagang di cTrader. Anda sudah menggabungkan editor kode, backtester, VPS, dan tiga
tab browser. **cMind meruntuhkan semua itu menjadi satu konsol gelap, ramah keyboard yang Anda jalankan
sendiri** — dan itu open source, jadi tidak ada yang tentang edge, strategi, atau credentials Anda
pernah meninggalkan kotak Anda.

:::tip[TL;DR]
Self-host cMind di laptop, VPS murah, atau server rumah. Author, backtest, jalankan, dan pantau cBot
di satu tempat, dengan inti AI melakukan pekerjaan. → [Jalankan dalam 5 menit](./deployment/local.md)
:::

## Mengapa self-host daripada layanan hosting?

- **Miliki stack dan data Anda.** cBot, credentials, token, dan riwayat equity Anda hidup di
  **infrastruktur Anda** — tidak ada pihak ketiga, tidak ada lock-in, tidak ada email "kami menutup produk ini".
- **Benar-benar milik Anda untuk diubah.** C# 14 / .NET 10, DDD ketat, EF Core + PostgreSQL, server MCP
  — semuanya open source dan hackable. Fork, perpanjang, kirim PR.
- **Tidak ada paywall per-fitur.** Bawa kunci AI Anda sendiri untuk penyedia apa pun; setiap fitur AI aktif.

Lebih suka tidak menjalankan server sendiri? Perusahaan hosting dapat menjalankan cMind terkelola untuk Anda —
lihat [Untuk cloud & penyedia VPS](./for-cloud-providers.md).

## Satu konsol, tidak ada tab-juggling

- **Author** di editor Monaco yang nyata (editor VS Code), dengan template C# **dan** Python dan
  sandboxed `dotnet build` dalam container yang dapat dibuang. → [Build & backtest](./features/build-and-backtest.md)
- **Backtest** di seluruh armada node dan pantau kurva ekuitas streaming kembali live.
- **Jalankan** strategi live dan **pantau** dari satu dashboard. → [Dashboard](./features/dashboard.md)
- **Salin** akun master ke banyak akun di seluruh broker dan cTrader ID, dengan rekonsiliasi
  yang bertahan koneksi dropped dan rotating token. → [Copy trading](./features/copy-trading.md)

## AI yang melakukan pekerjaan, bukan obrolan kecil

Bawa kunci API Anda sendiri (penyedia apa pun yang didukung — cloud atau model lokal) dan dapatkan plain-English → real compiling cBot dengan loop self-repair, parameter tuning, backtest post-mortem, dan risk guard yang dapat auto-stop bot misbehaving. → [Temui inti AI](./features/ai.md)

## Tooling tingkat institusional, untuk satu

Ketelitian yang sama dengan desk yang membayar, di kotak Anda sendiri:

- [Backtest integrity](./features/backtest-integrity.md) · [Position sizing](./features/position-sizing.md)
- [Strategy health](./features/strategy-health.md) · [Regime lab](./features/regime-lab.md)
- [Execution TCA](./features/execution-tca.md) · [Trading journal](./features/trading-journal.md)
- [Agent Studio](./features/agent-studio.md) · [Contrarian positioning](./features/contrarian-positioning.md)

## Berjalan di mana Anda berada

Mulai di laptop Anda dengan `docker compose up`, naik ke VPS murah atau server rumah ketika Anda siap, dan periksa bot Anda dari telepon Anda — cMind adalah
[PWA](./features/pwa.md) yang dapat diinstal dan mobile-first. → [Jalankan secara lokal](./deployment/local.md)

Ingin klien AI Anda menggerakannya? Ada [server MCP](./features/mcp.md) built-in.

## Bantu membuatnya lebih baik

cMind adalah open source dan MIT-licensed — roadmap dibentuk komunitas:

- Daftarkan issue dan feature request, dan suara untuk apa yang penting.
- Tambahkan template cBot, adaptor penyedia AI, atau terjemahan UI.
- Kirim PR — tiga tingkat tes (unit + integration + E2E) dan DDD ketat menjaga standar tinggi, dan
  [panduan Contributing](./contributing.md) membimbing Anda melaluinya.

Siap? → [Baca intro](./intro.md) lalu [jalankan secara lokal](./deployment/local.md).
