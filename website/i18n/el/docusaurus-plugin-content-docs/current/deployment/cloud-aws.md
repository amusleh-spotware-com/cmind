---
description: "deploy/aws = Terraform module: ECS Fargate (Web + MCP) πίσω από ALB, RDS Postgres, CloudWatch logs."
---

# Ανάπτυξη AWS — βήμα προς βήμα

`deploy/aws` = Terraform module: **ECS Fargate** (Web + MCP) πίσω από **ALB**, **RDS Postgres**, CloudWatch logs.

## 1. Προαπαιτούμενα

- Terraform ≥ 1.5 + διαπιστευτήρια AWS (`aws configure` / env vars) με δικαιώματα για να κάνουν VPC-scoped
  πόροι, ECS, RDS, ALB, IAM.
- Τρεις εικόνες σε κατάστημα μητρικών σχέσεων που ECS μπορεί να τραβήξει (ECR, ή GHCR δημόσιο).

## 2. Αρχικοποίηση

```bash
cd deploy/aws
terraform init
```

## 3. Εφαρμογή

```bash
terraform apply \
  -var image_registry=ghcr.io/your-org/cmind \
  -var image_tag=1.0.0 \
  -var owner_email=you@example.com \
  -var owner_password='Change-Me-Str0ng!' \
  -var pg_password="$(openssl rand -hex 16)" \
  -var discovery_join_token="$(openssl rand -hex 24)"
```

Κάνει: RDS Postgres (`appdb`), ECS cluster, Fargate services για Web + MCP, ALB (Web στο `/`,
MCP στο `/mcp`), ομάδες ασφαλείας, CloudWatch log group, **ADOT (AWS Distro για
OpenTelemetry) sidecar συλλέκτη** σε κάθε εργασία. Εφαρμογή εξαγωγές OTLP στο sidecar, το οποίο αποστέλνει
ίχνη σε **X-Ray**, μετρικές στο **CloudWatch** (EMF, χώρος ονόματος `cmind`). τα κούρνια παραμένουν σε
`awslogs` driver ως συμπιεσμένο JSON. Ανακάλυψη στο για Web. Ο ρόλος εργασίας χορηγεί sidecar
X-Ray + CloudWatch write access — δεν υπάρχει συλλέκτης για τρέξιμο.

> Χρησιμοποιεί λογαριασμό **default VPC/subnets** για σύντομη. Για παραγωγή, σύρμα δικό σας VPC, ιδιωτικά
> subnets, HTTPS listener (ACM cert).

## 4. Λάβετε τα URLs

```bash
terraform output web_url   # ALB root
terraform output mcp_url   # ALB /mcp
```

Ανοίξτε `web_url`, συνδεθείτε με ιδιοκτήτη (αναγκάστηκε αλλαγή κωδικού πρόσβασης κατά την πρώτη σύνδεση).

## 5. Προσθήκη πράκτορες κόμβων (ξεχωριστά)

Fargate απαγορεύει προνόμια/DinD, έτσι τρέχουν πράκτορες αλλού που δείχνουν στο `web_url`:

- **ECS σε EC2** — capacity provider με `privileged = true` task definitions τρέχουν
  `cmind-node-agent`.
- **EKS** — Helm chart ([kubernetes.md](kubernetes.md)) με `nodeAgent.privileged=true`.

Ορίστε `NodeAgent__MainUrl=<web_url>`, `NodeAgent__AdvertiseUrl=<agent reachable url>`,
`NodeAgent__JwtSecret=<discovery_join_token>`. Οι πράκτορες αυτο-εγγράφονται — δείτε
[../operations/node-discovery.md](../operations/node-discovery.md).

## 6. Επαλήθευση

```bash
aws logs tail /ecs/cmind --since 5m         # συμπιεσμένο JSON logs
curl -s "$(terraform output -raw web_url)/version"
```

## Σημειώσεις παραγωγής

- Προσθέστε HTTPS listener + ACM certificate. περιορίστε την ομάδα ασφαλείας ALB.
- Αποθηκευμένα μυστικά σε AWS Secrets Manager / SSM, ενέχετε μέσω task-definition `secrets` αντί του
  plaintext `environment`.
- Ενεργοποιήστε RDS Multi-AZ + backups.
- Ίχνη (X-Ray), μετρικές (CloudWatch EMF), logs (CloudWatch Logs) συρόμενα αυτόματα μέσω
  ADOT sidecar. συσχετίζετε στο `trace_id`. Δείτε
  [../operations/logging.md](../operations/logging.md#aws--x-ray--cloudwatch-adot-sidecar).
- Εφαρμογή ήδη δείχνει `OTEL_EXPORTER_OTLP_ENDPOINT` κατά το task sidecar. δείχνουν σε εξωτερικό
  συλλέκτης εάν προτιμάτε κεντρικοποίηση.

## Πράκτορας αντιγραφής + Secrets Manager (S5)

`deploy/aws/copy-agent.tf` προσθέτει **copy-agent** ECS Fargate service φιλοξενίας `CopyEngineSupervisor`
(`App:Copy:Enabled=true`, `App:Features:CopyTrading=true`) με **όχι ALB** — εργάτης που κρατά μακροχρόνιες
cTrader sockets. Συμβολοσειρά σύνδεσης DB αποθηκευμένη σε **AWS Secrets Manager**, ενέχετε μέσω
task's `secrets` block (role εκτέλεσης χορήγηση `secretsmanager:GetSecretValue` σε μόνο αυτό το μυστικό),
όχι plaintext env. Κάθε `NodeName` εργασίας προεπιλογή στο όνομα κοντέινερ του (μοναδικό ανά Fargate εργασία), έτσι
DB lease αποδίδει τρέχουσα προφίλ ανά εργασία — δύο εργασίες ποτέ διπλό-κεντρική ένα. Κλιμάκωση
`copy_agent_count` για προσθήκη χωρητικότητας αντιγραφής. DataProtection keyring δοχείο μέσω Postgres, έτσι κάθε εργασία
μπορεί να αποκρυπτογραφήσει αποθηκευμένα Open API tokens.
