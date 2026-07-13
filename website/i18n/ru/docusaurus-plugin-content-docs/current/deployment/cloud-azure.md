---
description: "deploy/azure/main.bicep подготавливает безгосударственный уровень на Azure Container Apps плюс Postgres Flexible Server + Log Analytics."
---

# Развертывание Azure — пошаг за шагом

`deploy/azure/main.bicep` подготавливает безгосударственный уровень на **Azure Container Apps** плюс **Postgres Flexible Server** + Log Analytics.

## 1. Предварительные требования

- Azure CLI (`az login` выполнено), подписка, разрешение на создание групп ресурсов.
- Три образа отправлены в реестр, который Azure может вытянуть (например, GHCR публичный или ACR).

## 2. Создать группу ресурсов

```bash
az group create -n cmind-rg -l westeurope
```

## 3. Развернуть Bicep

```bash
az deployment group create -g cmind-rg -f deploy/azure/main.bicep \
  -p imageRegistry=ghcr.io/your-org/cmind imageTag=1.0.0 \
     ownerEmail=you@example.com \
     ownerPassword='Change-Me-Str0ng!' \
     pgPassword="$(openssl rand -hex 16)" \
     discoveryJoinToken="$(openssl rand -hex 24)"
```

Создает: среду Container Apps, Web (внешний вход), MCP (внешний вход), Postgres Flexible Server + `appdb`, Log Analytics, компонент **Application Insights на базе workspace**. Discovery включен для Web. Его строка подключения инъектируется в Web + MCP как `APPLICATIONINSIGHTS_CONNECTION_STRING`, поэтому трассы + метрики экспортируются нативно в App Insights, а логи направляются в то же Log Analytics workspace — collector не требуется. Передайте `-p otlpEndpoint=...` для **также** пересылки на OTLP collector.

## 4. Получить URLs

```bash
az deployment group show -g cmind-rg -n main --query properties.outputs
# webUrl, mcpUrl
```

Откройте `webUrl`, войдите как владелец (принудительная смена пароля при первом входе).

## 5. Добавить агентов узлов (отдельно)

Container Apps не может запускать privileged/DinD, поэтому запустите агентов в другом месте, указывая на `webUrl`:

- **AKS** — развертывайте диаграмму Helm ([kubernetes.md](kubernetes.md)) с `nodeAgent.privileged=true`, масштабируйте Web/MCP до 0, если хотите только уровень агента.
- **VM / VMSS** — запустите образ `cmind-node-agent` с `--privileged` с `NodeAgent:MainUrl=<webUrl>`, `NodeAgent:AdvertiseUrl=<vm reachable url>`, `NodeAgent:JwtSecret=<discoveryJoinToken>`.

Агенты автоматически регистрируются в течение одного интервала сердцебиения — см. [../operations/node-discovery.md](../operations/node-discovery.md).

## 6. Проверка

```bash
az containerapp logs show -g cmind-rg -n cmind-web --tail 50   # компактные JSON логи
curl -s <webUrl>/version
```

## Примечания для production

- Фронтируйте Web с помощью Azure Front Door / App Gateway для TLS + WAF.
- Сохраняйте секреты в Key Vault; передавайте стабильный сертификат Data Protection (`App__DataProtectionCertBase64` / `...Password`) чтобы кольцо ключей выжило перезагрузку реплики.
- App Insights (трассы+метрики) + Log Analytics (логи) подключаются автоматически; коррелируйте по `trace_id`. См. [../operations/logging.md](../operations/logging.md#azure--application-insights--log-analytics).
- Установите параметр `otlpEndpoint` (или `OTEL_EXPORTER_OTLP_ENDPOINT` на приложениях) для **также** пересылки на collector.
- Container Apps правила `scale` (мин/макс) подключены в Bicep.

## Copy-trading агент + Key Vault (S5)

`deploy/azure/main.bicep` также подготавливает **copy-agent** Container App, размещающий `CopyEngineSupervisor` (`App:Copy:Enabled=true`, `App:Features:CopyTrading=true`) с **без входа** — работник, держащий долгоживущие сокеты cTrader. Читает строку подключения БД из секрета **Azure Key Vault** через **назначенное пользователем управляемое удостоверение** (роль Key Vault Secrets User) вместо открытого текста секрета. `NodeName` каждой реплики по умолчанию равен имени хоста контейнера (уникален), поэтому аренда БД характеризует профили запуска на реплику и две реплики никогда не размещают один вместе. Масштабируйте `minReplicas`/`maxReplicas` для добавления емкости копирования; кольцо ключей DataProtection делится через Postgres, поэтому любая реплика может расшифровать сохраненные токены Open API. Выходные данные: `copyAgentName`, `keyVaultName`.
