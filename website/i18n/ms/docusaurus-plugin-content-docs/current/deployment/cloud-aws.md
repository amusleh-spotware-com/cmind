---
description: "deploy/aws = Modul Terraform: ECS Fargate (Web + MCP) di sebalik ALB, RDS Postgres, log CloudWatch."
---

# Penempatan AWS — langkah demi langkah

`deploy/aws` = Modul Terraform: **ECS Fargate** (Web + MCP) di sebalik **ALB**, **RDS Postgres**, log CloudWatch.

## 1. Prasyarat

- Terraform ≥ 1.5 + bukti kelayakan AWS (`aws configure` / pemboleh ubah env) dengan hak untuk membuat sumber berskop VPC, ECS, RDS, ALB, IAM.
- Tiga imej dalam pendaftar yang ECS boleh tarik (ECR, atau GHCR awam).

## 2. Inisialisasi

```bash
cd deploy/aws
terraform init
```

## 3. Gunakan

```bash
terraform apply \
  -var image_registry=ghcr.io/your-org/cmind \
  -var image_tag=1.0.0 \
  -var owner_email=you@example.com \
  -var owner_password='Change-Me-Str0ng!' \
  -var pg_password="$(openssl rand -hex 16)" \
  -var discovery_join_token="$(openssl rand -hex 24)"
```

Membuat: RDS Postgres (`appdb`), kluster ECS, perkhidmatan Fargate untuk Web + MCP, ALB (Web di `/`, MCP di `/mcp`), kumpulan keselamatan, kumpulan log CloudWatch, **sidecar pengumpul ADOT (AWS Distro for OpenTelemetry)** dalam setiap tugas. Apl mengeksport OTLP ke sidecar, yang menghantar jejak ke **X-Ray**, metrik ke **CloudWatch** (EMF, ruang nama `cmind`); log kekal pada pemandu `awslogs` sebagai JSON padat. Penemuan aktif untuk Web. Peranan tugas memberikan akses tulis sidecar X-Ray + CloudWatch — tiada pengumpul untuk dijalankan sendiri.

> Menggunakan **VPC/subnet lalai akaun** untuk kesederhanaan. Untuk pengeluaran, wayar VPC sendiri, subnet peribadi, pendengar HTTPS (sijil ACM).

## 4. Dapatkan URL

```bash
terraform output web_url   # akar ALB
terraform output mcp_url   # ALB /mcp
```

Buka `web_url`, daftar masuk dengan pemilik (perubahan kata laluan terpaksa pada log masuk pertama).

## 5. Tambah ejen nod (berasingan)

Fargate melarang istimewa/DinD, jadi jalankan ejen di tempat lain yang menunjuk pada `web_url`:

- **ECS pada EC2** — penyedia kapasiti dengan takrifan tugas `privileged = true` menjalankan `cmind-node-agent`.
- **EKS** — Carta Helm ([kubernetes.md](kubernetes.md)) dengan `nodeAgent.privileged=true`.

Tetapkan `NodeAgent__MainUrl=<web_url>`, `NodeAgent__AdvertiseUrl=<agent reachable url>`, `NodeAgent__JwtSecret=<discovery_join_token>`. Ejen mendaftar sendiri — lihat [../operations/node-discovery.md](../operations/node-discovery.md).

## 6. Sahkan

```bash
aws logs tail /ecs/cmind --since 5m         # log JSON padat
curl -s "$(terraform output -raw web_url)/version"
```

## Catatan pengeluaran

- Tambah pendengar HTTPS + sijil ACM; hadkan kumpulan keselamatan ALB.
- Simpan rahsia dalam AWS Secrets Manager / SSM, suntik melalui takrifan tugas `secrets` dan bukannya pemboleh ubah persekitaran `environment` teks biasa.
- Dayakan RDS Multi-AZ + sandaran.
- Jejak (X-Ray), metrik (CloudWatch EMF), log (CloudWatch Logs) berwayar secara automatik melalui sidecar ADOT; korelasikan pada `trace_id`. Lihat [../operations/logging.md](../operations/logging.md#aws--x-ray--cloudwatch-adot-sidecar).
- Apl sudah menunjuk `OTEL_EXPORTER_OTLP_ENDPOINT` di sidecar dalam tugas; sela semula ke pengumpul luaran jika anda suka memusatkan.

## Ejen salinan perdagangan + Penurus Rahsia (S5)

`deploy/aws/copy-agent.tf` menambah perkhidmatan **copy-agent** ECS Fargate menganjurkan `CopyEngineSupervisor` (`App:Copy:Enabled=true`, `App:Features:CopyTrading=true`) dengan **tiada ALB** — pekerja memegang soket cTrader berduration panjang. Rentetan sambungan DB disimpan dalam **AWS Secrets Manager**, disuntik melalui blok tugas `secrets` (peranan pelaksanaan yang diberi `secretsmanager:GetSecretValue` pada rahsia itu saja), bukan env teks biasa. `NodeName` setiap tugas lalai ke nama hos kontena-nya (unik setiap tugas Fargate), jadi atribut pajakan DB menjalankan profil setiap tugas — dua tugas tidak pernah melayan dua host satu. Skala `copy_agent_count` untuk menambah kapasiti salinan; gelang kunci DataProtection dikongsi melalui Postgres, jadi mana-mana tugas boleh menguraikan token Open API yang disimpan.
