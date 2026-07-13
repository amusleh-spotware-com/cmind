---
description: "deploy/azure/main.bicep durumsuz katmanı Azure Container Apps artı Postgres Flexible Server + Log Analytics'te sağlar."
---

# Azure konuşlandırması — adım adım

`deploy/azure/main.bicep` durumsuz katmanı **Azure Container Apps** artı **Postgres Flexible Server** + Log Analytics'te sağlar.

## 1. Ön koşullar

- Azure CLI (`az login` bitti), abonelik, kaynak grubu oluşturma izni.
- Azure çekebileceği kayıt defterine itilen üç görüntü (örneğin GHCR genel veya ACR).

## 2. Bir kaynak grubu oluştur

```bash
az group create -n cmind-rg -l westeurope
```

## 3. Bicep'ı konuşlandır

```bash
az deployment group create -g cmind-rg -f deploy/azure/main.bicep \
  -p imageRegistry=ghcr.io/your-org/cmind imageTag=1.0.0 \
     ownerEmail=you@example.com \
     ownerPassword='Change-Me-Str0ng!' \
     pgPassword="$(openssl rand -hex 16)" \
     discoveryJoinToken="$(openssl rand -hex 24)"
```

Oluşturur: Container Apps ortamı, Web (harici giriş), MCP (harici giriş), Postgres Flexible Server + `appdb`, Log Analytics, **çalışma alanı tabanlı Application Insights** bileşeni. Web için keşif aç. Bağlantı dizesi Web + MCP'ye `APPLICATIONINSIGHTS_CONNECTION_STRING` olarak enjekte edilir, bu nedenle izler + metrikler App Insights'a doğal olarak dışa aktarılırken günlükler aynı Log Analytics çalışma alanında yer alır — toplayıcı gerekli değildir. OTLP toplayıcısına *ayrıca* iletmek için `-p otlpEndpoint=...` geçirin.

## 4. URL'leri alın

```bash
az deployment group show -g cmind-rg -n main --query properties.outputs
# webUrl, mcpUrl
```

`webUrl` 'yi açın, sahibi ile oturum açın (ilk oturum açımda zorla şifre değişikliği).

## 5. Düğüm aracılarını ekleyin (ayrı)

Container Apps ayrıcalıklı/DinD'yi çalıştıramaz, bu nedenle başka yerde aracıları çalıştırın, `webUrl` 'ye yönlendirin:

- **AKS** — Helm grafiğini ([kubernetes.md](kubernetes.md)) `nodeAgent.privileged=true` ile konuşlandırın, orada sadece aracı katmanı isterseniz Web/MCP'yi 0'a ölçekleyin.
- **VM / VMSS** — `cmind-node-agent` görüntüsünü `NodeAgent:MainUrl=<webUrl>`, `NodeAgent:AdvertiseUrl=<vm reachable url>`, `NodeAgent:JwtSecret=<discoveryJoinToken>` ile `--privileged` ile çalıştırın.

Aracılar bir kalp atışı aralığı içinde kendi kendini kaydeder — bkz. [../operations/node-discovery.md](../operations/node-discovery.md).

## 6. Doğrula

```bash
az containerapp logs show -g cmind-rg -n cmind-web --tail 50   # kompakt JSON günlükleri
curl -s <webUrl>/version
```

## Üretim notları

- Web'i TLS + WAF için Azure Front Door / App Gateway ile ön koy.
