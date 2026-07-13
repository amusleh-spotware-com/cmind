---
title: Nasaďte do cloudu
description: Nasaďte cMind do Azure, AWS, nebo Kubernetes. Která platforma vyhovuje, předpoklady, a step-by-step průvodce.
sidebar_position: 2
---

# Nasaďte do cloudu

Vyrostl z laptopem? Čas dát cMind na skutečnou infrastrukturu. Dobrá zpráva: je navržen k scale out s téměř žádným operator ceremoniálem — bez ZooKeeper, bez leader výběru, jen repliky a databází.

**Jedna věc vědět dopředu:** stateless vrstva (Web + MCP) běží šťastně na *jakékoliv* container platformě, ale **node agenti potřebují privilegovaný Docker** (staví a spouští cTrader kontejnery). To vylučuje serverless runtimes jako Azure Container Apps a AWS Fargate pro *agenty* — spustit ty na [Kubernetes](./kubernetes.md), VM, nebo EC2 a ukázat je na vaši Web URL.

Vyberte svou cestu:

- 🟦 **[Azure](./cloud-azure.md)** — Container Apps + Postgres Flexible Server (Bicep).
- 🟧 **[AWS](./cloud-aws.md)** — ECS Fargate + ALB + RDS (Terraform).
- ⎈ **[Kubernetes](./kubernetes.md)** — Helm chart, works na AKS / EKS / kdekoli.
- 📈 **[Scaling](./scaling.md)** — jak to všechno scale a self-heals jakmile je to nahoru.

Stateless vrstva (Web + MCP) běžet na jakékoliv container platformě; Postgres = spravovanou databází. **Node agenti potřebují privilegovaný Docker (DinD)** — serverless container runtimes (Azure Container Apps, AWS Fargate) to blokují. Spustit agenty na Kubernetes ([kubernetes.md](kubernetes.md)) nebo VM/EC2, bod na Web URL.

| Cloud | Stateless vrstva | Databáze | Průvodce |
| ----- | -------------- | -------- | ----- |
| Azure | Container Apps (Bicep) | Postgres Flexible Server | [cloud-azure.md](cloud-azure.md) |
| AWS | ECS Fargate + ALB (Terraform) | RDS Postgres | [cloud-aws.md](cloud-aws.md) |

Běžné předpoklady, obě:

1. Staví + push tři obrázky do registry cloud může táhnout (`cmind-web`, `cmind-mcp`, `cmind-node-agent`).
2. Vybrat tajemství: DB heslo, owner email/heslo, **discovery join token** (≥ 32 znaků) sdílený Web app + každý node agent.
3. Nasaďte IaC (níže), pak přiveďte node agenty nahoru zvlášť (K8s/VM) s `NodeAgent__MainUrl` = nasazený Web URL, `NodeAgent__JwtSecret` = join token.

Discovery, logging, probes chování stejné jako local/K8s nastavení — viz [../operations/node-discovery.md](../operations/node-discovery.md) a [../operations/logging.md](../operations/logging.md).
