---
description: "deploy/aws = Terraform modülü: ALB arkasında ECS Fargate (Web + MCP), RDS Postgres, CloudWatch günlükleri."
---

# AWS konuşlandırması — adım adım

`deploy/aws` = Terraform modülü: **ALB** arkasında **ECS Fargate** (Web + MCP), **RDS Postgres**, CloudWatch günlükleri.

## 1. Ön koşullar

- Terraform ≥ 1.5 + AWS kimlik bilgileri (`aws configure` / env vars) VPC kapsamlı kaynaklar, ECS,
  RDS, ALB, IAM yapma hakkıyla.
- Kayıt defterinde ECS çekebilecek üç görüntü (ECR veya GHCR genel).

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

Şunları yapar: RDS Postgres (`appdb`), ECS kümesi, Web + MCP için Fargate hizmetleri, ALB (Web `/` 'de,
MCP `/mcp` 'de), güvenlik grupları, CloudWatch günlük grubu, **ADOT (OpenTelemetry için AWS Dağıtımı)
toplayıcı sidecar** her görevde. Uygulama OTLP'yi sidecar'a ihraç eder, izleri **X-Ray** 'a,
metrikleri **CloudWatch** 'a (EMF, `cmind` ad alanı) gönderir; günlükler `awslogs` sürücüsünde
kompakt JSON olarak kalır. Web için keşif aç. Görev rolü sidecar'ı X-Ray + CloudWatch yazma erişimine
veriyor — çalıştırmak için toplayıcı yok.

> Kısalık için hesabın **varsayılan VPC/alt ağları** kullanır. Üretim için, kendi VPC'sini, özel
> alt ağlarını, HTTPS dinleyiciyi (ACM sertifikası) kablolu.

## 4. URL'leri alın

```bash
terraform output web_url   # ALB kökü
terraform output mcp_url   # ALB /mcp
```

`web_url` 'yi açın, sahibi ile oturum açın (ilk oturum açımda zorla şifre değişikliği).

## 5. Düğüm aracılarını ekleyin (ayrı)

Fargate ayrıcalıklı/DinD'yi yasaklar, bu nedenle `web_url` 'yi gösteren başka yerde aracıları çalıştırın:

- **ECS on EC2** — `privileged = true` görev tanımları ile kapasite sağlayıcı `cmind-node-agent`
  çalıştırılıyor.
- **EKS** — Helm grafiği ([kubernetes.md](kubernetes.md)) ile `nodeAgent.privileged=true`.

`NodeAgent__MainUrl=<web_url>`, `NodeAgent__AdvertiseUrl=<agent reachable url>`,
`NodeAgent__JwtSecret=<discovery_join_token>` ayarlayın. Aracılar kendi kendini kaydeder — bkz.
[../operations/node-discovery.md](../operations/node-discovery.md).

## 6. Doğrula

```bash
aws logs tail /ecs/cmind --since 5m         # kompakt JSON günlükleri
curl -s "$(terraform output -raw web_url)/version"
```

## Üretim notları

- HTTPS dinleyicisi + ACM sertifikası ekleyin; ALB güvenlik grubunu sınırlayın.
- AWS Secrets Manager / SSM'de sırları saklayın, görev tanımı `secrets` aracılığıyla enjekte edin
  düz metin `environment` yerine.
- RDS Multi-AZ + yedeklemeleri etkinleştirin.
- İzler (X-Ray), metrikler (CloudWatch EMF), günlükler (CloudWatch Logs) ADOT sidecar aracılığıyla
  otomatik olarak kablolu; `trace_id` 'de ilişkilendirin. Bkz.
  [../operations/logging.md](../operations/logging.md#aws--x-ray--cloudwatch-adot-sidecar).
- Uygulama zaten `OTEL_EXPORTER_OTLP_ENDPOINT` 'i görev içi sidecar'a yönlendir; merkezileştirmeyi
  tercih ederseniz harici toplayıcıya yeniden yönlendirin.

## Kopya ticaret aracısı + Secrets Manager (S5)

`deploy/aws/copy-agent.tf` **kopya-aracı** ECS Fargate hizmetini `CopyEngineSupervisor` barındıran
ekler (`App:Copy:Enabled=true`, `App:Features:CopyTrading=true`) **ALB olmaksızın** — uzun ömürlü
cTrader soketlerini tutan işçi. DB bağlantı dizesi **AWS Secrets Manager** 'de depolanır, görev
`secrets` bloğu aracılığıyla enjekte edilir (yürütme rolü sadece bu sırrda `secretsmanager:GetSecretValue`
verilir), düz metin env değil. Her görevinin `NodeName` 'i konteyner adı varsayılanı (Fargate görevine
benzersiz), böylece DB kirasıyla çalışan profiller görev başına — iki görev hiçbir zaman birini çift
barındırmaz. Kopya kapasitesi eklemek için `copy_agent_count` 'ı ölçekleyin; DataProtection anahtar
halkası Postgres aracılığıyla paylaşılır, bu nedenle herhangi bir görev saklı Open API jetonlarını
şifre çözbilir.
