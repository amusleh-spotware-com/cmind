---
slug: /white-label-for-business
title: White-label untuk bisnis
description: Kirim cMind sebagai produk branded Anda sendiri — untuk prop firm, broker, dan copy-trading business. Rebrand setiap surface via config, tidak ada perubahan kode.
sidebar_position: 4
---

# White-label cMind untuk bisnis Anda

Jalankan prop firm, broker desk, atau copy-trading service? cMind dibangun dari hari pertama untuk **dijual kembali sebagai produk Anda sendiri**. Setiap surface — nama, logo, favicon, warna, bahkan aplikasi phone yang dapat diinstal — membengkok ke brand Anda. Pelanggan Anda melihat perusahaan *Anda*. Tidak ada perubahan kode, tidak ada fork, hanya config.

:::tip TL;DR
Arahkan `App:Branding` ke nama, warna, dan logo Anda. Restart. Selesai. Referensi teknis penuh hidup di [dokumen fitur White-label](./features/white-label.md).
:::

## Apa yang dapat Anda rebrand

| Surface | Apa yang berubah |
|---|---|
| **Nama produk** | Teks app bar + judul tab browser |
| **Logo & favicon** | Mark Anda di mana-mana, termasuk tab browser |
| **Warna** | Palet penuh — primary, surface, status color — mengalir melalui seluruh UI *dan* CSS app sendiri via design token |
| **Aplikasi yang dapat diinstal (PWA)** | Nama add-to-home-screen, icon, dan splash gunakan brand Anda |
| **Meta / SEO** | Deskripsi dan support URL adalah milik Anda |
| **Custom CSS** | Inject polish sendiri untuk 5% terakhir |

Semuanya default ke identitas cMind stock, jadi Anda hanya override yang Anda perdulikan.

## Rebrand 60-detik

Set ini pada deployment Anda (JSON config atau environment variable):

```json
{
  "App": {
    "Branding": {
      "ProductName": "AcmeFX",
      "CompanyName": "Acme Markets Ltd",
      "SupportUrl": "https://support.acme.example",
      "LogoUrl": "/branding/acme-logo.svg",
      "FaviconUrl": "/branding/acme.ico",
      "PrimaryColor": "#2D7FF9",
      "SecondaryColor": "#1E63C8",
      "ShowSiteLink": false
    }
  }
}
```

Environment-variable form: `App__Branding__ProductName=AcmeFX`. Warna divalidasi saat startup — bad hex value gagal boot dengan pesan jelas daripada render halaman yang rusak. Bagus dan loud, tepat saat Anda menginginkannya.

## Link "Powered by cMind"

Secara **default**, dashboard menunjukkan small, tasteful **link "Powered by cMind"** yang arahkan pengunjung kembali ke site ini. Itu on by default karena kami bangga dengan proyek dan itu membantu trader lain menemukannya — tetapi itu **panggilan Anda**.

- **Simpan** (default): subtle credit link di dashboard. Tidak membiayai Anda apa pun, membantu proyek.
- **Sembunyikan**: set `App__Branding__ShowSiteLink=false` dan itu hilang sepenuhnya — sempurna untuk fully white-labeled deployment di mana produk unmistakably *Anda*.

Lihat [dokumen fitur White-label](./features/white-label.md#powered-by-link) untuk exactly di mana itu render.

## Multi-tenant, per-customer branding

Karena branding adalah hanya deployment config, setiap tenant deployment dapat membawa identitas sendiri. Jalankan terpisah instance per pelanggan, atau drive branding dari control plane Anda sendiri — app membaca dari `IOptionsMonitor`, jadi itu dapat bahkan rebuild tema live saat option berubah.

Pair dengan:

- **[Feature toggle](./features/feature-toggles.md)** — tentukan capability mana setiap tenant lihat.
- **[Aturan prop-firm](./features/prop-firm.md)** — enforce challenge rule Anda dengan live equity tracking.
- **[Performance fee](./features/copy-performance-fees.md)** + **[marketplace provider](./features/copy-provider-marketplace.md)** — monetisasi copy trading.
- **[Compliance](./features/compliance.md)** — simpan audit trail regulator Anda minta.

## Asset & hosting

Drop logo/favicon Anda ke `wwwroot/branding/` app Web (atau arahkan `LogoUrl`/`FaviconUrl` ke URL absolute apa pun). Deploy bagaimanapun cocok — [Docker](./deployment/local.md), [Kubernetes](./deployment/kubernetes.md), [Azure](./deployment/cloud-azure.md), atau [AWS](./deployment/cloud-aws.md).

Siap membuatnya milik Anda? Mulai dengan [referensi white-label teknis →](./features/white-label.md)
