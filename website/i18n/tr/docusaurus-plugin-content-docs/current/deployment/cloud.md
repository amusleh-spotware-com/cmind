---
title: Buluta konuşlandırın
description: cMind'ı Azure, AWS veya Kubernetes'e konuşlandırın. Hangi platform uyar, ön koşullar ve adım adım rehberler.
sidebar_position: 2
---

# Buluta konuşlandırın ☁️

Dizüstü bilgisayarınızı aştınız? cMind'ı gerçek altyapıya koymak zamanı. İyi haber: hemen hemen operatör
törenini hiçbirinde ölçeklendirmek için tasarlandı — ZooKeeper yok, lider seçimi yok, sadece kopyalar
ve bir veri tabanı.

**Başlangıçta bilmek için bir şey:** Durumsuz katman (Web + MCP) *herhangi* konteyner platformunda
mutlu bir şekilde çalışır, ama **düğüm aracıları ayrıcalıklı Docker**'a ihtiyaç duyar (cTrader
konteynerler derlerler ve çalıştırırlar). Bu sunucusuz çalışma zamanlarını (Azure Container Apps ve AWS
Fargate) *aracılar* için kurallar dışı — bunları [Kubernetes](./kubernetes.md), bir VM veya EC2'de
çalıştırın ve Web URL'sine yönlendirin.

Yolunuzu seçin:

- 🟦 **[Azure](./cloud-azure.md)** — Container Apps + Postgres Flexible Server (Bicep).
- 🟧 **[AWS](./cloud-aws.md)** — ECS Fargate + ALB + RDS (Terraform).
- ⎈ **[Kubernetes](./kubernetes.md)** — Helm grafiği, AKS / EKS / herhangi bir yerde çalışır.
- 📈 **[Ölçekleme](./scaling.md)** — açılmasından sonra tüm ölçekler ve kendini iyileştirir.

Durumsuz katman (Web + MCP) herhangi bir konteyner platformunda çalışır; Postgres = yönetilen veri tabanı.
**Düğüm aracıları ayrıcalıklı Docker (DinD) gerekir** — sunucusuz konteyner çalışma zamanları (Azure
Container Apps, AWS Fargate) blok yapar. Aracıları Kubernetes ([kubernetes.md](kubernetes.md)) veya
VM/EC2'de çalıştırın, Web URL'sine yönlendirin.

| Bulut | Durumsuz katman | Veri tabanı | Rehber |
| ----- | -------------- | -------- | ----- |
| Azure | Container Apps (Bicep) | Postgres Flexible Server | [cloud-azure.md](cloud-azure.md) |
| AWS | ECS Fargate + ALB (Terraform) | RDS Postgres | [cloud-aws.md](cloud-aws.md) |

Ortak ön koşullar, her ikisi:

1. Üç görüntüyü bulut çekebilecek bir kayıt defterine derleyin ve gönderin (`cmind-web`, `cmind-mcp`,
   `cmind-node-agent`).
2. Sırları seçin: DB şifresi, sahip e-postası/şifresi, **keşif birleştirme jetonunu** (≥ 32 karakter)
   Web uygulaması + her düğüm aracı tarafından paylaşılan.
3. IaC'yi konuşlandırın (aşağı), sonra düğüm aracılarını ayrı olarak (K8s/VM) `NodeAgent__MainUrl` =
   konuşlandırılmış Web URL, `NodeAgent__JwtSecret` = birleştirme jetonuyla getirin.

Keşif, günlükleme, yoklama yerel/K8s kurulumları ile aynı şekilde davranır — bkz.
[../operations/node-discovery.md](../operations/node-discovery.md) ve
[../operations/logging.md](../operations/logging.md).
