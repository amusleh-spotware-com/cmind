---
description: "deploy/azure/main.bicep, Azure Container Apps artı Postgres Esnek Sunucu + Günlük Analitikleri sağlamlaştırır."
---

# Azure dağıtımı — adım adım

`deploy/azure/main.bicep` **Azure Container Apps** artı **Postgres Esnek Sunucu** + Günlük Analitikleri sağlamlaştırır.

## 1. Ön koşullar

- Azure CLI (`az login` yapıldı), abonelik, kaynak grupları oluşturma izni.
- Azure'un çekebildiği kayıt defterine üç görüntü itildi (örn. GHCR ortak veya ACR).

## 2. Kaynak grubu oluştur

```bash
az group create -n cmind-rg -l westeurope
```

## 3. Bicep'i dağıt

```bash
az deployment group create -g cmind-rg -f deploy/azure/main.bicep \
  -p imageRegistry=ghcr.io/your-org/cmind imageTag=1.0.0 \
     ownerEmail=you@example.com \
     ownerPassword='Change-Me-Str0ng!' \
     pgPassword="$(openssl rand -hex 16)" \
     discoveryJoinToken="$(openssl rand -hex 24)"
```

Oluşturur: Container Apps ortamı, Web (harici giriş), MCP (harici giriş), Postgres Esnek Sunucu + `appdb`, Günlük Analitikleri, **çalışma alanı temelli Application Insights** bileşeni. Web için bulma açık. Bağlantı dizesi Web + MCP'ye `APPLICATIONINSIGHTS_CONNECTION_STRING` olarak enjekte edilir, bu nedenle izlemeler + metrikler yerel olarak App Insights'a aktarılırken günlükler aynı Günlük Analitikleri çalışma alanına iner — toplayıcıya gerek yok. OTLP toplayıcıya *ayrıca* iletmek için `-p otlpEndpoint=...` geç.

## 4. URL'leri al

```bash
az deployment group show -g cmind-rg -n main --query properties.outputs
# webUrl, mcpUrl
```

`webUrl` aç, sahibi (ilk girişte zorunlu şifre değişikliği) ile oturum aç.

## 5. Düğüm aracılarını ekle (ayrı)

Container Apps ayrıcalıklı/DinD çalıştıramaz, bu nedenle aracıları başka yerlerde çalıştırıp `webUrl` noktasına işaret et:

- **AKS** — Helm grafiğini dağıt ([kubernetes.md](kubernetes.md)) `nodeAgent.privileged=true` ile, Web/MCP'yi isterseniz ölçeği 0'a indir.
- **VM / VMSS** — `cmind-node-agent` görüntüsünü `--privileged` ile çalıştır; `NodeAgent:MainUrl=<webUrl>`, `NodeAgent:AdvertiseUrl=<vm reachable url>`, `NodeAgent:JwtSecret=<discoveryJoinToken>`.

Aracılar bir sinyal süresi içinde kendi kendini kaydettirir — bkz. [../operations/node-discovery.md](../operations/node-discovery.md).

## 6. Doğrula

```bash
az containerapp logs show -g cmind-rg -n cmind-web --tail 50   # kompakt JSON günlükleri
curl -s <webUrl>/version
```

## Üretim notları

- Web'i Azure Front Door / App Gateway ile TLS + WAF için arkaya al.
- Sırları Anahtar Kasası'nda sakla; veri koruma sertifikası sabit tutun (`App__DataProtectionCertBase64` / `...Password`), bu nedenle anahtar halka çoğaltma yeniden başlatmalarında kalır.
- App Insights (izlemeler+metrikler) + Günlük Analitikleri (günlükler) otomatik olarak bağlanır; `trace_id` üzerinde ilişkilendir. Bkz. [../operations/logging.md](../operations/logging.md#azure--application-insights--log-analytics).
- `otlpEndpoint` parametresini ayarla (veya uygulamalarda `OTEL_EXPORTER_OTLP_ENDPOINT`) toplayıcıya *ayrıca* iletmek için.
- Container Apps `scale` kuralları (min/max) Bicep'te bağlı.

## Kopya alım aracısı + Anahtar Kasası (S5)

`deploy/azure/main.bicep` ayrıca **kopya aracısı** Container App'i sağlamlaştırır; `CopyEngineSupervisor` barındırır (`App:Copy:Enabled=true`, `App:Features:CopyTrading=true`), **giriş yok** — uzun ömürlü cTrader soketleri tutun. DB bağlantı dizesini **Anahtar Kasası**'ndan okur; **kullanıcı tarafından atanan yönetilen kimlik** (Anahtar Kasası Sırları Kullanıcısı rolü) aracılığıyla, satır içi düz metin sırrı yerine. Her çoğaltmanın `NodeName` varsayılan değeri kap adı (benzersiz), bu nedenle DB kiralama çoğaltma başına profilleri öznitelendirir ve iki çoğaltma hiçbir zaman birini çift barındırmaz. `minReplicas`/`maxReplicas`'ı kopyala kapasitesi eklemek için ölçeklendir; DataProtection anahtar halka Postgres üzerinden paylaşılır, bu nedenle herhangi bir çoğaltma depolanmış Open API belirteçlerini şifresini çözebilir. Çıktılar: `copyAgentName`, `keyVaultName`.
