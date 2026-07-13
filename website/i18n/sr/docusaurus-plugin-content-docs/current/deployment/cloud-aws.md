---
description: "deploy/aws = Terraform модул: ECS Fargate (Web + MCP) иза ALB, RDS Postgres, CloudWatch логови."
---

# AWS развој — корак по корак

`deploy/aws` = Terraform модул: **ECS Fargate** (Web + MCP) иза **ALB**, **RDS Postgres**, CloudWatch логови.

## 1. Предуслови

- Terraform ≥ 1.5 + AWS верене (`aws configure` / env променљиве) са правима за направљање VPC-scoped
  ресурса, ECS, RDS, ALB, IAM.
- Три слике у регистру ECS може доставити (ECR, или GHCR јавне).

## 2. Иницијализирај

```bash
cd deploy/aws
terraform init
```

## 3. Применити

```bash
terraform apply \
  -var image_registry=ghcr.io/your-org/cmind \
  -var image_tag=1.0.0 \
  -var owner_email=you@example.com \
  -var owner_password='Change-Me-Str0ng!' \
  -var pg_password="$(openssl rand -hex 16)" \
