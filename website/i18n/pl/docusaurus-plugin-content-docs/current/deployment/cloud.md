---
title: Wdrażaj do chmury
description: Wdrażaj cMind do Azure, AWS lub Kubernetes. Która platforma pasuje, wymagania wstępne i przewodniki krok po kroku.
sidebar_position: 2
---

# Wdrażaj do chmury ☁️

Przerosnąłeś swojego laptopa? Czas umieścić cMind na rzeczywistej infrastrukturze. Dobrą wiadomość: jest zaprojektowany do skalowania w poziomie prawie bez ceremonii operatora — brak ZooKeeper, brak wyborów lidera, tylko repliki i baza danych.

**Jedna rzecz do wiadości z przodu:** warstwę bezstanową (Web + MCP) uruchamia się szczęśliwie na *dowolnej* platformie kontenerów, ale **agenci węzłów potrzebują uprzywilejowanego Docker** (kompilują i uruchamiają kontenery cTrader). To wyklucza bezserwerowe runtime'y takie jak Azure Container Apps i AWS Fargate dla *agentów* — uruchom te na [Kubernetes](./kubernetes.md), maszynie wirtualnej lub EC2 i wskaż je na URL sieci Web.

Wybierz swoją ścieżkę:

- 🟦 **[Azure](./cloud-azure.md)** — Container Apps + Postgres Flexible Server (Bicep).
- 🟧 **[AWS](./cloud-aws.md)** — ECS Fargate + ALB + RDS (Terraform).
- ⎈ **[Kubernetes](./kubernetes.md)** — Helm chart, działa na AKS / EKS / wszędzie.
- 📈 **[Skalowanie](./scaling.md)** — jak to wszystko skaluje i samo się leczy po uruchomieniu.

Warstwa bezstanowa (Web + MCP) uruchamia się na dowolnej platformie kontenerów; Postgres = zarządzana baza danych. **Agenci węzłów potrzebują uprzywilejowanego Docker (DinD)** — bezserwerowe runtime'y kontenerów (Azure Container Apps, AWS Fargate) to blokują. Uruchom agentów na Kubernetes ([kubernetes.md](kubernetes.md)) lub maszynie wirtualnej/EC2, wskaż na URL sieci Web.

| Chmura | Warstwa bezstanowa | Baza danych | Przewodnik |
| ----- | -------------- | -------- | ----- |
| Azure | Container Apps (Bicep) | Postgres Flexible Server | [cloud-azure.md](cloud-azure.md) |
| AWS | ECS Fargate + ALB (Terraform) | RDS Postgres | [cloud-aws.md](cloud-aws.md) |

Wspólne wymagania wstępne, oboje:

1. Kompiluj + popchnij trzy obrazy do rejestru, który chmura może pobrać (`cmind-web`, `cmind-mcp`, `cmind-node-agent`).
2. Wybierz sekrety: hasło DB, email/hasło właściciela, **token join odkrycia** (≥ 32 znaki) współdzielone przez aplikację sieciową + każdy agent węzła.
3. Wdrażaj IaC (poniżej), potem wyrź agentów węzłów osobno (K8s/VM) z `NodeAgent__MainUrl` = wdrażanego URL sieci Web, `NodeAgent__JwtSecret` = token join.

Odkrycie, logowanie, sondy zachowują się tak samo jak setup lokalny/K8s — patrz [../operations/node-discovery.md](../operations/node-discovery.md) i [../operations/logging.md](../operations/logging.md).
