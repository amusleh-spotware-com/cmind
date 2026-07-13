---
description: "deploy/aws = Terraform module: ECS Fargate (Web + MCP) di belakang ALB, RDS Postgres, CloudWatch logs."
---

# Deployment AWS — step by step

`deploy/aws` = Terraform module: **ECS Fargate** (Web + MCP) di belakang **ALB**, **RDS Postgres**, CloudWatch logs.

## 1. Prerequisites

- Terraform ≥ 1.5 + AWS credential (`aws configure` / env var) dengan right untuk membuat VPC-scoped resource, ECS, RDS, ALB, IAM.
- Tiga image di registry ECS dapat pull (ECR, atau GHCR public).

## 2. Inisialisasi

```bash
cd deploy/aws
terraform init
```

## 3. Apply

```bash
terraform apply \
  -var image_registry=ghcr.io/your-org/cmind \
  -var image_tag=1.0.0 \
  -var owner_email=you@example.com \
  -var owner_password='Change-Me-Str0ng!' \
  -var pg_password="$(openssl rand -hex 16)" \
  -var discovery_join_token="$(openssl rand -hex 24)"
```

Buat: RDS Postgres (`appdb`), ECS cluster, Fargate service untuk Web + MCP, ALB (Web di `/`, MCP di `/mcp`), security group, CloudWatch log group, **ADOT (AWS Distro untuk OpenTelemetry) collector sidecar** dalam setiap task. App export OTLP ke sidecar, yang ship trace ke **X-Ray**, metric ke **CloudWatch** (EMF, namespace `cmind`); log tetap di `awslogs` driver sebagai compact JSON. Discovery on untuk Web. Task role grant sidecar X-Ray + CloudWatch write access — tidak ada collector untuk jalankan sendiri.

> Gunakan **default VPC/subnet** account untuk brevity. Untuk production, wire VPC sendiri, private subnet, HTTPS listener (ACM cert).

## 4. Dapatkan URL

```bash
terraform output web_url   # ALB root
terraform output mcp_url   # ALB /mcp
```
