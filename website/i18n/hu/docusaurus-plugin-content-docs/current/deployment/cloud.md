---
title: Telepítés a felhőbe
description: Telepítsd a cMind-et az Azure-ba, AWS-be vagy Kubernetes-be. Melyik platform illeszkedik, előfeltételek és lépésenkénti útmutatók.
sidebar_position: 2
---

# Telepítés a felhőbe ☁️

Kinőtt a laptopod? Itt az ideje a cMind-et valódi infrastruktúrára tenni. Jó hírek: arra lett tervezve, hogy szinte nincs operátor ceremónia — nincs ZooKeeper, nincs vezetői választás, csak replika és adatbázis.

**Az egyik dolog, amit előre kell tudni:** az állapot nélküli szint (Web + MCP) szívesen működik *bármilyen* konténer-platformon, de **a node-ügynököknek jogosult Docker** szükséges (cTrader-konténereket építenek és futtatnak). Ez kiszűri a kiszolgáló nélküli futási időket, mint az Azure Container Apps és az AWS Fargate az *ügynökök* számára — futtassa őket a [Kubernetes](./kubernetes.md) címen, VM vagy EC2 és mutassa az a Web URL.

Válassz az útvonalad:

- 🟦 **[Azure](./cloud-azure.md)** — Container Apps + Postgres Flexible Server (Bicep).
- 🟧 **[AWS](./cloud-aws.md)** — ECS Fargate + ALB + RDS (Terraform).
- ⎈ **[Kubernetes](./kubernetes.md)** — a Helm-diagram, az AKS / EKS / bárhol működik.
- 📈 **[Méretezés](./scaling.md)** — hogyan skálázódik az összes és öngyógyul, miután fent van.

Az állapot nélküli szint (Web + MCP) bármilyen konténer-platformon működik; a Postgres = felügyelt adatbázis. **A node-ügynököknek jogosult Docker (DinD)** szükséges — kiszolgáló nélküli konténer-futási idők (Azure Container Apps, AWS Fargate) tiltják. Az ügynökök futtatása a [Kubernetes](kubernetes.md) címen vagy a VM/EC2, mutat az Web URL-re.

| Felhő | Állapot nélküli szint | Adatbázis | Útmutató |
| ----- | -------------- | -------- | ----- |
| Azure | Container Apps (Bicep) | Postgres Flexible Server | [cloud-azure.md](cloud-azure.md) |
| AWS | ECS Fargate + ALB (Terraform) | RDS Postgres | [cloud-aws.md](cloud-aws.md) |

Közös előfeltételek, mindkettő:

1. Építsen + nyomjon három képet a regisztrációs felhőbe, amely lekérheti (`cmind-web`, `cmind-mcp`, `cmind-node-agent`).
2. Válassza ki a titkokat: DB-jelszó, tulajdonos e-mail/jelszó, **felderítési csatlakozási token** (≥ 32 karakter) a Web-alkalmazás és az összes node-ügynök által megosztott.
3. Telepítse az IaC-t (alul), majd hozza fel a node-ügynököket külön (K8s/VM) a `NodeAgent__MainUrl` = telepített Web URL, `NodeAgent__JwtSecret` = csatlakozási token értékkel.

Felderítés, naplózás, szondák ugyanaz a viselkedés, mint a helyi/K8s beállítások — lásd a [../operations/node-discovery.md](../operations/node-discovery.md) és a [../operations/logging.md](../operations/logging.md) fájlokat.
