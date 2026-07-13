---
slug: /white-label-for-business
title: White-label untuk bisnis
description: Kirim cMind sebagai produk bermerek Anda sendiri — untuk prop firm, broker, dan bisnis copy-trading. Rebrand setiap permukaan melalui config, tanpa perubahan kode.
sidebar_position: 4
---

# White-label cMind untuk bisnis Anda 🏢

Menjalankan prop firm, meja broker, atau layanan copy-trading? cMind dibangun sejak hari pertama untuk menjadi
**dijual kembali sebagai produk Anda sendiri**. Setiap permukaan — nama, logo, favicon, warna, bahkan
aplikasi telepon yang dapat diinstal — membengkok ke brand Anda. Pelanggan Anda melihat *perusahaan Anda*. Tanpa perubahan kode,
tanpa fork, hanya config.

:::tip TL;DR
Arahkan `App:Branding` ke nama, warna, dan logo Anda. Restart. Selesai. Referensi teknis lengkap hidup
di [doc fitur White-label](./features/white-label.md).
:::

## Apa yang dapat Anda rebrand

| Permukaan | Apa yang berubah |
|---|---|
| **Nama produk** | Teks app bar + judul tab browser |
| **Logo & favicon** | Tanda Anda di mana-mana, termasuk tab browser |
| **Warna** | Palet penuh — primer, permukaan, warna status — mengalir melalui seluruh UI *dan* CSS aplikasi sendiri melalui design token |
| **Aplikasi yang dapat diinstal (PWA)** | Nama add-to-home-screen, icon, dan splash menggunakan brand Anda |
| **Meta / SEO** | Deskripsi dan URL dukungan adalah milik Anda |
| **CSS kustom** | Injeksi polesan Anda sendiri untuk 5% terakhir |

Semuanya default ke identitas cMind stok, jadi Anda hanya override apa yang Anda pedulikan.

## Rebrand 60-detik

Atur ini pada deployment Anda (JSON config atau environment variable):

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

Bentuk environment-variable: `App__Branding__ProductName=AcmeFX`. Warna divalidasi pada startup —
nilai hex yang buruk gagal boot dengan pesan yang jelas alih-alih merender halaman yang rusak. Bagus dan
keras, tepat saat Anda menginginkannya.

## Link "Powered by cMind"

Secara **default**, dashboard menampilkan link kecil dan lezat **"Powered by cMind"** yang
mengarahkan pengunjung kembali ke situs ini. Ini aktif secara default karena kami bangga dengan proyek dan
membantu trader lain menemukannya — tetapi ini **keputusan Anda**.

- **Simpan itu** (default): link kredit halus di dashboard. Tidak mengorbankan apa pun, membantu proyek.
- **Sembunyikan itu**: atur `App__Branding__ShowSiteLink=false` dan itu hilang sepenuhnya — sempurna untuk
  deployment fully white-labeled di mana produk yang tidak dapat disalahartikan *milik Anda*.

Lihat [doc fitur White-label](./features/white-label.md#powered-by-link) untuk persis di mana itu
render.

## Multi-tenant, per-customer branding

Karena branding hanya config deployment, setiap deployment tenant dapat membawa identitasnya sendiri. Jalankan
instance terpisah per pelanggan, atau dorong branding dari control plane Anda sendiri — aplikasi membacanya dari
`IOptionsMonitor`, jadi itu bahkan dapat membangun ulang tema live ketika opsi berubah.

Pasangkan dengan:

- **[Feature toggles](./features/feature-toggles.md)** — putuskan kemampuan mana yang dilihat setiap tenant.
- **[Prop-firm rules](./features/prop-firm.md)** — enforce aturan tantangan Anda dengan tracking ekuitas live.
- **[Performance fees](./features/copy-performance-fees.md)** + **[provider marketplace](./features/copy-provider-marketplace.md)** — monetisasi copy trading.
- **[Compliance](./features/compliance.md)** — jaga audit trail yang regulator Anda minta.

## Aset & hosting

Jatuhkan logo/favicon ke dalam `wwwroot/branding/` aplikasi Web (atau arahkan `LogoUrl`/`FaviconUrl`
ke URL absolut apa pun). Deploy bagaimanapun yang cocok — [Docker](./deployment/local.md),
[Kubernetes](./deployment/kubernetes.md), [Azure](./deployment/cloud-azure.md), atau
[AWS](./deployment/cloud-aws.md).

Siap membuatnya milik Anda? Mulai dengan [referensi white-label teknis →](./features/white-label.md)
