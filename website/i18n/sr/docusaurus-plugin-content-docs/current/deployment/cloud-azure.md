---
description: "deploy/azure/main.bicep снабдева stateless слој на Azure Container Apps плус Postgres Flexible Server + Log Analytics."
---

# Azure развој — корак по корак

`deploy/azure/main.bicep` снабдева stateless слој на **Azure Container Apps** плус **Postgres Flexible Server** + Log Analytics.

## 1. Предуслови

- Azure CLI (`az login` урађено), претплата, дозвола да направи групе ресурса.
- Три слике потиснуте у регистар Azure може доставити (нпр. GHCR јавне, или ACR).

## 2. Направи групу ресурса

```bash
az group create -n cmind-rg -l westeurope
```

## 3. Развој Bicep

```bash
az deployment group create -g cmind-rg -f deploy/azure/main.bicep \
  -p imageRegistry=ghcr.io/your-org/cmind imageTag=1.0.0 \
     ownerEmail=you@example.com \
     ownerPassword='Change-Me-Str0ng!' \
     pgPassword="$(openssl rand -hex 16)" \
     discoveryJoinToken="$(openssl rand -hex 24)"
```
