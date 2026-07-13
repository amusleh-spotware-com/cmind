---
slug: /for-traders
title: cMind untuk trader cTrader
description: Mengapa trader cTrader harus self-host cMind — miliki stack dan data Anda, author, backtest, jalankan dan monitor cBot dalam satu konsol bertenaga AI, di laptop, VPS atau ponsel Anda.
keywords:
  - cTrader
  - Perdagangan algoritmik
  - Platform perdagangan self-hosted
  - Backtesting cBot
  - Bot perdagangan AI
  - Perangkat lunak perdagangan open source
sidebar_position: 5
---

# cMind untuk trader cTrader

Anda sudah berdagang di cTrader. Anda sudah menggosok code editor, backtester, VPS, dan tiga tab browser. **cMind menciutkan semuanya menjadi satu konsol gelap, keyboard-friendly yang Anda jalankan sendiri** — dan itu open source, jadi tidak ada tentang edge, strategi, atau kredensial Anda yang pernah meninggalkan box Anda.

:::tip TL;DR
Self-host cMind di laptop, VPS murah, atau home server. Author, backtest, run, dan monitor cBot di satu tempat, dengan inti AI melakukan chore. → [Jalankan dalam 5 menit](./deployment/local.md)
:::

## Mengapa self-host daripada hosted service?

- **Miliki stack dan data Anda.** cBot, kredensial, token, dan equity history Anda hidup di **infrastruktur Anda** — tidak ada pihak ketiga, tidak ada lock-in, tidak ada email "kami menyunsetkan produk ini".
- **Itu benar-benar milik Anda untuk diubah.** C# 14 / .NET 10, strict DDD, EF Core + PostgreSQL, server MCP — semua open source dan hackable. Fork, extend, kirim PR.
- **Tidak ada paywall per-fitur.** Bawa kunci AI Anda sendiri untuk provider apa pun; setiap fitur AI aktif.

Lebih suka tidak menjalankan server sendiri? Perusahaan hosting dapat menjalankan managed cMind untuk Anda — lihat [Untuk cloud & VPS provider](./for-cloud-providers.md).

## Satu konsol, tidak ada tab-juggling

- **Author** di IDE Monaco real (editor VS Code), dengan template C# **dan** Python dan `dotnet build` sandbox dalam container sekali pakai. → [Build & backtest](./features/build-and-backtest.md)
- **Backtest** melintasi fleet node dan tonton equity curve stream kembali live.
- **Jalankan** strategi live dan **monitor** dari satu dashboard. → [Dashboard](./features/dashboard.md)
- **Copy** account master ke banyak account melintasi broker dan cTrader ID, dengan reconciliation yang bertahan dropped connection dan rotating token. → [Copy trading](./features/copy-trading.md)

## AI yang lakukan chore, bukan small talk

Bawa kunci API Anda sendiri (provider yang didukung apa pun — cloud atau model lokal) dan dapatkan plain-English → real compiling cBot dengan self-repair loop, parameter tuning, backtest post-mortem, dan risk guard yang dapat auto-stop bot yang salah perilaku. → [Temui inti AI](./features/ai.md)

## Institutional-grade tooling, untuk satu orang

Rigor yang sama yang dibayar desk, di box Anda sendiri:

- [Backtest integrity](./features/backtest-integrity.md) · [Position sizing](./features/position-sizing.md)
- [Strategy health](./features/strategy-health.md) · [Regime lab](./features/regime-lab.md)
- [Execution TCA](./features/execution-tca.md) · [Trading journal](./features/trading-journal.md)
- [Agent Studio](./features/agent-studio.md) · [Contrarian positioning](./features/contrarian-positioning.md)

## Berjalan di mana Anda berada

Mulai di laptop Anda dengan `docker compose up`, lulus ke VPS murah atau home server saat Anda siap, dan periksa bot Anda dari ponsel Anda — cMind adalah installable, mobile-first [PWA](./features/pwa.md). → [Jalankan secara lokal](./deployment/local.md)

Ingin klien AI Anda menjalankannya? Ada built-in [server MCP](./features/mcp.md).

## Bantu membuatnya lebih baik

cMind adalah open source dan MIT-licensed — roadmap adalah community-shaped:

- Ajukan issue dan feature request, dan vote apa yang penting.
- Tambahkan template cBot, adapter AI provider, atau terjemahan UI.
- Kirim PR — tiga test tier (unit + integration + E2E) dan strict DDD menjaga bar tinggi, dan [panduan Contributing](./contributing.md) membimbing Anda melalui itu.

Siap? → [Baca intro](./intro.md) kemudian [jalankan secara lokal](./deployment/local.md).
