---
description: "deploy/aws = Terraform modulu: ECS Fargate (Web + MCP) ALB arkasinda, RDS Postgres, CloudWatch günlükleri."
---

# AWS Dagitimi - Adim Adim

`deploy/aws` = Terraform modulu: **ECS Fargate** (Web + MCP) **ALB** arkasinda, **RDS Postgres**, CloudWatch günlükleri.

## 1. Ön Kosullar

- Terraform >= 1.5 + AWS kimlik bilgileri (`aws configure` / env vars) VPC kapsamli kaynaklar, ECS, RDS, ALB, IAM haklariyla.
- ECS'in cekebilecegi uc görüntü kayit defterinde (ECR veya GHCR public).

## 2. Baslat

```bash
cd deploy/aws
terraform init
```

## 3. Uygula

```bash
terraform apply   -var image_registry=ghcr.io/your-org/cmind   -var image_tag=1.0.0   -var owner_email=you@example.com   -var owner_password='Change-Me-Str0ng!'   -var pg_password="$(openssl rand -hex 16)"   -var discovery_join_token="$(openssl rand -hex 24)"
```

Yapar: RDS Postgres (`appdb`), ECS cluster, Web + MCP icin Fargate servisleri, ALB (Web `/`, MCP `/mcp`), guvenlik gruplari, CloudWatch log grubu, her görevde **ADOT (AWS Distro for OpenTelemetry) collector sidecari**. Uygulama OTLP'yi sidecara aktarir, bu da izleri **X-Ray**'e, metrikleri **CloudWatch**'a (EMF, namespace `cmind`) gönderir; günlükler `awslogs` surucusunde kompakt JSON olarak kalir. Web'de keşif acik. Görev rolü sidecara X-Ray + CloudWatch yazma erişimi verir — calistiracak collector yok.

> Kisaligi icin hesabin **varsayilan VPC/subnetleri** kullanilir. Uretim icin, kendi VPC'nizi, özel subnetlerinizi, HTTPS listener'inizi (ACM sertifikasi) baglayin.

## 4. URL'leri Alin

```bash
terraform output web_url   # ALB root
terraform output mcp_url   # ALB /mcp
```

`web_url`'i acin, sahibiyle giris yapin (ilk giriste zorunlu parola degisikligi).

## 5. Node Aracilari Ekleyin (Ayrı)

Fargate ayricalikli/DinD'ye izin vermez, bu nedenle aracilari `web_url`'e isaret eden baska bir yerde calistirin:

- **ECS on EC2** — `privileged = true` görev tanimlariyla capacity provider, `cmind-node-agent` calistiran.
- **EKS** — Helm grafigi ([kubernetes.md](kubernetes.md)) ile `nodeAgent.privileged=true`.

`NodeAgent__MainUrl=<web_url>`, `NodeAgent__AdvertiseUrl=<araciya ulasilabilir url>`, `NodeAgent__JwtSecret=<discovery_join_token>` ayarlayin. Aracilar kendilerini kaydeder — bkz. [../operations/node-discovery.md](../operations/node-discovery.md).

## 6. Dogrula

```bash
aws logs tail /ecs/cmind --since 5m         # kompakt JSON günlükleri
curl -s "$(terraform output -raw web_url)/version"
```

## Uretim Notlari

- HTTPS listener + ACM sertifikasi ekleyin; ALB guvenlik grubunu kisitlayin.
- Gizlilikleri AWS Secrets Manager / SSM'de saklayin, görev tanimi `secrets` yerine duz metin `environment` ile enjekte edin.
- RDS Multi-AZ + yedeklemeleri etkinlestirin.
- Izler (X-Ray), metrikler (CloudWatch EMF), günlükler (CloudWatch Logs) ADOT sidecar uzerinden otomatik baglanir; `trace_id` ile iliskilendirin. Bkz. [../operations/logging.md](../operations/logging.md#aws--x-ray--cloudwatch-adot-sidecar).
- Uygulama zaten `OTEL_EXPORTER_OTLP_ENDPOINT`'i islem ici sidecara isaret eder; merkezi bir collector tercih ediyorsaniz yeniden isaret edin.

## Kopyalama-Ticaret Aracisi + Secrets Manager (S5)

`deploy/aws/copy-agent.tf` **copy-agent** ECS Fargate servisi ekler, `CopyEngineSupervisor`'u (`App:Copy:Enabled=true`, `App:Features:CopyTrading=true`) barindirir — **ALB yok** — uzun ömürlü cTrader soketlerini tutan worker. DB baglanti dizisi **AWS Secrets Manager**'da saklanir, görevin `secrets` blogu uzerinden enjekte edilir (yurutme rolü yalnizca o secret uzerinde `secretsmanager:GetSecretValue` ile donatilmis), duz metin env degil. Her görevin `NodeName` varsayilan olarak kapsayici ana bilgisayar adidir (her Fargate görevi icin benzersiz), böylece DB kira özniteligi calisan profilleri basina atar — iki görev asla ayni anda birini barindirmaz. `copy_agent_count`'u ölceklendirerek kopya kapasitesi ekleyin; DataProtection anahtar halkasi PostgreSQL uzerinden paylasilir, böylece her görev sakli Open API belirteclerini cozebilir.
