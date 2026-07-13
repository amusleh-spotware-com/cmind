---
description: "deploy/azure/main.bicep provision stateless tier pada Azure Container Apps plus Postgres Flexible Server + Log Analytics."
---

# Deployment Azure — step by step

`deploy/azure/main.bicep` provision stateless tier pada **Azure Container Apps** plus **Postgres Flexible Server** + Log Analytics.

## 1. Prerequisites

- Azure CLI (`az login` done), subscription, permission untuk create resource group.
- Tiga image pushed ke registry Azure dapat pull (mis. GHCR public, atau ACR).

## 2. Buat resource group

```bash
az group create -n cmind-rg -l westeurope
```

## 3. Deploy Bicep

```bash
az deployment group create -g cmind-rg -f deploy/azure/main.bicep \
  -p imageRegistry=ghcr.io/your-org/cmind imageTag=1.0.0 \
     ownerEmail=you@example.com \
     ownerPassword='Change-Me-Str0ng!' \
     pgPassword="$(openssl rand -hex 16)" \
     discoveryJoinToken="$(openssl rand -hex 24)"
```

Ciptakan: Container Apps environment, Web (external ingress), MCP (external ingress), Postgres Flexible Server + `appdb`, Log Analytics, **workspace-based Application Insights** component. Discovery on untuk Web. Koneksi string-nya injected ke Web + MCP sebagai `APPLICATIONINSIGHTS_CONNECTION_STRING`, jadi trace + metric export secara native ke App Insights sementara log landing di Log Analytics workspace yang sama — tidak ada collector dibutuhkan. Pass `-p otlpEndpoint=...` untuk *juga* forward ke OTLP collector.

## 4. Dapatkan URL

```bash
az deployment group show -g cmind-rg -n main --query properties.outputs
# webUrl, mcpUrl
```

Buka `webUrl`, sign in dengan owner (dipaksa password change pada first login).

## 5. Tambahkan node agent (terpisah)

Container Apps tidak dapat jalankan privileged/DinD, jadi jalankan agent tempat lain, arahkan ke `webUrl`:

- **AKS** — deploy Helm chart ([kubernetes.md](kubernetes.md)) dengan `nodeAgent.privileged=true`, scale Web/MCP ke 0 jika hanya ingin agent tier di sana.
- **VM / VMSS** — jalankan image `cmind-node-agent` `--privileged` dengan `NodeAgent:MainUrl=<webUrl>`, `NodeAgent:AdvertiseUrl=<vm reachable url>`, `NodeAgent:JwtSecret=<discoveryJoinToken>`.

Agent self-register dalam satu heartbeat interval — lihat [../operations/node-discovery.md](../operations/node-discovery.md).
