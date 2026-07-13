---
slug: /for-cloud-providers
title: cMind untuk cloud & penyedia VPS
description: Mengapa penyedia cloud atau VPS harus menawarkan hosting cMind terkelola — produk siap pakai dan terdeferensiasi untuk trader algo, broker dan prop firm, dengan cara yang jelas untuk monetisasi komputasi, reselling white-label dan AI terkelola.
keywords:
  - Hosting terkelola
  - Penyedia VPS
  - Penyedia cloud
  - Hosting platform perdagangan
  - Reseller white-label
  - Hosting AI terkelola
sidebar_position: 7
---

# cMind untuk cloud & penyedia VPS 🖥️

Anda sudah menyewa compute. cMind adalah produk open-source siap pakai yang dapat Anda bungkus compute itu di sekitar: **tawarkan hosting cMind terkelola** dan dapatkan beban kerja bernilai tinggi, sticky, haus compute —
trader algoritmik, broker, prop firm, dan komunitas perdagangan yang menginginkan platform berjalan
tanpa menjadi tim ops sendiri.

:::tip TL;DR
Jalankan tier stateless + Postgres + armada node; beri pelanggan URL bermerek. Monetisasi
langganan, compute, white-label, dan AI. → [Deploy ke cloud](./deployment/cloud.md)
:::

## Mengapa menawarkan cMind terkelola

- **Tidak ada cost build.** Itu open source, MIT-licensed, dan sudah didokumentasikan, diuji, dan containerized.
  Anda mengemas dan mengoperasikannya — Anda tidak membangunnya.
- **Produk yang terdeferensiasi untuk niche yang menguntungkan.** Perdagangan algo itu haus compute: backtest dan
  node live membakar CPU, yang *billable usage* yang sudah Anda jual.
- **Pelanggan sticky.** Trader yang membangun dan menjalankan strategi di dalam platform tidak churn secara santai.
- **Mengubah caveat menjadi upsell.** cMind adalah self-hosted by design — untuk pelanggan yang "tidak ingin
  menjadi tim ops," *Anda* adalah jawabannya.

## Siapa yang membeli cMind terkelola dari Anda

- **Quant & trader individu** yang ingin dihosting. → [Untuk trader](./for-traders.md)
- **Broker cTrader** menjalankan white-label untuk klien mereka. → [Untuk broker](./for-brokers.md)
- **Prop firm & bisnis copy-trading** yang membutuhkan infrastruktur bermerek, dapat diaudit.

## Apa arti "cMind terkelola" untuk dijalankan

Anda mengoperasikan tiga tier; pelanggan mendapat URL web bermerek:

| Tier | Apa itu | Di mana berjalan |
|---|---|---|
| Stateless (Web + MCP) | Aplikasi + API + server MCP | Platform container apa pun, autoscaled |
| Database | PostgreSQL | Managed Postgres (RDS / Flexible Server / milik Anda sendiri) |
| Armada node | Build & jalankan container cTrader | **VM atau Kubernetes — memerlukan privileged Docker** |

:::warning Satu hal untuk scope di depan
Agen node membangun dan menjalankan container cTrader, jadi mereka membutuhkan **privileged Docker**. Itu mengecualikan
serverless container runtime (Azure Container Apps, AWS Fargate) *untuk agent* — jalankan di
[Kubernetes](./deployment/kubernetes.md), VM, atau EC2. Tier stateless berjalan di mana saja.
:::

Panduan deployment nyata, copy-paste membuat ini konkret: [cloud overview](./deployment/cloud.md) ·
[Azure](./deployment/cloud-azure.md) · [AWS](./deployment/cloud-aws.md) ·
[Kubernetes](./deployment/kubernetes.md) · [Scaling](./deployment/scaling.md).

## Bagaimana Anda monetisasi

- **Langganan hosting terkelola.** Rencana Starter / Team / Business bulanan berukuran menurut armada node dan
  concurrency backtest.
- **Metering penggunaan & compute.** Tagihan backtest-jam, live-node-jam, dan storage — secara alami diukur
  oleh armada container yang Anda jalankan.
- **White-label reseller tier.** Charge lebih banyak untuk rebrand penuh (logo, warna, PWA,
  `ShowSiteLink=false`) dan untuk mengaktifkan kemampuan premium melalui
  [feature toggles](./features/feature-toggles.md). → [White-label](./features/white-label.md)
- **AI terkelola.** Bundle kunci penyedia AI default sehingga setiap pengguna pelanggan mendapat AI tanpa setup, dan
  markup penggunaan — atau tawarkan bring-your-own-key. → [Fitur AI](./features/ai.md)
- **Prop-firm & revenue share copy-trading.** Host firm menjalankan tantangan dan performance fee dan
  ambil platform cut. → [Prop-firm](./features/prop-firm.md) ·
  [Performance fees](./features/copy-performance-fees.md) ·
  [Provider marketplace](./features/copy-provider-marketplace.md)
- **Setup, onboarding & SLA.** Lampirkan layanan profesional dan dukungan premium.

## Pola multi-tenant

- **Deployment-per-tenant (recommended).** Satu instance bermerek per pelanggan — isolasi kuat,
  branding per-tenant dan database, token join node yang berbeda per tenant. Branding dibaca dari
  `IOptionsMonitor`, jadi setiap instance membawa identitasnya sendiri.
  → [Multi-tenant branding](./white-label-for-business.md#multi-tenant-per-customer-branding) ·
  [Node discovery](./operations/node-discovery.md)
- **Shared control plane (advanced).** Dorong banyak instance dari layer provisioning Anda sendiri, seeding
  branding dan fitur per tenant secara programmatic.

## Metering penggunaan untuk billing

Endpoint **`GET /api/usage`** owner/admin-only mengembalikan ringkasan read-only yang dapat dijajak penyedia dan
bill — tanpa persistence atau domain baru, itu memproyeksikan state yang ada:

```json
{
  "users": { "total": 42 },
  "nodes": { "total": 6, "online": 5 },
  "instances": { "total": 1280, "backtestsRunning": 3, "runsRunning": 11 },
  "cbots": { "total": 210 },
  "tradingAccounts": { "total": 88 }
}
```

Pollnya per tenant deployment untuk mendorong pricing berbasis kursi, berbasis fleet, atau berbasis workload. Pasang dengan
[logging & observability](./operations/logging.md) untuk metering compute yang lebih baik.

## Menjaga margin tetap dapat diprediksi

Skala node ke permintaan, bagikan tier Postgres, dan autoscale tier stateless. Permukaan operasional yang Anda butuhkan sudah ada:

- [Scaling & self-healing](./deployment/scaling.md)
- [Logging & observability](./operations/logging.md)
- [Backup & recovery](./operations/backup-recovery.md)

## Memulai

1. Berdiri deployment referensi dari [cloud guides](./deployment/cloud.md).
2. Template per tenant (branding + join token + DB) dan kawat billing Anda ke compute usage.
3. Daftarkan — Anda sekarang memiliki platform perdagangan algo terkelola untuk dijual.

## Berkontribusi kembali

Penyedia menjalankan cMind pada skala hit tepi tajam pertama. Upstream perbaikan operasional Anda dan
peningkatan IaC menjaga armada Anda murah untuk dipertahankan — mulai dengan
[panduan Contributing](./contributing.md).
