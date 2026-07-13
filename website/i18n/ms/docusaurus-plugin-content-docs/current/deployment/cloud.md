---
title: Sebarkan ke cloud
description: Sebarkan cMind ke Azure, AWS, atau Kubernetes. Platform mana yang sesuai, syarat-syarat, dan panduan langkah-demi-langkah.
sidebar_position: 2
---

# Sebarkan ke cloud ☁️

Melampaui laptop anda? Masa untuk meletakkan cMind di infrastruktur sebenar. Berita baik: ia dirancang untuk
berskalakan keluar dengan hampir tiada upacara pengendali — tiada ZooKeeper, tiada pemilihan pemimpin, hanya replika dan
pangkalan data.

**Satu perkara yang perlu tahu didepan:** peringkat tanpa keadaan (Web + MCP) berjalan dengan senang di *mana-mana* platform bekas,
tetapi **ejen nod memerlukan Docker istimewa** (mereka membina dan menjalankan bekas cTrader). Itu
menghalang waktu jalan tanpa pelayan seperti Azure Container Apps dan AWS Fargate untuk *ejen* — jalankan pada
[Kubernetes](./kubernetes.md), VM, atau EC2 dan arahkan mereka ke URL Web anda.

Pilih jalan anda:

- 🟦 **[Azure](./cloud-azure.md)** — Container Apps + Postgres Flexible Server (Bicep).
- 🟧 **[AWS](./cloud-aws.md)** — ECS Fargate + ALB + RDS (Terraform).
- ⎈ **[Kubernetes](./kubernetes.md)** — carta Helm, berfungsi di AKS / EKS / mana-mana.
- 📈 **[Penskalaan](./scaling.md)** — bagaimana ia semua berskalakan dan menyembuhkan diri setelah ia naik.

Peringkat tanpa keadaan (Web + MCP) berjalan di mana-mana platform bekas; Postgres = pangkalan data terurus.
**Ejen nod memerlukan Docker istimewa (DinD)** — waktu jalan bekas tanpa pelayan (Azure Container
Apps, AWS Fargate) menghalangnya. Jalankan ejen di Kubernetes ([kubernetes.md](kubernetes.md)) atau
VM/EC2, arahkan ke URL Web.

| Cloud | Peringkat tanpa keadaan | Pangkalan data | Panduan |
| ----- | -------------- | -------- | ----- |
| Azure | Container Apps (Bicep) | Postgres Flexible Server | [cloud-azure.md](cloud-azure.md) |
| AWS | ECS Fargate + ALB (Terraform) | RDS Postgres | [cloud-aws.md](cloud-aws.md) |

Syarat-syarat umum, kedua-duanya:

1. Binaan + tolak tiga imej ke pendaftar cloud boleh tarik (`cmind-web`, `cmind-mcp`,
   `cmind-node-agent`).
2. Pilih rahsia: kata laluan DB, e-mel pemilik/kata laluan, **token sambungan penemuan** (≥ 32 char)
   dikongsi oleh aplikasi Web + setiap ejen nod.
3. Sebarkan IaC (di bawah), kemudian bawa ejen nod naik secara berasingan (K8s/VM) dengan
   `NodeAgent__MainUrl` = URL Web yang disebarkan, `NodeAgent__JwtSecret` = token sambungan.

Penemuan, log, siasatan berkelakuan sama seperti persediaan tempatan/K8s — lihat
[../operations/node-discovery.md](../operations/node-discovery.md) dan
[../operations/logging.md](../operations/logging.md).
