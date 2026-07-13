---
title: Развертайте в облаке
description: Развертайте cMind в Azure, AWS или Kubernetes. Какая платформа подходит, предусловия и пошаговые руководства.
sidebar_position: 2
---

# Развертайте в облаке ☁️

Переросли ваш ноутбук? Пора положить cMind на реальную инфраструктуру. Хорошая новость: это разработано для масштабирования с почти нулевой церемонией оператора — нет ZooKeeper, нет выборов лидера, просто реплики и база данных.

**Одна вещь, которую нужно знать заранее:** Stateless уровень (Web + MCP) очень хорошо работает на *любой* платформе контейнеров, но **агенты узлов нуждаются в привилегированном Docker** (они создают и запускают контейнеры cTrader). Это исключает бессерверные среды выполнения, такие как Azure Container Apps и AWS Fargate для *агентов* — запустите их на [Kubernetes](./kubernetes.md), VM или EC2 и укажите на ваш Web URL.

Выберите свой путь:

- 🟦 **[Azure](./cloud-azure.md)** — Container Apps + Postgres Flexible Server (Bicep).
- 🟧 **[AWS](./cloud-aws.md)** — ECS Fargate + ALB + RDS (Terraform).
- ⎈ **[Kubernetes](./kubernetes.md)** — диаграмма Helm, работает на AKS / EKS / везде.
- 📈 **[Масштабирование](./scaling.md)** — как это всё масштабируется и самовосстанавливается после запуска.

Stateless уровень (Web + MCP) работает на любой платформе контейнеров; Postgres = управляемая база данных.
**Агенты узлов нуждаются в привилегированном Docker (DinD)** — бессерверные среды выполнения контейнеров (Azure Container Apps, AWS Fargate) блокируют. Запустите агентов на Kubernetes ([kubernetes.md](kubernetes.md)) или VM/EC2, укажите на Web URL.

| Облако | Stateless уровень | База данных | Руководство |
| ----- | -------------- | -------- | ----- |
| Azure | Container Apps (Bicep) | Postgres Flexible Server | [cloud-azure.md](cloud-azure.md) |
| AWS | ECS Fargate + ALB (Terraform) | RDS Postgres | [cloud-aws.md](cloud-aws.md) |

Общие предусловия, оба:

1. Создайте + отправьте три образа в реестр облака, который может получить (`cmind-web`, `cmind-mcp`, `cmind-node-agent`).
2. Выберите секреты: пароль БД, электронная почта владельца/пароль, **маркер присоединения обнаружения** (≥ 32 символов) общий для Web приложения + каждого агента узла.
3. Развертайте IaC (ниже), затем отдельно поднимите агентов узлов (K8s/VM) с `NodeAgent__MainUrl` = развернутый Web URL, `NodeAgent__JwtSecret` = маркер присоединения.

Обнаружение, логирование, пробы работают так же как локальные/K8s установки — смотрите [../operations/node-discovery.md](../operations/node-discovery.md) и [../operations/logging.md](../operations/logging.md).
