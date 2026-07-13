---
title: Ανάπτυξη στο cloud
description: Ανάπτυξη cMind στο Azure, AWS ή Kubernetes. Ποια πλατφόρμα ταιριάζει, προαπαιτούμενα και βήμα προς βήμα οδηγούς.
sidebar_position: 2
---

# Ανάπτυξη στο cloud ☁️

Ξεπεράσατε το φορητό σας; Ώρα να βάλετε cMind σε πραγματική υποδομή. Καλή είδηση: σχεδιάστηκε για
κλιμάκωση με σχεδόν καμία τελετουργία διαχειριστή — χωρίς ZooKeeper, χωρίς εκλογή αρχηγού, μόνο αντίγραφα και
βάση δεδομένων.

**Το ένα πράγμα που πρέπει να ξέρετε εκ των προτέρων:** το stateless tier (Web + MCP) τρέχει ευτυχώς σε *οποιαδήποτε* κοντέινερ
πλατφόρμα, αλλά **οι πράκτορες κόμβων χρειάζονται προνόμια Docker** (κτίζουν και τρέχουν cTrader κοντέινερ). Αυτό
αποκλείει serverless runtimes όπως Azure Container Apps και AWS Fargate για τους *πράκτορες* — τρέχτε αυτά
σε [Kubernetes](./kubernetes.md), VM ή EC2 και δείχνουν στο Web URL σας.

Επιλέξτε το δικό σας μονοπάτι:

- 🟦 **[Azure](./cloud-azure.md)** — Container Apps + Postgres Flexible Server (Bicep).
- 🟧 **[AWS](./cloud-aws.md)** — ECS Fargate + ALB + RDS (Terraform).
- ⎈ **[Kubernetes](./kubernetes.md)** — το Helm chart, λειτουργεί σε AKS / EKS / παντού.
- 📈 **[Κλιμάκωση](./scaling.md)** — πώς κλιμακώνεται όλα και αυτο-θεραπεύει μόλις ανέβει.

Το stateless tier (Web + MCP) τρέχει σε οποιαδήποτε κοντέινερ πλατφόρμα. Postgres = διαχειριζόμενη βάση δεδομένων.
**Οι πράκτορες κόμβων χρειάζονται προνόμια Docker (DinD)** — serverless container runtimes (Azure Container
Apps, AWS Fargate) το αποκλείουν. Τρέχτε πράκτορες σε Kubernetes ([kubernetes.md](kubernetes.md)) ή
VM/EC2, δείχνουν στο Web URL.

| Cloud | Stateless tier | Database | Guide |
| ----- | -------------- | -------- | ----- |
| Azure | Container Apps (Bicep) | Postgres Flexible Server | [cloud-azure.md](cloud-azure.md) |
| AWS | ECS Fargate + ALB (Terraform) | RDS Postgres | [cloud-aws.md](cloud-aws.md) |

Κοινά προαπαιτούμενα, και τα δύο:

1. Κτίζουν + ώθηση τρεις εικόνες στο κατάστημα cloud μπορεί να τραβήξει (`cmind-web`, `cmind-mcp`,
   `cmind-node-agent`).
2. Επιλέξτε μυστικά: κωδικό πρόσβασης DB, ιδιοκτήτης email/κωδικό πρόσβασης, **token συνδέσμου ανακάλυψης** (≥ 32 χαρακτήρες)
   μοιράζεται από το Web app + κάθε πράκτορα κόμβων.
3. Ανάπτυξη IaC (παρακάτω), στη συνέχεια φέρνουν πράκτορες κόμβων ξεχωριστά (K8s/VM) με
   `NodeAgent__MainUrl` = ανάπτυξη Web URL, `NodeAgent__JwtSecret` = token συνδέσμου.

Ανακάλυψη, καταγραφή, δοκιμές συμπεριφέρονται το ίδιο με τα τοπικά/K8s setups — δείτε
[../operations/node-discovery.md](../operations/node-discovery.md) και
[../operations/logging.md](../operations/logging.md).
