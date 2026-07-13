---
slug: /white-label-for-business
title: Label-putih untuk perniagaan
description: Hantar cMind sebagai produk berjenama anda sendiri — untuk prop firm, broker, dan perniagaan salinan perdagangan. Menjenamakan semua permukaan melalui config, tiada perubahan kod.
sidebar_position: 4
---

# Label-putih cMind untuk perniagaan anda 🏢

Jalankan prop firm, meja broker, atau perkhidmatan salinan perdagangan? cMind dibina dari hari pertama untuk
**dijual semula sebagai produk anda sendiri**. Setiap permukaan — nama, logo, favicon, warna, bahkan
apl telefon yang boleh dipasang — membengkok ke jenama anda. Pelanggan anda melihat *perniagaan* anda. Tiada perubahan kod,
tiada garpu, hanya config.

:::tip TL;DR
Arahkan `App:Branding` kepada nama, warna, dan logo anda. Mula semula. Selesai. Rujukan teknikal penuh hidup
dalam [dokumen ciri label-putih](./features/white-label.md).
:::

## Apa yang boleh anda menjenamakan

| Permukaan | Apa yang berubah |
|---|---|
| **Nama produk** | Teks bar apl + tajuk tab pelayar |
| **Logo & favicon** | Tanda anda di mana-mana, termasuk tab pelayar |
| **Warna** | Palet penuh — utama, permukaan, warna status — mengalir melalui keseluruhan UI *dan* CSS aplikasi sendiri melalui token reka bentuk |
| **Apl yang boleh dipasang (PWA)** | Nama tambah-ke-skrin-rumah, ikon, dan percikan menggunakan jenama anda |
| **Meta / SEO** | Penerangan dan URL sokongan adalah milik anda |
| **CSS khusus** | Suntik kilauan anda sendiri untuk 5% terakhir |

Semuanya lalai kepada identiti cMind stok, jadi anda hanya mengatasi apa yang anda pedulikan.

## Rebrand 60 saat

Tetapkan ini pada penempatan anda (config JSON atau pembolehubah persekitaran):

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

Bentuk pembolehubah persekitaran: `App__Branding__ProductName=AcmeFX`. Warna disahkan pada permulaan —
nilai hex yang buruk gagal but dengan mesej yang jelas bukannya memberikan halaman yang rosak. Bagus dan
kuat, betul-betul apabila anda menginginkannya.

## Pautan "Berkuasa oleh cMind"

Secara **lalai**, papan pemuka menunjukkan pautan **"Berkuasa oleh cMind"** yang kecil, mulia
yang menunjukkan pengunjung kembali ke laman ini. Ia hidup secara lalai kerana kami bangga dengan projek dan
ia membantu pedagang lain mencarinya — tetapi ia **panggilan anda**.

- **Simpan itu** (lalai): pautan kredit halus di papan pemuka. Kos anda tidak ada, membantu projek.
- **Sembunyikannya**: tetapkan `App__Branding__ShowSiteLink=false` dan ia hilang sepenuhnya — sempurna untuk
  penempatan berlabel-putih sepenuhnya di mana produk itu jelas milik *anda*.

Lihat [dokumen ciri label-putih](./features/white-label.md#powered-by-link) untuk betul-betul di mana ia
memberikan.

## Penjenamaan berbilang penyewa, setiap pelanggan

Kerana penjenamaan adalah hanya config penempatan, setiap penempatan penyewa boleh membawa identiti sendiri. Jalankan
contoh yang terpisah setiap pelanggan, atau mendorong penjenamaan daripada pesawat kawalan anda sendiri — apl membacanya daripada
`IOptionsMonitor`, jadi ia bahkan boleh membina semula tema hidup apabila pilihan berubah.

Pasang ini dengan:

- **[Togol ciri](./features/feature-toggles.md)** — tentukan keupayaan mana setiap penyewa melihat.
- **[Peraturan prop-firm](./features/prop-firm.md)** — kuatkuasakan peraturan cabaran anda dengan pelacakan ekuiti hidup.
- **[Bayaran prestasi](./features/copy-performance-fees.md)** + **[pasaran penyedia](./features/copy-provider-marketplace.md)** — mendapatkan wang salinan perdagangan.
- **[Pematuhan](./features/compliance.md)** — simpan jejak audit yang regulator anda akan minta.

## Aset & pengehosan

Turunkan logo/favicon anda ke dalam `wwwroot/branding/` apl Web (atau arahkan `LogoUrl`/`FaviconUrl`
pada mana-mana URL mutlak). Sebarkan bagaimana yang sesuai dengan anda — [Docker](./deployment/local.md),
[Kubernetes](./deployment/kubernetes.md), [Azure](./deployment/cloud-azure.md), atau
[AWS](./deployment/cloud-aws.md).

Bersedia membuatnya milik anda? Mulai dengan [rujukan teknikal label-putih →](./features/white-label.md)
