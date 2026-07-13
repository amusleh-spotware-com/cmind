---
description: "deploy/aws = Terraform modülü: ECS Fargate (Web + MCP) ALB arkasında, RDS Postgres, CloudWatch günlükleri."
---

# AWS dağıtımı — adım adım

`deploy/aws` = Terraform modülü: **ECS Fargate** (Web + MCP) **ALB** arkasında, **RDS Postgres**, CloudWatch günlükleri.

## 1. Ön koşullar

- Terraform ≥ 1.5 + AWS kimlik bilgileri (`aws configure` / env vars) ile VPC kapsamlı kaynaklar, ECS, RDS, ALB, IAM oluşturma yetkisi.
- Kayıt defterinden çekebildiği üç görüntü (ECR veya GHCR ortak).

## 2. Başlat

```bash
cd deploy/aws
terraform init
```

## 3. Uygula

```bash
terraform apply \
  -var image_registry=ghcr.io/your-org/cmind \
  -var image_tag=1.0.0 \
  -var owner_email=you@example.com \
  -var owner_password='Change-Me-Str0ng!' \
  -var pg_password="$(openssl rand -hex 16)" \
  -var discovery_join_token="$(openssl rand -hex 24)"
```

Oluşturur: RDS Postgres (`appdb`), ECS kümesi, Web + MCP için Fargate hizmetleri, ALB (Web `/` da, MCP `/mcp` da), güvenlik grupları, CloudWatch günlük grubu, **her görevde ADOT (AWS OpenTelemetry Dağıtımı) toplayıcı kenar görevlisi**. Uygulama OTLP'yi kenar görevlisine aktarır, bu da izlemeleri **X-Ray** ye, metrikleri **CloudWatch** ye (EMF, ad alanı `cmind`) gönderir; günlükler `awslogs` sürücüsünde kompakt JSON olarak kalır. Web için bulma açık. Görev rolü toplayıcı için X-Ray + CloudWatch yazma erişimi verir — kendiniz çalıştıracak toplayıcı yok.

> Özlülük için hesabın **varsayılan VPC/alt ağları** kullanır. Üretim için kendi VPC, özel alt ağlar, HTTPS dinleyicisi (ACM sertifikası) bağlayın.

## 4. URL'leri al

```bash
terraform output web_url   # ALB kökü
terraform output mcp_url   # ALB /mcp
```

`web_url` aç, sahibi (ilk girişte zorunlu şifre değişikliği) ile oturum aç.

## 5. Düğüm aracılarını ekle (ayrı)

Fargate ayrıcalıklı/DinD'yi yasaklar, bu nedenle aracıları `web_url` noktasında başka yerlerde çalıştırın:

- **ECS on EC2** — `cmind-node-agent` çalıştıran `privileged = true` görev tanımları ile kapasite sağlayıcısı.
- **EKS** — Helm grafiği ([kubernetes.md](kubernetes.md)) `nodeAgent.privileged=true` ile.

Şunu ayarla: `NodeAgent__MainUrl=<web_url>`, `NodeAgent__AdvertiseUrl=<agent reachable url>`, `NodeAgent__JwtSecret=<discovery_join_token>`. Aracılar kendi kendini kaydettirir — bkz. [../operations/node-discovery.md](../operations/node-discovery.md).

## 6. Doğrula

```bash
aws logs tail /ecs/cmind --since 5m         # kompakt JSON günlükleri
curl -s "$(terraform output -raw web_url)/version"
```

## Üretim notları

- HTTPS dinleyicisi + ACM sertifikası ekle; ALB güvenlik grubunu kısıtla.
- Sırları AWS Secrets Manager / SSM'de sakla, görev tanımı `secrets` üzerinden enjekte et, düz metin `environment` yerine.
- RDS Multi-AZ + yedeklemeleri etkinleştir.
- İzlemeler (X-Ray), metrikler (CloudWatch EMF), günlükler (CloudWatch Logs) ADOT kenar görevlisi aracılığıyla otomatik olarak bağlanır; `trace_id` üzerinde ilişkilendir. Bkz. [../operations/logging.md](../operations/logging.md#aws--x-ray--cloudwatch-adot-sidecar).
- Uygulama zaten `OTEL_EXPORTER_OTLP_ENDPOINT`'i görev içi kenar görevlisine işaret eder; merkezi leştirmek isterseniz harici toplayıcıya yeniden işaret et.

## Kopya alım aracısı + Secrets Manager (S5)

`deploy/aws/copy-agent.tf` **kopya aracısı** ECS Fargate hizmetini ekler; `CopyEngineSupervisor` barındırır (`App:Copy:Enabled=true`, `App:Features:CopyTrading=true`), **ALB yok** — uzun ömürlü cTrader soketleri tutun. DB bağlantı dizesi **AWS Secrets Manager**'da saklanır, görevin `secrets` bloğu aracılığıyla enjekte edilir (yürütme rolü sadece o sırrı `secretsmanager:GetSecretValue` için verir), düz metin env değil. Her görevin `NodeName` varsayılan değeri kap adı (Fargate görevi başına benzersiz), bu nedenle DB kiralama öznitelikleri profilleri görev başına çalıştırır — iki görev hiçbir zaman birini çift barındırmaz. `copy_agent_count` kopyala kapasitesi eklemek için ölçeklendir; DataProtection anahtar halka Postgres üzerinden paylaşılır, bu nedenle herhangi bir görev depolanmış Open API belirteçlerini şifresini çözebilir.
