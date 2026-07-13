---
title: Distribuisci al cloud
description: Distribuisci cMind ad Azure, AWS o Kubernetes. Quale piattaforma si adatta, prerequisiti e guide passo dopo passo.
sidebar_position: 2
---

# Distribuisci al cloud

Superato il tuo laptop? È ora di mettere cMind su infrastrutture reali. Buone notizie: è progettato per scalare verso l'esterno con quasi nessuna cerimonia dell'operatore — no ZooKeeper, nessuna elezione dei leader, solo repliche e un database.

**L'unica cosa da sapere in anticipo:** il tier stateless (Web + MCP) funziona felicemente su *qualsiasi* piattaforma di contenitori, ma **gli agenti dei nodi hanno bisogno di Docker privilegiato** (costruiscono ed eseguono contenitori cTrader). Ciò esclude i runtime serverless come Azure Container Apps e AWS Fargate per gli *agenti* — eseguili su [Kubernetes](./kubernetes.md), una VM o EC2 e puntali al tuo URL Web.

Scegli il tuo percorso:

- 🟦 **[Azure](./cloud-azure.md)** — Container Apps + Postgres Flexible Server (Bicep).
- 🟧 **[AWS](./cloud-aws.md)** — ECS Fargate + ALB + RDS (Terraform).
- ⎈ **[Kubernetes](./kubernetes.md)** — la chart Helm, funziona su AKS / EKS / dovunque.
- 📈 **[Scaling](./scaling.md)** — come si scala e si auto-guarisce una volta che è attivo.

Il tier stateless (Web + MCP) viene eseguito su qualsiasi piattaforma di contenitori; Postgres = database gestito. **Gli agenti dei nodi hanno bisogno di Docker privilegiato (DinD)** — i runtime di contenitori serverless (Azure Container Apps, AWS Fargate) lo bloccano. Esegui gli agenti su Kubernetes ([kubernetes.md](kubernetes.md)) o VM/EC2, punta all'URL Web.

| Cloud | Tier stateless | Database | Guida |
| ----- | -------------- | -------- | ----- |
| Azure | Container Apps (Bicep) | Postgres Flexible Server | [cloud-azure.md](cloud-azure.md) |
| AWS | ECS Fargate + ALB (Terraform) | RDS Postgres | [cloud-aws.md](cloud-aws.md) |

Prerequisiti comuni, entrambi:

1. Build e push di tre immagini nel registro cloud che può pull (`cmind-web`, `cmind-mcp`, `cmind-node-agent`).
2. Scegli i segreti: password del database, email/password del proprietario, **discovery join token** (≥ 32 caratteri) condiviso da Web app + ogni agente di nodo.
3. Distribuisci IaC (sotto), quindi porta i nodi agenti verso l'alto separatamente (K8s/VM) con `NodeAgent__MainUrl` = URL Web distribuito, `NodeAgent__JwtSecret` = join token.

La scoperta, la registrazione e i probe si comportano uguali alle configurazioni locali/K8s — vedi [../operations/node-discovery.md](../operations/node-discovery.md) e [../operations/logging.md](../operations/logging.md).
