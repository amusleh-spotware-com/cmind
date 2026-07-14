---
slug: /for-brokers
title: cMind untuk broker cTrader
description: Mengapa broker cTrader perlu menjalankan cMind berlabel-putih untuk pelanggan mereka sendiri — berikan pedagang AI, salinan perdagangan dan cabaran prop-firm di bawah jenama anda, hadkan akaun ke pembrokeran anda, dan menang keunggulan berbanding pesaing.
keywords:
  - Broker cTrader
  - Platform perdagangan label-putih
  - Teknologi broker
  - Salinan perdagangan untuk broker
  - Alat perdagangan AI
  - Perisian prop firm
sidebar_position: 6
---

# cMind untuk broker cTrader 🏦

Anda menjalankan pembrokeran cTrader. Pelanggan anda sudah boleh berdagang — tetapi begitu juga klien broker lain.
**cMind memungkinkan anda memberikan pedagang anda platform operasi perdagangan yang berkuasa AI penuh, berjenama sebagai
milik anda sendiri**, jadi mereka membina, ujian belakang, jalankan, salinan, dan memantau strategi di dalam ekosistem *anda*
bukannya hanyut ke alat pihak ketiga. Itu lebih melekit klien, lebih banyak volum, dan keunggulan sebenar berbanding
broker yang tidak menawarkan apa-apa kecuali terminal.

:::tip[TL;DR]
Jalankan cMind label-putih untuk pelanggan anda. Hadkan akaun ke pembrokeran **anda**, hidupkan AI dan
salinan perdagangan, dan hantarnya di bawah jenama anda. → [Label-putih untuk perniagaan](./white-label-for-business.md)
:::

## Keunggulan yang anda dapat berbanding broker lain

- **Membezakan pada alatan, bukan hanya spread.** Berikan klien penjanaan cBot AI, ujian belakang di
  kluster terurus, salinan perdagangan, dan cabaran prop-firm — keupayaan yang kebanyakan broker tidak
  tawarkan.
- **Simpan klien dalam ekosistem anda.** Apabila pedagang membina dan menjalankan strategi mereka di dalam platform berjenama anda,
  mereka tinggal. Pengekalan adalah seluruh permainan.
- **Di bawah jenama anda, di domain anda.** Nama, logo, warna, favicon, bahkan apl telefon yang boleh dipasang —
  semua milik anda. Tiada orang melihat "cMind." → [Ciri label-putih](./features/white-label.md)

## Berkhidmat hanya ke akaun anda (senarai putih broker)

Menjalankan label-putih untuk *klien* anda? Hadkan broker mana yang melakukan perdagangan akaun pengguna boleh menambah jadi
penempatan anda hanya pernah melayani buku anda:

```json
{
  "App": {
    "Accounts": {
      "AllowedBrokers": ["Nama Pembrokeran Anda"]
    }
  }
}
```

Apabila senarai putih ditetapkan, cMind memeriksa setiap akaun yang pengguna cuba tambah — kedua-duanya melalui cTrader Open
API dan melalui log masuk cID manual (disahkan dengan membaca nama broker akaun sebenar) — dan menolak mana-mana
akaun yang bukan pada senarai anda. Biarkan kosong dan setiap broker dibenarkan (lalai). Lihat
[Dokumen ciri label-putih](./features/white-label.md#broker-allowlist) untuk mekanik penuh.

## Hantar satu apl API Terbuka untuk semua pengguna anda

Langkau kesusahan setiap pengguna: sediakan **satu aplikasi API Terbuka cTrader** dan setiap klien mengautentifikasi
akaun mereka melaluinya — tiada klien pernah mendaftar milik mereka sendiri. Daftarkan URL pengubahan arahan tunggal, lepaskan
kredensial dalam config atau tetapan pemilik, dan mod bersama beralih untuk semua orang. Berunding lebih tinggi
had mesej cTrader? Selaraskan **had kadar klien setiap jenis mesej** (atau nyahdayakan tempo).
→ [Apl API Terbuka bersama & had kadar](./features/open-api-shared-app.md)

## Cara baru untuk mendapatkan wang

- **AI, tanpa geseran untuk klien.** Sediakan kunci penyedia AI lalai pada peringkat penempatan dan
  setiap klien mendapat ciri AI dengan serta-merta — tiada daftar di tempat lain. Tandakan, atau ikatan ke premium
  peringkat. Klien masih boleh membawa kunci mereka sendiri. → [Ciri AI](./features/ai.md)
- **Cabaran prop-firm.** Jalankan cabaran pedagang yang dibiayai dengan pelacakan ekuiti hidup dan peraturan yang dikuatkuasakan,
  dan caj untuk kemasukan. → [Peraturan prop-firm](./features/prop-firm.md)
- **Perniagaan salinan perdagangan.** Bayaran prestasi dan pasaran penyedia mengubah salinan perdagangan menjadi
  pendapatan. → [Bayaran prestasi](./features/copy-performance-fees.md) ·
  [Pasaran penyedia](./features/copy-provider-marketplace.md)
- **Peringkat ciri.** Tentukan ciri mana yang setiap segmen klien lihat dengan
  [togol ciri](./features/feature-toggles.md).

## Dikawal, boleh diaudit, berbilang penyewa

- **[Pematuhan](./features/compliance.md)** log memberikan anda jejak audit yang regulator anda akan minta.
- **[Pengesahan dua-faktor](./features/two-factor-auth.md)** boleh dibuat wajib setiap penempatan.
- **Penjenamaan setiap klien** — jalankan contoh yang terpisah berjenama per segmen, didorong dari kawalan
  pesawat anda sendiri. → [Penjenamaan berbilang penyewa setiap pelanggan](./white-label-for-business.md#multi-tenant-per-customer-branding)

## Bagaimana untuk memulai

1. Baca [Label-putih untuk perniagaan](./white-label-for-business.md) untuk rebrand 60 saat.
2. Tetapkan `App:Accounts:AllowedBrokers` kepada pembrokeran anda dan pilih [set ciri](./features/feature-toggles.md) anda.
3. [Sebarkan](./deployment/cloud.md) itu — Docker, Kubernetes, Azure, atau AWS.

Tidak mahu menjalankan infrastruktur sendiri? Penyedia pengehosan boleh mengoperasikan cMind yang terurus untuk anda
— arahkan mereka kepada [Untuk penyedia cloud & VPS](./for-cloud-providers.md).

## Bentuk peta jalan

cMind adalah sumber terbuka. Broker yang membina di atasnya mendapat suara yang lebih besar di mana ia pergi — minta
integrasi dan kawalan yang anda perlukan, dan sumbangkan mereka kembali melalui
[Panduan Menyumbang](./contributing.md).
