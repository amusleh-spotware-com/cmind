---
title: Implante na nuvem
description: Implante o cMind no Azure, AWS ou Kubernetes. Qual plataforma se ajusta, pré-requisitos e guias passo a passo.
sidebar_position: 2
---

# Implante na nuvem ☁️

Superou seu laptop? Hora de colocar o cMind em infraestrutura real. Boa notícia: está projetado para
escalar com quase nenhuma cerimônia do operador — sem ZooKeeper, sem eleição de líderes, apenas réplicas e um
banco de dados.

**A única coisa a saber antecipadamente:** a camada sem estado (Web + MCP) funciona muito bem em *qualquer* plataforma de contêiner, mas **agentes de nó precisam de Docker privilegiado** (eles compilam e executam contêineres cTrader). Isso
exclui tempos de execução sem servidor como Azure Container Apps e AWS Fargate para os *agentes* — execute aqueles
em [Kubernetes](./kubernetes.md), uma VM, ou EC2 e aponte-os para seu URL web.

Escolha seu caminho:

- 🟦 **[Azure](./cloud-azure.md)** — Container Apps + Postgres Flexible Server (Bicep).
- 🟧 **[AWS](./cloud-aws.md)** — ECS Fargate + ALB + RDS (Terraform).
- ⎈ **[Kubernetes](./kubernetes.md)** — o gráfico Helm, funciona em AKS / EKS / qualquer lugar.
- 📈 **[Escalabilidade](./scaling.md)** — como tudo escala e se autocura uma vez que esteja ativo.

A camada sem estado (Web + MCP) funciona em qualquer plataforma de contêiner; Postgres = banco de dados gerenciado.
**Agentes de nó precisam de Docker privilegiado (DinD)** — tempos de execução de contêiner sem servidor (Azure Container
Apps, AWS Fargate) bloqueiam. Execute agentes em Kubernetes ([kubernetes.md](kubernetes.md)) ou
VM/EC2, aponte para URL web.

| Nuvem | Camada sem estado | Banco de dados | Guia |
| ----- | -------------- | -------- | ----- |
| Azure | Container Apps (Bicep) | Postgres Flexible Server | [cloud-azure.md](cloud-azure.md) |
| AWS | ECS Fargate + ALB (Terraform) | RDS Postgres | [cloud-aws.md](cloud-aws.md) |

Pré-requisitos comuns, ambos:

1. Compile + envie três imagens para o registro que a nuvem pode extrair (`cmind-web`, `cmind-mcp`,
   `cmind-node-agent`).
2. Escolha segredos: senha do BD, e-mail/senha do proprietário, **token de junção de descoberta** (≥ 32 caracteres)
   compartilhado pelo aplicativo web + cada agente de nó.
3. Implante IaC (abaixo), depois traga agentes de nó separadamente (K8s/VM) com
   `NodeAgent__MainUrl` = URL web implantada, `NodeAgent__JwtSecret` = token de junção.

Descoberta, registro, sondagens se comportam igual aos setups locais/K8s — veja
[../operations/node-discovery.md](../operations/node-discovery.md) e
[../operations/logging.md](../operations/logging.md).
