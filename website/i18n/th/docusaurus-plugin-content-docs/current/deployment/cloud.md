---
title: Deploy ไปยังคลาวด์
description: Deploy cMind ไป Azure, AWS หรือ Kubernetes ที่ platform fits prerequisites และ step-by-step guides
sidebar_position: 2
---

# Deploy ไปยังคลาวด์ ☁️

Outgrown แล็ปท็อปของคุณ? เวลาที่จะวาง cMind บนโครงสร้างพื้นฐานจริง ข่าวดี: มันถูกออกแบบมาให้ scale out ด้วยพิธีกรรมผู้ดำเนินการเกือบไม่มี — ไม่มี ZooKeeper ไม่มี leader election เพียงจำลองและฐานข้อมูล

**สิ่งหนึ่งที่ต้องรู้ล่วงหน้า:** ชั้น stateless (Web + MCP) ทำงานได้ดีบน *container* platform ใด ๆ แต่ **node agents ต้องใช้ Docker ที่มีสิทธิ์** (พวกมันสร้างและเรียกใช้ cTrader containers) ซึ่งขัดขวาง serverless runtimes เช่น Azure Container Apps และ AWS Fargate สำหรับ *agents* — รัน เหล่านั้นบน [Kubernetes](./kubernetes.md) VM หรือ EC2 และชี้ Web URL ของคุณ

เลือกเส้นทางของคุณ:

- 🟦 **[Azure](./cloud-azure.md)** — Container Apps + Postgres Flexible Server (Bicep)
- 🟧 **[AWS](./cloud-aws.md)** — ECS Fargate + ALB + RDS (Terraform)
- ⎈ **[Kubernetes](./kubernetes.md)** — Helm chart ทำงาน AKS / EKS / ที่ใดก็ได้
- 📈 **[Scaling](./scaling.md)** — วิธี ที่มันทั้งหมด scales และ self-heals เมื่อมันขึ้น

Stateless tier (Web + MCP) รัน container platform ใด ๆ; Postgres = managed database **Node agents ต้องใช้ privileged Docker (DinD)** — serverless container runtimes (Azure Container Apps AWS Fargate) block มัน รัน agents บน Kubernetes ([kubernetes.md](kubernetes.md)) หรือ VM/EC2 ชี้ Web URL

| Cloud | Stateless tier | Database | Guide |
| ----- | -------------- | -------- | ----- |
| Azure | Container Apps (Bicep) | Postgres Flexible Server | [cloud-azure.md](cloud-azure.md) |
| AWS | ECS Fargate + ALB (Terraform) | RDS Postgres | [cloud-aws.md](cloud-aws.md) |

Common prerequisites ทั้งสอง:

1. Build + push images สาม ไป registry cloud สามารถ pull (`cmind-web` `cmind-mcp` `cmind-node-agent`)
2. Pick secrets: DB password owner email/password **discovery join token** (≥ 32 chars) shared โดย Web app + ทุก node agent
3. Deploy IaC (ด้านล่าง) แล้ว นำ node agents ขึ้นแยกต่างหาก (K8s/VM) ด้วย `NodeAgent__MainUrl` = deployed Web URL `NodeAgent__JwtSecret` = join token

Discovery logging probes ทำงาน เดียวกับ local/K8s setups — ดู [../operations/node-discovery.md](../operations/node-discovery.md) และ [../operations/logging.md](../operations/logging.md)
