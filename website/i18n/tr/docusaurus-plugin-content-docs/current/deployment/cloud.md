---
title: Buluta dağıtın
description: cMind'i Azure, AWS veya Kubernetes'e dağıtın. Hangi platform uygun, ön koşullar ve adım adım kılavuzlar.
sidebar_position: 2
---

# Buluta Dağıtın ☁️

Dizüstünüzü aştınız? cMind'i gerçek altyapıya koymak için zaman. İyi haber: neredeyse hiç operatör merasimi olmadan ölçeklenmek için tasarlanmıştır — ZooKeeper yok, lider seçimi yok, sadece replikalar ve bir veritabanı.

**Baştan bilmeniz gereken tek şey:** durumsuz katman (Web + MCP) herhangi bir konteyner platformunda mutlu bir şekilde çalışır, ancak **düğüm aracılarının ayrıcalıklı Docker'ı gerekir** (cTrader konteynerleri oluşturur ve çalıştırır). Bu, Azure Container Apps ve AWS Fargate gibi sunucusuz çalışma zamanlarını *aracılar* için dışlar — bunları [Kubernetes](./kubernetes.md), bir VM veya EC2 üzerinde çalıştırın ve Web URL'sine işaret edin.

Yolunuzu seçin:

- 🟦 **[Azure](./cloud-azure.md)** — Container Apps + Postgres Esnek Sunucu (Bicep).
- 🟧 **[AWS](./cloud-aws.md)** — ECS Fargate + ALB + RDS (Terraform).
- ⎈ **[Kubernetes](./kubernetes.md)** — Helm grafiği, AKS / EKS / her yerde çalışır.
- 📈 **[Ölçekleme](./scaling.md)** — başladıktan sonra hepsi nasıl ölçeklendirilir ve kendi kendini iyileştirir.

Durumsuz katman (Web + MCP) herhangi bir konteyner platformunda çalışır; Postgres = yönetilen veritabanı.
**Düğüm aracılarının ayrıcalıklı Docker'ı gerekir (DinD)** — sunucusuz konteyner çalışma zamanları (Azure Container Apps, AWS Fargate) bunu engeller. Aracıları Kubernetes ([kubernetes.md](kubernetes.md)) veya VM/EC2 üzerinde çalıştırın, Web URL'sine işaret edin.

| Bulut | Durumsuz katman | Veritabanı | Kılavuz |
|---|---|---|---|
| Azure | App Service / Container Apps | Postgres Flexible Server | [Kılavuz →](./cloud-azure.md) |
| AWS | ECS Fargate | RDS Postgres | [Kılavuz →](./cloud-aws.md) |
| Any | Kubernetes | Managed / Self-hosted Postgres | [Kılavuz →](./kubernetes.md) |

## Temel adımlar (tüm bulutlar)

1. Postgres'i yönetilen hizmet olarak sağlayın (veya kendi Docker konteynerini çalıştırın).
2. Web uygulamasını dağıtın (stateless, herhangi bir konteyner platformu).
3. MCP sunucusunu dağıtın (Web ile aynı yerde, HTTP+SSE).
4. Düğüm aracılarını, ayrıcalıklı Docker erişimi ile ayrı VM'lere veya Kubernetes Pod'larına dağıtın.
5. Aracıları keşfi için kaydetmeye izin verin (işaret: `NodeAgent:MainUrl`).

Adım adım kılavuzlar:

- **[Azure Bicep →](./cloud-azure.md)** — 15 dakika.
- **[AWS Terraform →](./cloud-aws.md)** — 20 dakika.
- **[Kubernetes Helm →](./kubernetes.md)** — 10 dakika.

## Paket ortamı değişkenleri

`docker-compose.yml` veya konteyner platformunuzun env-dosya mekanizması aracılığıyla:

| Değişken | Örnek | Notlar |
|---|---|---|
| `App__Branding__ProductName` | `MyFX` | Ürün adı (varsayılan: cMind) |
| `App__Branding__CompanyName` | `Acme Ltd` | Şirket adı |
| `App__Ai__ApiKey` | `sk-...` | AI sağlayıcı anahtarı (isteğe bağlı) |
| `App__Accounts__AllowedBrokers` | `["MyBroker"]` | Broker izin listesi (isteğe bağlı) |
| `ConnectionStrings__Postgres` | `Host=postgres;...` | Postgres bağlantı dizesi |
| `Aspire__Dashboard__Enabled` | `false` | Aspire gösterge panelini devre dışı bırakın (üretimde) |

Tam liste: [AppOptions.cs →](https://github.com/amusleh-spotware-com/cmind/blob/main/src/Core/Options/AppOptions.cs)

## Kendi'nizi barındırmaya hazır mısınız?

Başlayın:
- **Sadece çalıştırmak mı istiyorsunuz?** → [Yerel olarak](./local.md)
- **Kurulum için adımsal kılavuz?** → [Azure](./cloud-azure.md) / [AWS](./cloud-aws.md) / [Kubernetes](./kubernetes.md)
- **Nasıl ölçeklendirilir?** → [Ölçekleme →](./scaling.md)
