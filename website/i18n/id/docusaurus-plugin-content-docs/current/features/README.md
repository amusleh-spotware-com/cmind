---
slug: /features
title: Fitur — tur lengkap
description: Semua yang bisa cMind lakukan — copy trading, AI, build & backtest, prop-firm guard, white-label, PWA, MCP, dan lebih banyak lagi.
sidebar_label: Gambaran Umum
---

# Fitur — tur lengkap 🧭

Selamat datang di tur grand. cMind mengemas *banyak* ke dalam satu app, jadi ini adalah peta. Setiap capability memiliki doc deep-dive sendiri — klik melalui ke apa pun gatal yang Anda garuk.

## 🔁 Copy Trading

Permata mahkota. Cerminkan akun master ke banyak, dan jaga agar tetap sinkron bahkan ketika internet misbehave.

- **[Copy trading](./copy-trading.md)** — inti: mirroring, order type, SL/TP, slippage, desync/resync.
- **[Execution transparency](./copy-execution-transparency.md)** — lihat persis apa yang disalin, kapan, dan mengapa.
- **[Performance fees](./copy-performance-fees.md)** — charge untuk signal Anda, gaya high-water-mark.
- **[Provider marketplace](./copy-provider-marketplace.md)** — biarkan trader menemukan dan follow provider.
- **[Notifications](./copy-notifications.md)** — dapatkan tahu ketika ada sesuatu yang memerlukan Anda.
- **[AI copy recommender](./ai-copy-recommender.md)** — biarkan AI menyarankan siapa yang harus disalin.
- **[Open API token lifecycle](./token-lifecycle.md)** — bagaimana cMind menjaga tepat satu token yang valid per cID.

## 📊 Home base Anda

- **[Dashboard](./dashboard.md)** — command center live, mobile-first: KPI dengan sparkline, activity chart, status ring, live feed, dan (untuk admin) cluster health. Itu refresh sendiri.

## 🧠 Inti AI

Bukan chat box yang disolder di samping — AI yang benar-benar *melakukan pekerjaan*.

- **[AI assistant, agent, risk guard & alert](./ai.md)** — strategi generation, self-repairing build, background risk guard yang dapat auto-stop bot, dan smart alert.

## 🛠️ Build & Run

- **[Build & backtest cBot](./build-and-backtest.md)** — IDE Monaco in-browser, template C#/Python, sandboxed build, dan live equity curve.
- **[Server MCP](./mcp.md)** — expose tools cMind di atas HTTP + SSE sehingga klien AI dapat menggerakannya.

## 🏢 Jalankan sebagai bisnis

- **[White-label / branding](./white-label.md)** — rebrand setiap permukaan melalui config.
- **[Simulasi tantangan prop-firm](./prop-firm.md)** — enforce daily-loss, drawdown, dan target rule dengan live equity.
- **[Feature toggles](./feature-toggles.md)** — putuskan apa yang dilihat setiap deployment/tenant.
- **[Compliance / legal](./compliance.md)** — audit trail dan legal surface.

## 📱 Pengalaman

- **[Aplikasi yang dapat diinstal (PWA)](./pwa.md)** — mobile-first, offline shell, add-to-home-screen.
- **[UI design system & mobile-first](../ui-guidelines.md)** — design token dan rule di balik tampilan.

## ⚙️ Di bawah kap

Bits operasional yang membuat semuanya berjalan:

- **[Node fleet & discovery](../operations/node-discovery.md)** — bagaimana node self-register dan heal.
- **[Horizontal scaling](../deployment/scaling.md)** — tambahkan replicas, tidak ada koordinator eksternal yang diperlukan.
- **[Logging & audit](../operations/logging.md)** — structured log + OpenTelemetry.
- **[Deployment](../deployment/local.md)** — dapatkan berjalan di mana saja.

:::note Menjaga docs jujur
Setiap doc fitur dijaga tetap lockstep dengan kode — ubah perilaku, perbarui doc, commit sama.
Jika Anda pernah melihat drift, itu adalah bug: silakan [buka issue](https://github.com/amusleh-spotware-com/cmind/issues/new/choose)
atau kirim PR. 🙏
:::
