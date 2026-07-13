---
description: "deploy/aws = modul Terraform: ECS Fargate (Web + MCP) di belakang ALB, RDS Postgres, log CloudWatch."
---

# Deploy AWS — langkah demi langkah

`deploy/aws` = modul Terraform: **ECS Fargate** (Web + MCP) di belakang **ALB**, **RDS Postgres**, log CloudWatch.

## 1. Prasyarat

- Terraform ≥ 1.5 + kredensial AWS (`aws configure` / variabel env) dengan hak untuk membuat
  sumber daya VPC-scoped, ECS, RDS, ALB, IAM.
- Tiga image di registry ECS yang dapat ditarik (ECR, atau GHCR public).

## 2. Inisialisasi

```bash
cd deploy/aws
terraform init
```

## 3. Apply

```bash
terraform apply \
  -var image_registry=ghcr.io/your-org/cmind \
  -var image_tag=1.0.0 \
  -var owner_email=you@example.com \
  -var owner_password='Change-Me-Str0ng!' \
  -var pg_password="$(openssl rand -hex 16)" \
  -var discovery_join_token="$(openssl rand -hex 24)"
```

Membuat: RDS Postgres (`appdb`), cluster ECS, layanan Fargate untuk Web + MCP, ALB (Web di `/`,
MCP di `/mcp`), grup keamanan, grup log CloudWatch, **sidecar collector ADOT (AWS Distro for
OpenTelemetry)** di setiap tugas. Aplikasi mengekspor OTLP ke sidecar, yang mengirim
traces ke **X-Ray**, metrik ke **CloudWatch** (EMF, namespace `cmind`); log tetap di
driver `awslogs` sebagai JSON kompak. Discovery aktif untuk Web. Peran tugas memberikan sidecar
akses tulis X-Ray + CloudWatch — tidak perlu menjalankan collector sendiri.

> Menggunakan **VPC/subnet default** akun untuk singkat. Untuk produksi, gunakan VPC sendiri, subnet
> privat, listener HTTPS (sertifikat ACM).

## 4. Dapatkan URL

```bash
terraform output web_url   # root ALB
terraform output mcp_url   # ALB /mcp
```

Buka `web_url`, masuk dengan owner (perubahan password wajib pada login pertama).

## 5. Tambahkan agent node (terpisah)

Fargate tidak mengizinkan privileged/DinD, jadi jalankan agent di tempat lain yang menunjuk ke `web_url`:

- **ECS on EC2** — capacity provider dengan definisi tugas `privileged = true` yang menjalankan
  `cmind-node-agent`.
- **EKS** — Helm chart ([kubernetes.md](kubernetes.md)) dengan `nodeAgent.privileged=true`.

Set `NodeAgent__MainUrl=<web_url>`, `NodeAgent__AdvertiseUrl=<url yang可达 agent>`,
`NodeAgent__JwtSecret=<discovery_join_token>`. Agent mendaftarkan diri secara otomatis — lihat
[../operations/node-discovery.md](../operations/node-discovery.md).

## 6. Verifikasi

```bash
aws logs tail /ecs/cmind --since 5m         # log JSON kompak
curl -s "$(terraform output -raw web_url)/version"
```

## Catatan produksi

- Tambahkan listener HTTPS + sertifikat ACM; batasi grup keamanan ALB.
- Simpan secret di AWS Secrets Manager / SSM, masukkan melalui blok `secrets` definisi tugas, bukan
  environment plaintext.
- Aktifkan RDS Multi-AZ + backup.
- Traces (X-Ray), metrik (CloudWatch EMF), log (CloudWatch Logs) terhubung otomatis melalui
  sidecar ADOT; korelasikan dengan `trace_id`. Lihat
  [../operations/logging.md](../operations/logging.md#aws--x-ray--cloudwatch-adot-sidecar).
- Aplikasi sudah menunjuk `OTEL_EXPORTER_OTLP_ENDPOINT` ke sidecar dalam tugas; arahkan ulang ke
  collector eksternal jika ingin sentralisasi.

## Copy-trading agent + Secrets Manager (S5)

`deploy/aws/copy-agent.tf` menambahkan layanan ECS Fargate **copy-agent** yang menghosting `CopyEngineSupervisor`
(`App:Copy:Enabled=true`, `App:Features:CopyTrading=true`) **tanpa ALB** — worker yang memegang socket cTrader
bertahan lama. String koneksi DB disimpan di **AWS Secrets Manager**, dimasukkan melalui blok `secrets` tugas
(role eksekusi diberikan `secretsmanager:GetSecretValue` hanya untuk secret tersebut),
bukan env plaintext. `NodeName` setiap tugas default ke hostname containernya (unik per tugas Fargate), jadi
lease DB attribut profil yang berjalan per tugas — dua tugas tidak pernah menjadi host duplikat satu profil.
Skala `copy_agent_count` untuk menambah kapasitas copy; ring kunci DataProtection dibagikan melalui Postgres, jadi
tugas mana pun bisa mendekripsi token Open API yang disimpan.
