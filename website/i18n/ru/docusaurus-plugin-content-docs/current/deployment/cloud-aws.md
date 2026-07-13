---
description: "deploy/aws = Terraform модуль: ECS Fargate (Web + MCP) за ALB, RDS Postgres, CloudWatch логи."
---

# Развертывание AWS — пошаг за шагом

`deploy/aws` = Terraform модуль: **ECS Fargate** (Web + MCP) за **ALB**, **RDS Postgres**, CloudWatch логи.

## 1. Предварительные требования

- Terraform ≥ 1.5 + учетные данные AWS (`aws configure` / переменные окружения) с правами на создание ресурсов в области VPC, ECS, RDS, ALB, IAM.
- Три образа в реестре, которые ECS может вытянуть (ECR, или GHCR публичный).

## 2. Инициализация

```bash
cd deploy/aws
terraform init
```

## 3. Применить

```bash
terraform apply \
  -var image_registry=ghcr.io/your-org/cmind \
  -var image_tag=1.0.0 \
  -var owner_email=you@example.com \
  -var owner_password='Change-Me-Str0ng!' \
  -var pg_password="$(openssl rand -hex 16)" \
  -var discovery_join_token="$(openssl rand -hex 24)"
```

Создает: RDS Postgres (`appdb`), кластер ECS, Fargate сервисы для Web + MCP, ALB (Web в `/`, MCP в `/mcp`), группы безопасности, группа логов CloudWatch, **ADOT (AWS Distro for OpenTelemetry) collector sidecar** в каждой задаче. Приложение экспортирует OTLP в sidecar, который отправляет трассы в **X-Ray**, метрики в **CloudWatch** (EMF, пространство имен `cmind`); логи остаются на драйвере `awslogs` в виде компактного JSON. Discovery включен для Web. Роль задачи предоставляет sidecar доступ на запись в X-Ray + CloudWatch — нет необходимости запускать collector самостоятельно.

> Для краткости использует **VPC/подсети по умолчанию** учетной записи. Для production подключите свои VPC, приватные подсети, HTTPS слушатель (сертификат ACM).

## 4. Получить URLs

```bash
terraform output web_url   # ALB root
terraform output mcp_url   # ALB /mcp
```

Откройте `web_url`, войдите как владелец (принудительная смена пароля при первом входе).

## 5. Добавить агентов узлов (отдельно)

Fargate запрещает privileged/DinD, поэтому запустите агентов в другом месте, указывая на `web_url`:

- **ECS на EC2** — capacity provider с `privileged = true` определениями задач, запускающих `cmind-node-agent`.
- **EKS** — диаграмма Helm ([kubernetes.md](kubernetes.md)) с `nodeAgent.privileged=true`.

Установите `NodeAgent__MainUrl=<web_url>`, `NodeAgent__AdvertiseUrl=<agent reachable url>`, `NodeAgent__JwtSecret=<discovery_join_token>`. Агенты автоматически регистрируются — см. [../operations/node-discovery.md](../operations/node-discovery.md).

## 6. Проверка

```bash
aws logs tail /ecs/cmind --since 5m         # компактные JSON логи
curl -s "$(terraform output -raw web_url)/version"
```

## Примечания для production

- Добавьте HTTPS слушатель + сертификат ACM; ограничьте группу безопасности ALB.
- Сохраните секреты в AWS Secrets Manager / SSM, инъектируйте через `secrets` определения задачи вместо открытого текста `environment`.
- Включите RDS Multi-AZ + резервные копии.
- Трассы (X-Ray), метрики (CloudWatch EMF), логи (CloudWatch Logs) подключаются автоматически через ADOT sidecar; коррелируйте по `trace_id`. См. [../operations/logging.md](../operations/logging.md#aws--x-ray--cloudwatch-adot-sidecar).
- Приложение уже указывает `OTEL_EXPORTER_OTLP_ENDPOINT` на встроенный в задачу sidecar; перенаправьте на внешний collector, если вы предпочитаете централизацию.

## Copy-trading агент + Secrets Manager (S5)

`deploy/aws/copy-agent.tf` добавляет сервис **copy-agent** ECS Fargate, размещающий `CopyEngineSupervisor` (`App:Copy:Enabled=true`, `App:Features:CopyTrading=true`) с **без ALB** — работник, держащий долгоживущие сокеты cTrader. Строка подключения БД хранится в **AWS Secrets Manager**, инъектируется через блок `secrets` задачи (роль выполнения предоставляет `secretsmanager:GetSecretValue` только на этот секрет), не открытый текст env. `NodeName` каждой задачи по умолчанию равен имени хоста контейнера (уникален на задачу Fargate), поэтому аренда БД характеризует профили запуска на задачу — две задачи никогда не размещают одно вместе. Масштабируйте `copy_agent_count` для добавления емкости копирования; кольцо ключей DataProtection делится через Postgres, поэтому любая задача может расшифровать сохраненные токены Open API.
