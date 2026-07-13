---
description: "deploy/aws = Terraform module: ECS Fargate (الويب + MCP) خلف ALB و RDS Postgres و CloudWatch logs."
---

# نشر AWS - خطوة بخطوة

`deploy/aws` = وحدة Terraform: **ECS Fargate** (الويب + MCP) خلف **ALB** و **RDS Postgres** و CloudWatch logs.

## 1. المتطلبات الأساسية

- Terraform ≥ 1.5 + بيانات اعتماد AWS (`aws configure` / variables env) مع الحقوق لجعل موارد نطاق VPC و ECS و RDS و ALB و IAM.
- ثلاث صور في registry ECS يمكنها السحب (ECR أو GHCR public).

## 2. الإبداع

```bash
cd deploy/aws
terraform init
```

## 3. التطبيق

```bash
terraform apply \
  -var image_registry=ghcr.io/your-org/cmind \
  -var image_tag=1.0.0 \
  -var owner_email=you@example.com \
  -var owner_password='Change-Me-Str0ng!' \
  -var pg_password="$(openssl rand -hex 16)" \
  -var discovery_join_token="$(openssl rand -hex 24)"
```

يحدث: RDS Postgres (`appdb`) و ECS cluster و خدمات Fargate للويب + MCP و ALB (الويب على `/` و MCP على `/mcp`) و security groups و CloudWatch log group و **ADOT (AWS Distro for OpenTelemetry) collector sidecar** في كل مهمة. التطبيق يصدر OTLP إلى sidecar التي تشحن traces إلى **X-Ray** والمقاييس إلى **CloudWatch** (EMF و namespace `cmind`)؛ السجلات البقاء على محرك `awslogs` كـ JSON مضغوط. اكتشاف لويب. منحة دور المهمة sidecar X-Ray + CloudWatch write access — لا المجمع للتشغيل بنفسك.

> تستخدم **VPC الافتراضية للحساب/subnets** للإيجاز. للإنتاج، وكيل VPC الخاص و subnets خاصة و HTTPS listener (ACM cert).

## 4. الحصول على عناوين URL

```bash
terraform output web_url   # ALB root
terraform output mcp_url   # ALB /mcp
```
