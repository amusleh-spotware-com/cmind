---
title: Развој облаку
description: Развој cMind на Azure, AWS, или Kubernetes. Која платформа одговара, предуслови, и корак-по-корак водичи.
sidebar_position: 2
---

# Развој облаку ☁️

Перечиш своју лаптопа? Време је да стави cMind на прави инфраструктура. Добра вест: дизајниран је за
скалирање са скоро без оператера церемоније — без ZooKeeper, без лидер избора, само реплике и база.

**Једна ствар да знаш напред:** stateless слој (Web + MCP) трчи срећно на *било какав* контејнер
платформа, али **агенти чворова морају привилегован Docker** (граде и покрећу cTrader контејнере). То
искључује serverless окруженја као Azure Container Apps и AWS Fargate за *агенте* — покрени те
на [Kubernetes](./kubernetes.md), VM, или EC2 и укажи их на твој Web URL.

Пронађи своју путању:

- 🟦 **[Azure](./cloud-azure.md)** — Container Apps + Postgres Flexible Server (Bicep).
- 🟧 **[AWS](./cloud-aws.md)** — ECS Fargate + ALB + RDS (Terraform).
- ⎈ **[Kubernetes](./kubernetes.md)** — Helm шарта, ради на AKS / EKS / било где.
- 📈 **[Скалирање](./scaling.md)** — како се све скалира и само-лечи када буде горе.

Stateless слој (Web + MCP) трчи на bilo kojem контејнер платформи; Postgres = управљано база.
**Агенти чворова морају привилегован Docker (DinD)** — serverless контејнер окруженја (Azure Container
Apps, AWS Fargate) га блокирају. Покрени агенте на Kubernetes ([kubernetes.md](kubernetes.md)) или
VM/EC2, укажи на Web URL.

| Облак | Stateless слој | База | Водич |
| ----- | -------------- | -------- | ----- |
| Azure | Container Apps (Bicep) | Postgres Flexible Server | [cloud-azure.md](cloud-azure.md) |
| AWS | ECS Fargate + ALB (Terraform) | RDS Postgres | [cloud-aws.md](cloud-aws.md) |

Уобичајени предуслови, обоје:

1. Грађење + пушање три слике на регистар облак може доставити (`cmind-web`, `cmind-mcp`,
   `cmind-node-agent`).
2. Изабери тајни: DB лозинка, власник имејл/лозинка, **откривање спајање жетон** (≥ 32 знаци)
   дељено од Web апликација + сваком агенту чвора.
