---
title: Déployer vers le cloud
description: Déployez cMind sur Azure, AWS, ou Kubernetes. Quelle plate-forme convient, prérequis, et guides étape par étape.
sidebar_position: 2
---

# Déployer vers le cloud ☁️

Avez-vous dépassé votre ordinateur portable ? Il est temps de mettre cMind sur une vraie infrastructure. Bonne nouvelle : c'est conçu pour
mettre à l'échelle presque sans cérémonie d'opérateur — pas de ZooKeeper, pas d'élection de leader, juste réplicas et une
base de données.

**La seule chose à savoir à l'avance :** la couche apatride (Web + MCP) fonctionne heureux sur *n'importe quelle* plate-forme de conteneur, mais **les agents de nœud ont besoin de Docker privilégié** (ils construisent et exécutent les conteneurs cTrader). Cela exclut les runtimes serverless comme Azure Container Apps et AWS Fargate pour les *agents* — exécutez-les
sur [Kubernetes](./kubernetes.md), une VM, ou EC2 et pointez-les vers votre URL Web.

Choisissez votre chemin :

- 🟦 **[Azure](./cloud-azure.md)** — Container Apps + Postgres Flexible Server (Bicep).
- 🟧 **[AWS](./cloud-aws.md)** — ECS Fargate + ALB + RDS (Terraform).
- ⎈ **[Kubernetes](./kubernetes.md)** — le chart Helm, fonctionne sur AKS / EKS / n'importe où.
- 📈 **[Scaling](./scaling.md)** — comment tout cela s'étend et s'auto-guérit une fois que c'est opérationnel.

La couche apatride (Web + MCP) s'exécute sur n'importe quelle plate-forme de conteneur ; Postgres = base de données gérée.
**Les agents de nœud ont besoin de Docker privilégié (DinD)** — les runtimes serverless (Azure Container
Apps, AWS Fargate) le bloquent. Exécutez les agents sur Kubernetes ([kubernetes.md](kubernetes.md)) ou
VM/EC2, pointez vers l'URL Web.

| Cloud | Couche apatride | Base de données | Guide |
| ----- | -------------- | -------- | ----- |
| Azure | Container Apps (Bicep) | Postgres Flexible Server | [cloud-azure.md](cloud-azure.md) |
| AWS | ECS Fargate + ALB (Terraform) | RDS Postgres | [cloud-aws.md](cloud-aws.md) |

Prérequis communs, les deux :

1. Construisez + poussez trois images vers le registre que le cloud peut tirer (`cmind-web`, `cmind-mcp`,
   `cmind-node-agent`).
2. Choisissez les secrets : mot de passe BD, email/mot de passe propriétaire, **jeton de jointure de découverte** (≥ 32 caractères)
   partagé par l'app Web + chaque agent de nœud.
3. Déployez IaC (ci-dessous), puis mettez les agents de nœud en place séparément (K8s/VM) avec
   `NodeAgent__MainUrl` = URL Web déployée, `NodeAgent__JwtSecret` = jeton de jointure.

La découverte, la journalisation, les sondes se comportent pareils que les setups locaux/K8s — voir
[../operations/node-discovery.md](../operations/node-discovery.md) et
[../operations/logging.md](../operations/logging.md).
