---
title: Deploy ke cloud
description: Deploy cMind ke Azure, AWS, atau Kubernetes. Platform mana yang cocok, prerequisite, dan step-by-step guide.
sidebar_position: 2
---

# Deploy ke cloud

Keluar dari laptop? Waktu untuk menempatkan cMind pada infrastruktur real. Kabar baik: itu dirancang untuk scale out dengan hampir tidak ada operator ceremony — tidak ada ZooKeeper, tidak ada leader election, hanya replica dan database.

**Satu hal yang harus diketahui di depan:** stateless tier (Web + MCP) berjalan happily pada *platform container apa pun*, tetapi **node agent butuh privileged Docker** (mereka build dan jalankan container cTrader). Itu aturan out serverless runtime seperti Azure Container Apps dan AWS Fargate untuk *agent* — jalankan di [Kubernetes](./kubernetes.md), VM, atau EC2 dan arahkan ke Web URL Anda.

Pilih jalur Anda:

- 🟦 **[Azure](./cloud-azure.md)** — Container Apps + Postgres Flexible Server (Bicep).
- 🟧 **[AWS](./cloud-aws.md)** — ECS Fargate + ALB + RDS (Terraform).
- ⎈ **[Kubernetes](./kubernetes.md)** — Helm chart, bekerja di AKS / EKS / di mana saja.
- 📈 **[Scaling](./scaling.md)** — bagaimana semuanya scale dan self-heal setelah itu up.

Stateless tier (Web + MCP) berjalan di platform container apa pun; Postgres = managed database. **Node agent butuh privileged Docker (DinD)** — serverless container runtime (Azure Container Apps, AWS Fargate) membloknya. Jalankan agent di Kubernetes ([kubernetes.md](kubernetes.md)) atau VM/EC2, arahkan ke Web URL.

| Cloud | Stateless tier | Database | Guide |
| ----- | -------------- | -------- | ----- |
| Azure | Container Apps (Bicep) | Postgres Flexible Server | [cloud-azure.md](cloud-azure.md) |
| AWS | ECS Fargate + ALB (Terraform) | RDS Postgres | [cloud-aws.md](cloud-aws.md) |

Prerequisite umum, keduanya:

1. Build + push tiga image ke registry cloud dapat pull (`cmind-web`, `cmind-mcp`, `cmind-node-agent`).
2. Pilih secret: DB password, owner email/password, **discovery join token** (≥ 32 char) dibagikan oleh Web app + setiap node agent.
3. Deploy IaC (di bawah), kemudian bawa node agent up terpisah (K8s/VM) dengan `NodeAgent__MainUrl` = deployed Web URL, `NodeAgent__JwtSecret` = join token.

Discovery, logging, probe berperilaku sama seperti local/K8s setup — lihat [../operations/node-discovery.md](../operations/node-discovery.md) dan [../operations/logging.md](../operations/logging.md).
