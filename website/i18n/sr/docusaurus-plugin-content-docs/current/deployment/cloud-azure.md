---
description: "deploy/azure/main.bicep провизионира без статуса слој на Azure Container Apps плус Postgres Flexible Server + Log Analytics."
---

# Azure развој — корак по корак

`deploy/azure/main.bicep` провизионира без статуса слој на **Azure Container Apps** плус **Postgres Flexible Server** + Log Analytics.

## 1. Предуслови

- Azure CLI (`az login` урађено), претплата, дозвола за креирање група ресурса.
- Три слике избачене у регистру Azure може вући (нпр. GHCR јавна, или ACR).

## 2. Направите групу ресурса

```bash
az group create -n cmind-rg -l westeurope
```

## 3. Развоја Bicep

```bash
az deployment group create -g cmind-rg -f deploy/azure/main.bicep \
  -p imageRegistry=ghcr.io/your-org/cmind imageTag=1.0.0 \
     ownerEmail=you@example.com \
     ownerPassword='Change-Me-Str0ng!' \
     pgPassword="$(openssl rand -hex 16)" \
     discoveryJoinToken="$(openssl rand -hex 24)"
```

Чини: Container Apps окружење, Web (повезни улаз), MCP (повезни улаз), Postgres Flexible Server + `appdb`, Log Analytics, **радни простор-базирано Application Insights** компонента. Откривање укључено за Web. Његова веза низа убачена у Web + MCP као `APPLICATIONINSIGHTS_CONNECTION_STRING`, тако да трагови + метрике извозирају домаће у App Insights док дневници остају у исти Log Analytics радни простор — нема колектора потребног. Проследи `-p otlpEndpoint=...` да *такође* напред у OTLP колектор.

## 4. Добијте URL-ова

```bash
az deployment group show -g cmind-rg -n main --query properties.outputs
# webUrl, mcpUrl
```

Отворите `webUrl`, пријавите се са власником (принуђена промена лозинке при првој пријави).

## 5. Додајте агенте чвора (одвојено)

Container Apps не може покренути привилегирани/DinD, па покрените агенте на другом месту, упутите на `webUrl`:

- **AKS** — развој Helm графикон ([kubernetes.md](kubernetes.md)) са `nodeAgent.privileged=true`, скала Web/MCP на 0 ако желиш само агент слој тамо.
- **VM / VMSS** — покрени `cmind-node-agent` слику `--privileged` са `NodeAgent:MainUrl=<webUrl>`, `NodeAgent:AdvertiseUrl=<vm reachable url>`, `NodeAgent:JwtSecret=<discoveryJoinToken>`.

Агенти само-регистрирају унутар једног интервала пулса — видети [../operations/node-discovery.md](../operations/node-discovery.md).

## 6. Проверити

```bash
az containerapp logs show -g cmind-rg -n cmind-web --tail 50   # компактан JSON дневници
curl -s <webUrl>/version
```

## Напомене производње

- Предова Web са Azure Front Door / App Gateway за TLS + WAF.
- Чување тајни у Key Vault; проследи стабилна Data Protection сертификат (`App__DataProtectionCertBase64` / `...Password`) тако да кључни прстен преживи репа рестарта.
- App Insights (трагови+метрике) + Log Analytics (дневници) жица аутоматски; корелирајте на `trace_id`. Видети [../operations/logging.md](../operations/logging.md#azure--application-insights--log-analytics).
- Поставити `otlpEndpoint` параметар (или `OTEL_EXPORTER_OTLP_ENDPOINT` на апликацијама) да *такође* напред у колектор.
- Container Apps `scale` правила (мин/макс) жица у Bicep.

## Агент копирања + Key Vault (S5)

`deploy/azure/main.bicep` такође провизионира **copy-agent** Container App хостинга `CopyEngineSupervisor` (`App:Copy:Enabled=true`, `App:Features:CopyTrading=true`) са **нема улаза** — радник мајстор дугоживи cTrader утичнице. Чита DB низ везника из **Azure Key Vault** тајне преко **корисник-доделене управљане идентичности** (Key Vault Secrets User улога) радије него обичан текст тајна. Свака репа `NodeName` подразумевано је њено име контејнера (јединствено), тако DB лease атрибути покреће профиле по реплици и две репе никад двоструко-домаћин један. Скала `minReplicas`/`maxReplicas` да додате капацитета копирања; DataProtection кључни прстен дељен кроз Postgres, тако да свака репа може дешифровати сачувани Open API токени. Излази: `copyAgentName`, `keyVaultName`.
