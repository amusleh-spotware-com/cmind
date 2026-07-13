---
title: Bereitstellung zu die Cloud
description: Bereitstellung cMind zu Azure, AWS, oder Kubernetes. Welch Plattform passt, Vorbedingung, und Schritt-für-Schritt Führer.
sidebar_position: 2
---

# Bereitstellung zu die Cloud

Älter geworden Ihr Laptop? Zeit zu setzen cMind auf echt Infrastruktur. Gute Nachrichten: es ist entworfen zu Skalierung aus mit fast nein Betreiber Zeremonie — nein ZooKeeper, nein Anführer Wahl, nur Replikate und ein Datenbank.

**Die eins Sache zu wissen im Voraus:** das Zustand-los Tier (Web + MCP) läuft glücklich auf *jedes* Container-Plattform, aber **Knoten Agenten brauchen privilegiert Docker** (sie bauen und laufen cTrader Behälter). Das Regel aus Serverless Laufzeit wie Azure Container Apps und AWS Fargate für *Agenten* — laufen die auf [Kubernetes](./kubernetes.md), ein VM, oder EC2 und zeigen sie auf Ihr Web URL.

Wähle Deinen Weg:

- 🟦 **[Azure](./cloud-azure.md)** — Container Apps + Postgres Flexible Server (Bicep).
- 🟧 **[AWS](./cloud-aws.md)** — ECS Fargate + ALB + RDS (Terraform).
- ⎈ **[Kubernetes](./kubernetes.md)** — die Helm Diagramm, funktioniert auf AKS / EKS / überall.
- 📈 **[Skalierung](./scaling.md)** — wie es alles Skala und Selbst-heilt einmal es ist oben.

Zustand-los Tier (Web + MCP) laufen auf jedes Container-Plattform; Postgres = verwaltet Datenbank. **Knoten Agenten brauchen privilegiert Docker (DinD)** — Serverlos Container Laufzeit (Azure Container Apps, AWS Fargate)Block es. Laufen Agenten auf Kubernetes ([kubernetes.md](kubernetes.md)) oder VM/EC2, Zeigen auf Web URL.

| Cloud | Zustand-los Tier | Datenbank | Führer |
| ----- | -------------- | -------- | ----- |
| Azure | Container Apps (Bicep) | Postgres Flexible Server | [cloud-azure.md](cloud-azure.md) |
| AWS | ECS Fargate + ALB (Terraform) | RDS Postgres | [cloud-aws.md](cloud-aws.md) |

Gemeinsam Vorbedingung, beide:

1. Bau + drücke drei Bilder zu Registrierung Cloud kann ziehen (`cmind-web`, `cmind-mcp`, `cmind-node-agent`).
2. Wählen Geheimnisse: DB Passwort, Besitzer Email/Passwort, **Ermittlung Beitritts-Token** (≥ 32 Zeichen) Teilen durch Web App + jedem Knoten Agenten.
3. Bereitstellung IaC (unten), dann bringen Knoten Agenten oben separat (K8s/VM) mit `NodeAgent__MainUrl` = bereitgestellt Web URL, `NodeAgent__JwtSecret` = Beitritt-Token.

Ermittlung, Protokollierung, Sonden Verhalten gleich wie lokal/K8s Setup — siehe [../operations/node-discovery.md](../operations/node-discovery.md) und [../operations/logging.md](../operations/logging.md).
