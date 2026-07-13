---
slug: /for-brokers
title: cMind untuk broker cTrader
description: Mengapa broker cTrader harus menjalankan white-label cMind untuk klien mereka sendiri — berikan trader AI, copy trading dan prop-firm challenge di bawah brand Anda, batasi account ke broker Anda, dan menangkan keunggulan atas kompetitor.
keywords:
  - Broker cTrader
  - Platform perdagangan white-label
  - Teknologi broker
  - Copy trading untuk broker
  - Alat perdagangan AI
  - Perangkat lunak prop firm
sidebar_position: 6
---

# cMind untuk broker cTrader

Anda menjalankan broker cTrader. Klien Anda sudah bisa berdagang — tetapi begitu juga klien broker lain. **cMind memungkinkan Anda memberikan trader full AI-powered trading operations platform, branded sebagai milik Anda**, sehingga mereka build, backtest, run, copy, dan monitor strategi di dalam ekosistem *Anda* daripada drift ke tool pihak ketiga. Itu klien yang lebih sticky, volume lebih banyak, dan real edge atas broker yang hanya menawarkan terminal.

:::tip TL;DR
Jalankan white-label cMind untuk klien Anda. Batasi account ke broker **Anda**, switch on AI dan copy trading, dan kirim di bawah brand Anda. → [White-label untuk bisnis](./white-label-for-business.md)
:::

## Edge yang Anda dapatkan atas broker lain

- **Diferensiasi pada tooling, bukan hanya spread.** Berikan klien generasi AI cBot, backtesting pada managed cluster, copy trading, dan prop-firm challenge — capabilities yang kebanyakan broker tidak menawarkan.
- **Simpan klien di ekosistem Anda.** Ketika trader build dan run strategi mereka di platform branded Anda, mereka tetap. Retention adalah keseluruhan permainan.
- **Di bawah brand Anda, di domain Anda.** Nama, logo, warna, favicon, bahkan aplikasi phone yang dapat diinstal — semua milik Anda. Tidak ada yang melihat "cMind." → [Fitur white-label](./features/white-label.md)

## Layani hanya account Anda (broker allowlist)

Menjalankan white-label untuk klien *Anda*? Batasi broker mana dari trading account yang pengguna dapat tambahkan sehingga deployment Anda hanya melayani buku Anda:

```json
{
  "App": {
    "Accounts": {
      "AllowedBrokers": ["Nama Broker Anda"]
    }
  }
}
```

Ketika allowlist diatur, cMind memeriksa setiap account yang pengguna coba tambahkan — baik melalui cTrader Open API maupun melalui cID login manual (diverifikasi dengan membaca nama broker real account) — dan menolak account apa pun yang tidak ada di list Anda. Biarkan kosong dan setiap broker diizinkan (default). Lihat [dokumen fitur White-label](./features/white-label.md#broker-allowlist) untuk mekanik penuh.

## Kirim satu Open API app untuk semua pengguna Anda

Skip hassle per-user: sediakan **satu aplikasi cTrader Open API** dan setiap klien otorisasi account mereka melalui itu — klien tidak pernah daftar mereka sendiri. Daftar single redirect URL, drop kredensial di config atau owner setting, dan shared-mode nyala untuk semua. Negosiasikan cTrader message limit lebih tinggi? Tune **per-message-type client rate limit** (atau disable pacing). → [Aplikasi Open API & rate limit bersama](./features/open-api-shared-app.md)

## Cara baru untuk monetisasi

- **AI, dengan zero friction untuk klien.** Sediakan default AI provider key di level deployment dan setiap klien mendapat AI feature seketika — tanpa signup di tempat lain. Mark up, atau bundle ke premium tier. Klien masih bisa bawa kunci mereka sendiri. → [Fitur AI](./features/ai.md)
- **Prop-firm challenge.** Jalankan funded-trader challenge dengan live equity tracking dan enforced rule, dan charge untuk entry. → [Aturan prop-firm](./features/prop-firm.md)
- **Bisnis copy-trading.** Performance fee dan marketplace provider ubah copy trading menjadi revenue. → [Performance fee](./features/copy-performance-fees.md) · [Marketplace provider](./features/copy-provider-marketplace.md)
- **Feature tier.** Tentukan capability mana setiap client segment lihat dengan [feature toggle](./features/feature-toggles.md).

## Regulated, auditable, multi-tenant

- **[Compliance](./features/compliance.md)** log memberikan Anda audit trail yang regulator minta.
- **[Two-factor auth](./features/two-factor-auth.md)** dapat dibuat mandatory per deployment.
- **Per-client branding** — jalankan terpisah branded instance per segment, driven dari control plane Anda sendiri. → [Multi-tenant branding](./white-label-for-business.md#multi-tenant-per-customer-branding)

## Cara memulai

1. Baca [White-label untuk bisnis](./white-label-for-business.md) untuk 60-detik rebrand.
2. Set `App:Accounts:AllowedBrokers` ke broker Anda dan pilih [feature set](./features/feature-toggles.md) Anda.
3. [Deploy](./deployment/cloud.md) — Docker, Kubernetes, Azure, atau AWS.

Tidak ingin jalankan infrastruktur sendiri? Provider hosting dapat mengoperasikan managed cMind untuk Anda — arahkan ke [Untuk cloud & VPS provider](./for-cloud-providers.md).

## Bentuk roadmap

cMind adalah open source. Broker yang build di atasnya mendapat outsized say ke mana itu pergi — request integrasi dan kontrol yang Anda butuhkan, dan berkontribusi balik via [panduan Contributing](./contributing.md).
