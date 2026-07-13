---
title: Deploy do cloud
description: Nasaďte cMind na Azure, AWS alebo Kubernetes. Ktorá platforma sedí, prerequisity a step-by-step sprievodca.
sidebar_position: 2
---

# Deploy do cloud ☁️

Prerastli ste svoj notebook? Čas dať cMind na skutočnú infraštruktúru. Dobrá správa: je to navrhnuté na
scale out s takmer žiadnym operator ceremony — žiadny ZooKeeper, žiadna leader election, len repliky a databáza.

**Jedna vec na vedomie up front:** stateless tier (Web + MCP) beží šťastne na *akejkoľvek* container
platform, ale **node agenti potrebujú privileged Docker** (stavajú a spúšťajú cTrader kontajnery). To
vylučuje serverless runtimes ako Azure Container Apps a AWS Fargate pre *agents* — spustite tie
na [Kubernetes](./kubernetes.md), VM alebo EC2 a nasmerujte ich na vašu Web URL.

Vyberte si svoju cestu:

- 🟦 **[Azure](./cloud-azure.md)** — Container Apps + Postgres Flexible Server (Bicep).
- 🟧 **[AWS](./cloud-aws.md)** — ECS Fargate + ALB + RDS (Terraform).
- ⎈ **[Kubernetes](./kubernetes.md)** — Helm chart, funguje na AKS / EKS / kdekoľvek.
- 📈 **[Scaling](./scaling.md)** — ako sa to všetko škáluje a self-heals keď je to hore.

Stateless tier (Web + MCP) spúšťame na akejkoľvek container platform; Postgres = managed databáza.
**Node agenti potrebujú privileged Docker (DinD)** — serverless container runtimes (Azure Container
Apps, AWS Fargate) to blokujú. Spustite agentov na Kubernetes ([kubernetes.md](kubernetes.md)) alebo
VM/EC2, nasmerujte na Web URL.

| Cloud | Stateless tier | Database | Sprievodca |
| ----- | -------------- | -------- | ----- |
| Azure | Container Apps (Bicep) | Postgres Flexible Server | [cloud-azure.md](cloud-azure.md) |
| AWS | ECS Fargate + ALB (Terraform) | RDS Postgres | [cloud-aws.md](cloud-aws.md) |

Spoločné prerequisity, oboje:

1. Build + push tri obrazy do registry cloud môže pull (`cmind-web`, `cmind-mcp`,
   `cmind-node-agent`).
2. Vyberte si tajomstvá: DB heslo, owner email/heslo, **discovery join token** (≥ 32 chars)
   zdieľaný Web app + každý node agent.
3. Nasaďte IaC (nižšie), potom spustite node agenti oddelene (K8s/VM) s
   `NodeAgent__MainUrl` = nasadená Web URL, `NodeAgent__JwtSecret` = join token.

Discovery, logging, probes sa správajú rovnako ako local/K8s setupy — pozrite
[../operations/node-discovery.md](../operations/node-discovery.md) a
[../operations/logging.md](../operations/logging.md).
