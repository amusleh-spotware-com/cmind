---
title: Namestite v oblak
description: Namestite cMind na Azure, AWS ali Kubernetes. Katera platforma se prilega, pogoji in vodniki po korakih.
sidebar_position: 2
---

# Namestite v oblak ☁️

Prerasli ste svoj prenosni računalnik? Čas je, da postavite cMind na pravo infrastrukturo. Dobra vest:
je zasnovan za razširjanje z zanesljivo brez usklajevalca — ni ZooKeeper, ni izbire vodje, samo
replike in podatkovna baza.

**Ena stvar, ki jo je dobro vedeti vnaprej:** brezstansko raven (Web + MCP) je vesela na *kateri koli*
platformi za kontejnirje, vendar **agenti vozlišč potrebujejo priviligirani Docker** (gradijo in tečejo
kontejnerje cTraderja). To izključuje brezzasebne čase izvajanja, kot so Azure Container Apps in AWS
Fargate za *agente* — tečite te na [Kubernetesem](./kubernetes.md), VM ali EC2 in jih pokažite na
vašo spletno URL.

Izberite svojo pot:

- 🟦 **[Azure](./cloud-azure.md)** — Container Apps + Postgres Flexible Server (Bicep).
- 🟧 **[AWS](./cloud-aws.md)** — ECS Fargate + ALB + RDS (Terraform).
- ⎈ **[Kubernetes](./kubernetes.md)** — Helm karta, deluje na AKS / EKS / kjerkoli.
- 📈 **[Skaliranje](./scaling.md)** — kako se vse skalira in samodejno zdravi, ko je zgoraj.

Brezstansko raven (Web + MCP) tečejo na kateri koli platformi za kontejnirje; Postgres = upravljana
podatkovna baza. **Agenti vozlišč potrebujejo priviligirani Docker (DinD)** — brezzasebni časi
izvajanja kontejnerja (Azure Container Apps, AWS Fargate) ga blokirajo. Tečite agente na
Kubernetesem ([kubernetes.md](kubernetes.md)) ali VM/EC2, pokažite na spletno URL.

| Oblak | Brezstansko raven | Podatkovna baza | Vodnik |
| ----- | -------------- | -------- | ----- |
| Azure | Container Apps (Bicep) | Postgres Flexible Server | [cloud-azure.md](cloud-azure.md) |
| AWS | ECS Fargate + ALB (Terraform) | RDS Postgres | [cloud-aws.md](cloud-aws.md) |

Skupni pogoji za oba:

1. Gradnja + potisk tri slik v registr, ki ga oblak lahko povleče (`cmind-web`, `cmind-mcp`,
   `cmind-node-agent`).
2. Izberite skrivnosti: geslo DB, e-pošta lastnika/geslo, **žeton sporočanja za odkrivanje** (≥ 32
   znakov) v skupni rabi z aplikacijo Web + vsakim agentom vozlišča.
3. Namestite IaC (spodaj), nato ločeno prinesite agente vozlišča (K8s/VM) z
   `NodeAgent__MainUrl` = nameščeno spletno URL, `NodeAgent__JwtSecret` = žeton sporočanja.

Odkrivanje, beležka, sondi se vedejo enako kot lokalne/K8s nastavke — poglejte
[../operations/node-discovery.md](../operations/node-discovery.md) in
[../operations/logging.md](../operations/logging.md).
