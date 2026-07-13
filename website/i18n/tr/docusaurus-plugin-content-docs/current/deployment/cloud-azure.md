---
title: Azure'e Dağıtın
description: Bicep şablonlarıyla Azure'e cMind dağıtın — Container Apps, Postgres Flexible Server, Managed Identity.
sidebar_position: 6
---

# Azure'e Dağıtın

Container Apps + Postgres Flexible Server kullanarak.

## Ön Koşullar

- Azure aboneliği
- Azure CLI
- Bicep CLI

## Dağıtım

```bash
az group create --name cmind-rg --location eastus

az deployment group create \
  --resource-group cmind-rg \
  --template-file deploy/azure/main.bicep \
  --parameters \
    ownerEmail=admin@example.com \
    ownerPassword=SecurePass123
```

Bicep şablonu oluşturur:
- Container Apps Ortamı
- Postgres Flexible Server
- KeyVault (sırlar için)
- Application Insights

## Yapılandırma

Environment Variables in Container Apps:

```
App__Branding__ProductName=YourApp
App__Ai__ApiKey=sk-...
ConnectionStrings__Postgres=...
```

## Maliyetler

- Container Apps: ~$0.05/saat + işlem
- Postgres: ~$30/ay (B1s katmanı)
- Application Insights: Ücretsiz (günde 1 GB kadar)

Daha fazla: [Kubernetes →](./kubernetes.md)
