---
title: Deploy to the cloud
description: Deploy cMind to Azure, AWS, or Kubernetes. Which platform fits, prerequisites, and krok za krokem guides.
sidebar_position: 2
---

# Deploy to the cloud ☁️

Outgrown your laptop? Time to put cMind on real infrastructure. Good news: it&apos;s designed to
scale out with almost no operator ceremony — no ZooKeeper, no leader election, just replicas and a
database.

**The one thing to know up front:** the stateless tier (Web + MCP) runs happily on *any* container
platform, but **node agents need privileged Docker** (they build and run cTrader containers). That
rules out serverless runtimes like Azure Container Apps and AWS Fargate for the *agents* — run those
on [Kubernetes](./kubernetes.md), a VM, or EC2 and point them at your Web URL.

Pick your path:

- 🟦 **[Azure](./cloud-azure.md)** — Container Apps + Postgres Flexible Server (Bicep).
- 🟧 **[AWS](./cloud-aws.md)** — ECS Fargate + ALB + RDS (Terraform).
- ⎈ **[Kubernetes](./kubernetes.md)** — the Helm chart, works on AKS / EKS / anywhere.
- 📈 **[Scaling](./scaling.md)** — how it all scales and self-heals once it&apos;s up.

Stateless tier (Web + MCP) run on any container platform; Postgres = managed database.
**Node agents need privileged Docker (DinD)** — serverless container runtimes (Azure Container
Apps, AWS Fargate) block it. Run agents on Kubernetes ([kubernetes.md](kubernetes.md)) or
VM/EC2, point at Web URL.

| Cloud | Stateless tier | Database | Guide |
| ----- | -------------- | -------- | ----- |
| Azure | Container Apps (Bicep) | Postgres Flexible Server | [cloud-azure.md](cloud-azure.md) |
| AWS | ECS Fargate + ALB (Terraform) | RDS Postgres | [cloud-aws.md](cloud-aws.md) |

Common prerequisites, both:

1. Build + push three images to registry cloud can pull (`cmind-web`, `cmind-mcp`,
   `cmind-node-agent`).
2. Pick secrets: DB password, owner email/password, **discovery join token** (≥ 32 chars)
   shared by Web app + every node agent.
3. Deploy IaC (below), then bring node agents up separately (K8s/VM) with
   `NodeAgent__MainUrl` = deployed Web URL, `NodeAgent__JwtSecret` = join token.

Discovery, logging, probes behave same as local/K8s setups — see
[../operations/node-discovery.md](../operations/node-discovery.md) and
[../operations/logging.md](../operations/logging.md).