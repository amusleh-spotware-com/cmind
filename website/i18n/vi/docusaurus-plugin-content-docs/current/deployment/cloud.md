---
title: Triển khai lên cloud
description: Triển khai cMind lên Azure, AWS, hoặc Kubernetes. Platform nào phù hợp, điều kiện tiên quyết, và hướng dẫn từng bước.
sidebar_position: 2
---

# Triển khai lên cloud ☁️

Vượt quá máy tính xách tay của bạn? Đã đến lúc đưa cMind lên cơ sở hạ tầng thực. Tin tốt: nó được thiết kế để scale out với gần như không có overhead từ operator — không ZooKeeper, không leader election, chỉ có replicas và database.

**Điều quan trọng cần biết trước tiên:** tầng stateless (Web + MCP) chạy tốt trên *bất kỳ* container platform nào, nhưng **node agents cần Docker có quyền cao** (chúng build và chạy cTrader containers). Điều đó loại trừ serverless runtimes như Azure Container Apps và AWS Fargate cho *agents* — chạy chúng trên [Kubernetes](./kubernetes.md), VM, hoặc EC2 và trỏ chúng tới Web URL của bạn.

Chọn đường đi của bạn:

- 🟦 **[Azure](./cloud-azure.md)** — Container Apps + Postgres Flexible Server (Bicep).
- 🟧 **[AWS](./cloud-aws.md)** — ECS Fargate + ALB + RDS (Terraform).
- ⎈ **[Kubernetes](./kubernetes.md)** — Helm chart, hoạt động trên AKS / EKS / bất kỳ nơi nào.
- 📈 **[Scaling](./scaling.md)** — cách nó scale và tự chữa lành một khi nó đã lên.

Tầng stateless (Web + MCP) chạy trên bất kỳ container platform nào; Postgres = managed database.
**Node agents cần Docker có quyền cao (DinD)** — serverless container runtimes (Azure Container Apps, AWS Fargate) chặn nó. Chạy agents trên Kubernetes ([kubernetes.md](kubernetes.md)) hoặc VM/EC2, trỏ tới Web URL.

| Cloud | Tầng stateless | Database | Hướng dẫn |
| ----- | -------------- | -------- | ----- |
| Azure | Container Apps (Bicep) | Postgres Flexible Server | [cloud-azure.md](cloud-azure.md) |
| AWS | ECS Fargate + ALB (Terraform) | RDS Postgres | [cloud-aws.md](cloud-aws.md) |

Điều kiện tiên quyết chung, cả hai:

1. Build + push ba images tới registry mà cloud có thể pull (`cmind-web`, `cmind-mcp`, `cmind-node-agent`).
2. Chọn secrets: DB password, owner email/password, **discovery join token** (≥ 32 chars) được chia sẻ bởi Web app + mỗi node agent.
3. Triển khai IaC (dưới), sau đó đưa node agents lên riêng biệt (K8s/VM) với `NodeAgent__MainUrl` = deployed Web URL, `NodeAgent__JwtSecret` = join token.

Discovery, logging, probes hoạt động giống như local/K8s setups — xem [../operations/node-discovery.md](../operations/node-discovery.md) và [../operations/logging.md](../operations/logging.md).
