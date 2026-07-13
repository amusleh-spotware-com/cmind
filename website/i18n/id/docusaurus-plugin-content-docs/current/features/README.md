---
slug: /features
title: Fitur — tur lengkap
description: Semua yang dapat dilakukan cMind — copy trading, AI, build & backtest, prop-firm guard, white-label, PWA, MCP, dan lebih banyak.
sidebar_label: Overview
---

# Fitur — tur lengkap

Selamat datang di tur besar. cMind mengemas *banyak* ke dalam satu app, jadi di sini peta-nya. Setiap capability punya doc deep-dive sendiri — klik ke apakah pun gatal Anda.

## Copy trading

Permata mahkota. Cerminkan account master ke banyak, dan simpan mereka sync bahkan saat internet misbehave.

- **[Copy trading](./copy-trading.md)** — core: mirroring, order type, SL/TP, slippage, desync/resync.
- **[Execution transparency](./copy-execution-transparency.md)** — lihat tepat apa yang dicopy, kapan, dan mengapa.
- **[Performance fee](./copy-performance-fees.md)** — charge untuk signal Anda, high-water-mark style.
- **[Marketplace provider](./copy-provider-marketplace.md)** — biarkan trader discover dan follow provider.
- **[Notifikasi](./copy-notifications.md)** — beritahu saat sesuatu butuh Anda.
- **[AI copy recommender](./ai-copy-recommender.md)** — biarkan AI suggest siapa untuk copy.
- **[Lifecycle token Open API](./token-lifecycle.md)** — bagaimana cMind simpan exactly satu valid token per cID.

## Basis rumah Anda

- **[Dashboard](./dashboard.md)** — live, mobile-first command center: KPI dengan sparkline, activity chart, status ring, live feed, dan (untuk admin) cluster health. Itu refresh sendiri.

## Inti AI

Bukan chat box yang dibolt di sisi — AI yang benar-benar *melakukan pekerjaan*.

- **[Asisten AI, agent, risk guard & alert](./ai.md)** — generasi strategi, self-repairing build, background risk guard yang dapat auto-stop bot, dan smart alert.

## Build & run

- **[Build & backtest cBot](./build-and-backtest.md)** — IDE Monaco in-browser, template C#/Python, build sandbox, dan live equity curve.
- **[Server MCP](./mcp.md)** — expose tool cMind over HTTP + SSE sehingga klien AI dapat drive.

## Jalankan sebagai bisnis

- **[White-label / branding](./white-label.md)** — rebrand setiap surface via config.
- **[Simulasi prop-firm challenge](./prop-firm.md)** — enforce daily-loss, drawdown, dan target rule dengan live equity.
- **[Feature toggle](./feature-toggles.md)** — tentukan apa yang setiap deployment/tenant lihat.
- **[Compliance / legal](./compliance.md)** — audit trail dan legal surface.

## Pengalaman

- **[Aplikasi yang dapat diinstal (PWA)](./pwa.md)** — mobile-first, offline shell, add-to-home-screen.
- **[Sistem desain UI & mobile-first](../ui-guidelines.md)** — design token dan rule di balik look.

## Di bawah hood

Bit operasional yang menjaga semuanya berjalan:

- **[Fleet node & discovery](../operations/node-discovery.md)** — bagaimana node self-register dan heal.
- **[Horizontal scaling](../deployment/scaling.md)** — tambahkan replica, tidak ada external coordinator dibutuhkan.
- **[Logging & audit](../operations/logging.md)** — structured log + OpenTelemetry.
- **[Deployment](../deployment/local.md)** — dapatkan berjalan di mana saja.

:::note Simpan dokumen jujur
Setiap doc fitur dijaga dalam lockstep dengan kode — ubah behavior, update doc, commit yang sama. Jika Anda pernah spot drift, itu bug: tolong [buka issue](https://github.com/amusleh-spotware-com/cmind/issues/new/choose) atau kirim PR.
:::
