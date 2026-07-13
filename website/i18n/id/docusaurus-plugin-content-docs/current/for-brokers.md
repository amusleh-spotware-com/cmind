---
slug: /for-brokers
title: cMind untuk broker cTrader
description: Mengapa broker cTrader harus menjalankan cMind white-label untuk kliennya sendiri — berikan trader AI, copy trading dan tantangan prop-firm di bawah brand Anda, batasi akun ke brokerage Anda, dan menangkan keunggulan atas pesaing.
keywords:
  - Broker cTrader
  - Platform perdagangan white-label
  - Teknologi broker
  - Copy trading untuk broker
  - Tools perdagangan AI
  - Perangkat lunak prop firm
sidebar_position: 6
---

# cMind untuk broker cTrader 🏦

Anda menjalankan brokerage cTrader. Klien Anda sudah bisa berdagang — tetapi begitu juga klien broker lain.
**cMind memungkinkan Anda memberi trader platform operasi perdagangan penuh bertenaga AI, bermerek sebagai
milik Anda**, sehingga mereka membangun, backtest, jalankan, salin, dan pantau strategi di dalam *ekosistem Anda*
daripada melayang ke alat pihak ketiga. Itu lebih sticky clients, lebih volume, dan keunggulan nyata atas
broker yang menawarkan tidak ada kecuali terminal.

:::tip TL;DR
Jalankan white-label cMind untuk klien Anda. Batasi akun ke **brokerage Anda**, nyalakan AI dan
copy trading, dan kirimnya di bawah brand Anda. → [White-label untuk bisnis](./white-label-for-business.md)
:::

## Keunggulan yang Anda dapatkan atas broker lain

- **Diferensiasi pada tooling, bukan hanya spread.** Berikan klien generasi cBot AI, backtesting pada
  cluster yang dikelola, copy trading, dan tantangan prop-firm — kemampuan yang sebagian besar broker
  tidak tawarkan.
- **Jaga klien di ekosistem Anda.** Ketika trader membangun dan menjalankan strategi mereka di dalam
  platform bermerek Anda, mereka tetap tinggal. Retensi adalah seluruh permainan.
- **Di bawah brand Anda, di domain Anda.** Nama, logo, warna, favicon, bahkan aplikasi telepon yang
  dapat diinstal — semuanya milik Anda. Tidak ada yang melihat "cMind." → [Fitur white-label](./features/white-label.md)

## Layani hanya akun Anda (broker allowlist)

Menjalankan white-label untuk *klien* Anda? Batasi broker mana yang akun trading pengguna dapat tambahkan
sehingga deployment Anda hanya pernah melayani buku Anda:

```json
{
  "App": {
    "Accounts": {
      "AllowedBrokers": ["Your Brokerage Name"]
    }
  }
}
```

Ketika allowlist diatur, cMind memeriksa setiap akun yang pengguna coba tambahkan — baik melalui cTrader Open
API dan melalui login cID manual (diverifikasi dengan membaca nama broker akun nyata) — dan menolak akun apa pun
yang tidak ada di daftar Anda. Biarkan kosong dan setiap broker diizinkan (default). Lihat
[doc fitur White-label](./features/white-label.md#broker-allowlist) untuk mekanika penuh.

## Kirim satu Open API app untuk semua pengguna Anda

Lewati kerumitan per-pengguna: sediakan **satu aplikasi cTrader Open API** dan setiap klien mengotorisasi
akun mereka melaluinya — tidak ada klien yang pernah mendaftar milik mereka sendiri. Daftarkan URL redirect tunggal,
jatuhkan credentials dalam config atau owner settings, dan mode bersama menyala untuk semua orang. Bernegosiasi
limit pesan cTrader yang lebih tinggi? Setel **per-message-type client rate limits** (atau nonaktifkan pacing).
→ [Aplikasi Open API bersama & rate limits](./features/open-api-shared-app.md)

## Cara baru untuk monetisasi

- **AI, tanpa gesekan untuk klien.** Sediakan kunci penyedia AI default pada tingkat deployment dan
  setiap klien mendapat fitur AI secara instan — tidak ada signup di tempat lain. Tandai, atau bundle ke
  premium tier. Klien masih bisa membawa kunci mereka sendiri. → [Fitur AI](./features/ai.md)
- **Tantangan prop-firm.** Jalankan tantangan trader yang didanai dengan tracking ekuitas live dan
  enforced rule, dan charge untuk entries. → [Prop-firm rules](./features/prop-firm.md)
- **Bisnis copy-trading.** Performance fee dan marketplace penyedia mengubah copy trading menjadi
  revenue. → [Performance fees](./features/copy-performance-fees.md) ·
  [Provider marketplace](./features/copy-provider-marketplace.md)
- **Feature tier.** Putuskan kemampuan mana yang dilihat setiap segmen klien dengan
  [feature toggles](./features/feature-toggles.md).

## Diatur, dapat diaudit, multi-tenant

- **[Compliance](./features/compliance.md)** log memberikan Anda audit trail yang regulator Anda minta.
- **[Two-factor auth](./features/two-factor-auth.md)** dapat dibuat mandatory per deployment.
- **Per-client branding** — jalankan instance terpisah bermerek per segmen, didorong dari control
  plane Anda sendiri. → [Multi-tenant branding](./white-label-for-business.md#multi-tenant-per-customer-branding)

## Cara memulai

1. Baca [White-label untuk bisnis](./white-label-for-business.md) untuk rebrand 60-detik.
2. Atur `App:Accounts:AllowedBrokers` ke brokerage Anda dan pilih [feature set](./features/feature-toggles.md) Anda.
3. [Deploy](./deployment/cloud.md) itu — Docker, Kubernetes, Azure, atau AWS.

Tidak ingin menjalankan infrastruktur sendiri? Penyedia hosting dapat mengoperasikan cMind terkelola untuk Anda
— arahkan ke [Untuk cloud & penyedia VPS](./for-cloud-providers.md).

## Bentuk roadmap

cMind adalah open source. Broker yang membangun di atasnya mendapatkan suara yang berlipat ganda tentang ke mana
ia pergi — minta integrasi dan kontrol yang Anda butuhkan, dan berkontribusi kembali melalui
[panduan Contributing](./contributing.md).
