---
slug: /for-cloud-providers
title: cMind untuk penyedia cloud & VPS
description: Mengapa penyedia cloud atau VPS perlu menawarkan pengehosan cMind yang terurus — produk yang sedia ada, dibezakan untuk pedagang algo, broker dan prop firm, dengan cara yang jelas untuk mendapatkan pengiraan, penjualan semula label-putih dan AI terurus.
keywords:
  - Pengehosan terurus
  - Penyedia VPS
  - Penyedia cloud
  - Pengehosan platform perdagangan
  - Penjual semula label-putih
  - Pengehosan AI terurus
sidebar_position: 7
---

# cMind untuk penyedia cloud & VPS 🖥️

Anda sudah menyewa pengiraan. cMind adalah produk sumber terbuka yang sedia ada yang anda boleh balut pengiraan itu
di sekitar: **tawarkan pengehosan cMind yang terurus** dan mendapatkan beban kerja bernilai tinggi, melekit, lapar pengiraan —
pedagang algoritma, broker, prop firm, dan komuniti perdagangan yang ingin platform berjalan
tanpa menjadi pasukan ops mereka sendiri.

:::tip[TL;DR]
Jalankan peringkat tanpa keadaan + Postgres + armada nod; berikan pelanggan URL berjenama. Mendapatkan wang
langganan, pengiraan, label-putih, dan AI. → [Sebarkan ke cloud](./deployment/cloud.md)
:::

## Mengapa tawarkan cMind terurus

- **Tiada kos pembinaan.** Ia adalah sumber terbuka, MIT-berlesen, dan sudah terdokumentasi, diuji, dan diperkontainerkan.
  Anda membungkus dan mengoperasikannya — anda tidak membinanya.
- **Produk yang dibezakan untuk niche yang menguntungkan.** Perdagangan algo adalah lapar pengiraan: ujian belakang dan
  nod hidup membakar CPU, yang *penggunaan yang boleh dibilangkan* anda sudah jual.
- **Pelanggan yang melekit.** Pedagang yang membina dan menjalankan strategi dalam platform tidak berputar santai.
- **Ubah kaveat menjadi penjualan naik.** cMind adalah hos-sendiri mengikut reka bentuk — untuk pelanggan yang "tidak mahu
  menjadi pasukan ops," *anda* adalah jawapannya.

## Siapa yang membeli cMind terurus dari anda

- **Kuantiti & pedagang individu** yang ingin diuji. → [Untuk pedagang](./for-traders.md)
- **Broker cTrader** menjalankan label-putih untuk pelanggan mereka. → [Untuk broker](./for-brokers.md)
- **Prop firm & perniagaan salinan perdagangan** yang memerlukan infrastruktur berjenama, boleh diaudit.

## Apa yang "cMind terurus" bermakna untuk dijalankan

Anda mengoperasikan tiga peringkat; pelanggan mendapat URL web yang berjenama:

| Peringkat | Apa itu | Di mana ia berjalan |
|---|---|---|
| Tanpa keadaan (Web + MCP) | Apl + API + pelayan MCP | Mana-mana platform bekas, autoskala |
| Pangkalan data | PostgreSQL | Postgres terurus (RDS / Pelayan Fleksibel / milik anda sendiri) |
| Armada nod | Binaan & jalankan bekas cTrader | **VM atau Kubernetes — perlu Docker istimewa** |

:::warning[Satu perkara untuk skop depan]
Ejen nod membina dan menjalankan bekas cTrader, jadi mereka memerlukan **Docker istimewa**. Itu menghalang
waktu jalan bekas tanpa pelayan (Azure Container Apps, AWS Fargate) *untuk ejen* — jalankan pada
[Kubernetes](./deployment/kubernetes.md), VM, atau EC2. Peringkat tanpa keadaan berjalan di mana-mana.
:::

Panduan penempatan sebenar, salin-tampal membuat ini konkret: [gambaran keseluruhan cloud](./deployment/cloud.md) ·
[Azure](./deployment/cloud-azure.md) · [AWS](./deployment/cloud-aws.md) ·
[Kubernetes](./deployment/kubernetes.md) · [Penskalaan](./deployment/scaling.md).

## Bagaimana anda mendapatkan wang itu

- **Langganan pengehosan terurus.** Pelan Starter / Pasukan / Perniagaan bulanan bersaiz oleh armada nod dan
  keserenakan ujian belakang.
- **Penggunaan & pengukuran pengiraan.** Jam ujian belakang bil, jam nod hidup, dan penyimpanan — semula jadi diukur
  oleh armada bekas yang anda sudah jalankan.
- **Peringkat penjual semula label-putih.** Caj lebih untuk rebrand penuh (logo, warna, PWA,
  `ShowSiteLink=false`) dan untuk membolehkan keupayaan premium melalui
  [togol ciri](./features/feature-toggles.md). → [Label-putih](./features/white-label.md)
- **AI terurus.** Kunci penyedia AI lalai bundle jadi setiap pengguna pelanggan mendapat AI tanpa persediaan, dan
  tandakan penggunaan — atau tawarkan bawa-milik-kunci. → [Ciri AI](./features/ai.md)
- **Prop-firm & pendapatan berkongsi salinan perdagangan.** Hos firma menjalankan cabaran dan bayaran prestasi dan
  ambil potongan platform. → [Prop-firm](./features/prop-firm.md) ·
  [Bayaran prestasi](./features/copy-performance-fees.md) ·
  [Pasaran penyedia](./features/copy-provider-marketplace.md)
- **Persediaan, pengenalan & SLA.** Pasang perkhidmatan profesional dan sokongan premium.

## Pola berbilang penyewa

- **Penempatan setiap penyewa (disyorkan).** Satu contoh berjenama setiap pelanggan — pengasingan yang kuat,
  penjenamaan dan pangkalan data setiap penyewa, token sambungan nod yang berbeza setiap penyewa. Penjenamaan dibaca daripada
  `IOptionsMonitor`, jadi setiap contoh membawa identiti sendiri.
  → [Penjenamaan berbilang penyewa](./white-label-for-business.md#multi-tenant-per-customer-branding) ·
  [Penemuan nod](./operations/node-discovery.md)
- **Pesawat kawalan bersama (lanjutan).** Jalankan banyak contoh daripada lapisan peruntukan anda sendiri, penjamuran
  penjenamaan dan ciri setiap penyewa secara programatik.

## Penggunaan pengukuran untuk pengebilan

Titik akhir **`GET /api/usage`** yang hanya pemilik/pentadbir mengembalikan ringkasan bacaan sahaja yang penyedia boleh jajak dan
bil pada — tanpa sebarang domain atau ketekunan baru, ia mengunjurkan keadaan sedia ada:

```json
{
  "users": { "total": 42 },
  "nodes": { "total": 6, "online": 5 },
  "instances": { "total": 1280, "backtestsRunning": 3, "runsRunning": 11 },
  "cbots": { "total": 210 },
  "tradingAccounts": { "total": 88 }
}
```

Jajaki ia setiap penempatan penyewa untuk mendorong harga berasaskan tempat duduk, berasaskan armada, atau berasaskan beban kerja. Sepasang dengan
[log & kebolehmataan](./operations/logging.md) untuk pengukuran pengiraan lebih halus.

## Menjaga margin dapat diramalkan

Nod penskalaan kepada permintaan, kongsi peringkat Postgres, dan autoskala peringkat tanpa keadaan. Permukaan operasi
anda perlukan sudah ada:

- [Penskalaan & penyembuhan diri](./deployment/scaling.md)
- [Log & kebolehmataan](./operations/logging.md)
- [Sandaran & pemulihan](./operations/backup-recovery.md)

## Bermula

1. Berdiri penempatan rujukan daripada [panduan cloud](./deployment/cloud.md).
2. Templat itu setiap penyewa (penjenamaan + token sambungan + DB) dan wayar pengebilan anda untuk penggunaan pengiraan.
3. Senaraikan itu — anda sekarang mempunyai platform perdagangan algo terurus untuk dijual.

## Sumbang kembali

Penyedia menjalankan cMind pada skala hit tepi tajam pertama. Aliran hulu pembaikan operasi anda dan
peningkatan IaC menjaga armada anda murah untuk dipertahankan — mulai dengan
[Panduan Menyumbang](./contributing.md).
