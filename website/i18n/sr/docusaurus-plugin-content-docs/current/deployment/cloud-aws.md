---
description: "deploy/aws = Terraform модул: ECS Fargate (Web + MCP) иза ALB, RDS Postgres, CloudWatch дневници."
---

# AWS развој — корак по корак

`deploy/aws` = Terraform модул: **ECS Fargate** (Web + MCP) иза **ALB**, **RDS Postgres**, CloudWatch дневници.

## 1. Предуслови

- Terraform ≥ 1.5 + AWS верификације (`aws configure` / променљивих окружења) са правима да направи VPC-опсежне
  ресурсе, ECS, RDS, ALB, IAM.
- Три слике у регистру ECS може вући (ECR, или GHCR јавна).

## 2. Иницијализирајте

```bash
cd deploy/aws
terraform init
```

## 3. Примени

```bash
terraform apply \
  -var image_registry=ghcr.io/your-org/cmind \
  -var image_tag=1.0.0 \
  -var owner_email=you@example.com \
  -var owner_password='Change-Me-Str0ng!' \
  -var pg_password="$(openssl rand -hex 16)" \
  -var discovery_join_token="$(openssl rand -hex 24)"
```

Чини: RDS Postgres (`appdb`), ECS кластер, Fargate услуге за Web + MCP, ALB (Web на `/`,
MCP на `/mcp`), безбедне групе, CloudWatch дневна група, **ADOT (AWS Distro за
OpenTelemetry) колектор боковни возни車** у сваком задатку. Апликација извозира OTLP у боковни, који шаље
трагове за **X-Ray**, метрике за **CloudWatch** (EMF, простор имена `cmind`); дневници остају на
`awslogs` возач као компактан JSON. Откривање укључено за Web. Задатак улога даје боковни
X-Ray + CloudWatch напиши приступ — нема колектора да водиш сам.

> Користи налог **подразумевано VPC/подмреже** за кратак облик. За производњу, жица властита VPC, приватна
> подмреже, HTTPS слушалац (ACM сертификат).

## 4. Добијте URL-ова

```bash
terraform output web_url   # ALB основа
terraform output mcp_url   # ALB /mcp
```

Отворите `web_url`, пријавите се са власником (принуђена промена лозинке при првој пријави).

## 5. Додајте агенте чвора (одвојено)

Fargate забрањује привилегирани/DinD, па покрените агенте на другом месту упутити на `web_url`:

- **ECS на EC2** — капацитета провајдер са `privileged = true` дефиниције задатака покреће
  `cmind-node-agent`.
- **EKS** — Helm графикон ([kubernetes.md](kubernetes.md)) са `nodeAgent.privileged=true`.

Поставити `NodeAgent__MainUrl=<web_url>`, `NodeAgent__AdvertiseUrl=<agent reachable url>`,
`NodeAgent__JwtSecret=<discovery_join_token>`. Агенти самo-регистрирају — видети
[../operations/node-discovery.md](../operations/node-discovery.md).

## 6. Проверити

```bash
aws logs tail /ecs/cmind --since 5m         # компактан JSON дневници
curl -s "$(terraform output -raw web_url)/version"
```

## Напомене производње

- Додајте HTTPS слушалац + ACM сертификат; ограничите ALB безбедну групу.
- Чување тајни у AWS Secrets Manager / SSM, убацити преко дефиниције задатка `secrets` уместо
  обичног текста `environment`.
- Омогућите RDS Multi-AZ + резервне копије.
- Трагови (X-Ray), метрике (CloudWatch EMF), дневници (CloudWatch Logs) жица аутоматски преко
  ADOT боковни; корелирајте на `trace_id`. Видети
  [../operations/logging.md](../operations/logging.md#aws--x-ray--cloudwatch-adot-sidecar).
- Апликација већ упутиће `OTEL_EXPORTER_OTLP_ENDPOINT` у боковни у-задатку; упутите у외 колектор
  ако желите да централизујете.

## Агент копирања + Secrets Manager (S5)

`deploy/aws/copy-agent.tf` додаје **copy-agent** ECS Fargate услугу хостинга `CopyEngineSupervisor`
(`App:Copy:Enabled=true`, `App:Features:CopyTrading=true`) са **без ALB** — радник мајстор дугоживи
cTrader утичнице. DB низ везника сачувана у **AWS Secrets Manager**, убачена кроз
дефиниција задатка `secrets` блок (извршни улога дао `secretsmanager:GetSecretValue` на само то тајну),
не обичан текст окружење. Сваки задатак `NodeName` подразумевано је његово име контејнера (јединствено по Fargate задатку), тако
DB лease атрибути покреће профиле по задатку — два задатка никад двоструко-домаћин један. Скала
`copy_agent_count` да додате капацитета копирања; DataProtection кључни прстен дељен кроз Postgres, тако да сваки задатак
може дешифровати сачувани Open API токени.
