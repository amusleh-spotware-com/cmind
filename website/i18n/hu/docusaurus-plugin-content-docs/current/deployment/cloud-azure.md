---
title: Azure telepites
description: "Azure: Container Apps, Flexible Server Postgres, Application Insights, Log Analytics."
---

# Azure telepites (Container Apps + Flexible Server)

## Architektura

```
Internet
   │
   ▼
Azure Front Door / Application Gateway
   │
   ├──► cmind-web — Azure Container Apps (Blazor Server, SignalR)
   │
   └──► cmind-mcp — Azure Container Apps (MCP HTTP+SSE szerver)
            │
            ▼
       Azure Database for PostgreSQL - Flexible Server (zone-redundant)
            │
            ▼
       Azure Cache for Redis (SignalR backplane)
```

## Fő komponensek

### Container Apps

A Web és az MCP Container Apps-ként futnak, ami automatikus skálázást és zone-redundanciát biztosít. A Container Apps environment az összes konténert egy virtuális hálózaton belül tartja.

### Application Insights

A `deploy/azure/main.bicep` provisioningolja a **workspace-based Application Insights** komponenst, és átadja a kapcsolati karakterláncot a Web és az MCP számára `APPLICATIONINSIGHTS_CONNECTION_STRING`-ként. Ez eredményezi:

- **Traces + metrikák** natívan az Application Insights-ba áramlanak (Application Map, live metrics, end-to-end transaction search), `trace_id` alapján korrelálva.
- **Logs** (compact JSON a stdout-on) landolnak a **Log Analytics workspace-ben** ugyanabban a munkaterületben a Container Apps `appLogsConfiguration` révén, igy az `AppTraces` / `ContainerAppConsoleLogs_CL` összekapcsolható a trace id alapján.

Állítsd be az opcionalis `otlpEndpoint` Bicep paramétert, hogy egy külső collector-ra is szétoszd az adatokat.

### Flexible Server Postgres

Zone-redundant high availability, automatikus backup 35 napra. A kapcsolati karakterláncot az alkalmazás a `appdb` connection string-ként használja.

## Naplofüggőségek

Log Analytics query (JSON sor a `Log_s`-ben):

```kusto
ContainerAppConsoleLogs_CL
| extend log = parse_json(Log_s)
| where log["service.name"] == "cmind-web"
| project TimeGenerated, level = log["@l"], msg = log["@m"], trace_id = log.trace_id
```

## Telepítés lépései

```bash
# Azure CLI bejelentkezés
az login
az account set --subscription <subscription-id>

# Erőforráscsoport létrehozása
az group create --name cmind-rg --location eastus

# Telepítés a bicep fájllal
az deployment group create   --resource-group cmind-rg   --template-file deploy/azure/main.bicep   --parameters     location=eastus     ownerEmail=te@example.com     pgPassword=<secure-password>     ownerPassword=<secure-password>
```

Vagy Helm chart-ot használj:

```bash
helm upgrade --install cmind ../helm/cmind   --namespace cmind --create-namespace   --set image.repository=<acr>.azurecr.io/cmind   --set image.tag=<version>   --set secrets.pgPassword="<secure-password>"   --set secrets.ownerEmail="te@example.com"   --set secrets.ownerPassword="<secure-password>"   --set secrets.discoveryJoinToken="<min-32-char-secret>"
```

## Skálázás

A Container Apps automatikusan skáláz CPU és memória alapján. A másolási kereskedési ügynök a KEDA scaler-eket használja a Postgres-lekérdezéses skálázáshoz (futó profilok száma alapján).

## SignalR Redis backplane

Az Azure Cache for Redis a SignalR backplane-ként szolgál, hogy a Blazor Server körök több pod között is működjenek.
