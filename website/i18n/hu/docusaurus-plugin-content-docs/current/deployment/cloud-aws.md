---
title: AWS telepites
description: "AWS: EKS, RDS Postgres, Terraform, ADOT kontener sidecar, CloudWatch Logs Insights + X-Ray + CloudWatch metrikak."
---

# AWS telepites (EKS + RDS)

A cMind `deploy/aws/main.tf` Terraform modulja egy teljes AWS EKS + RDS Postgres telepítést épít fel az AWS Distro for OpenTelemetry (ADOT) oldalkocsival minden Fargate task-ban.

## Architektura

```
Internet
   │
   ▼
Application Load Balancer (ALB)
   │
   ├──► cmind-web — EKS Fargate, Blazor Server, SignalR
   │
   ├──► cmind-mcp — EKS Fargate, MCP HTTP+SSE szerver
   │
   └──► cmind-copy-agent — EKS Fargate, másolási kereskedési ügynök
            │
            ▼
       RDS Postgres (Multi-AZ)
            │
            ▼
       ElastiCache Redis (SignalR backplane)
```

## Fő komponensek

### EKS Fargate

Az alkalmazás Fargate-en fut, nincs szerver, amit kezelni kell. Minden pod külön security group-ot kap az ALB és az adatbázis felé.

### ADOT Sidecar

Minden Fargate task-hoz az **AWS Distro for OpenTelemetry (ADOT) collector** oldalkocsi van csatolva. Az alkalmazás OTLP-t exportál a `http://localhost:4317`-re; az ADOT oldalkocsi szétosztja:

- **Traces → AWS X-Ray** (`awsxray` exporter)
- **Metrics → CloudWatch** (`awsemf`, namespace `cmind`, log group `/ecs/<prefix>/metrics`)
- **Logs** maradnak az `awslogs` driveren compact JSON-ként; a CloudWatch Logs Insights automatikusan felfedezi a JSON mezőket, igy `filter`/`stats` parancsokat futtathatsz `trace_id`, `service.name`, `@l` stb. mezőkön.

A task IAM szerepköre (`aws_iam_role.task`) hordozza az `AWSXRayDaemonWriteAccess` + `CloudWatchAgentServerPolicy` jogosultságokat.

### RDS Postgres

Multi-AZ, `db.t3.medium` alapértelemzés, a terhelésnek megfelelően skálázható. Automatikus backup 35 napra, point-in-time recovery támogatott.

## Naplofüggőségek

A CloudWatch Logs Insights query példa:

```
fields @timestamp, @l, @m, trace_id, `service.name`
| filter `service.name` = "cmind-web"
| sort @timestamp desc
```

## Telepítés lépései

```bash
cd deploy/aws
terraform init
terraform plan   # nézd meg a változtatásokat
terraform apply  # alkalmazás

# Helm chart értékek:
helm upgrade --install cmind ../helm/cmind   --namespace cmind --create-namespace   --set image.repository=<ecr-repo>/cmind   --set image.tag=<version>   --set secrets.pgPassword="<secure-password>"   --set secrets.ownerEmail="te@example.com"   --set secrets.ownerPassword="<secure-password>"   --set secrets.discoveryJoinToken="<min-32-char-secret>"   --set web.ingress.enabled=true   --set web.ingress.host=cmind.example.com   --set web.ingress.tls=true
```

## Skálázás

- **Web + MCP:** Fargate auto-scaling CPU/ memória alapján.
- **Copy-agent:** a KEDA Postgres scaler a futó másolási profilok számát kéri és skáláz, hogy minden pod körülbelül `copyAgent.keda.profilesPerPod` (alapértelemzés 25) profilt gazdagépjen.
- **RDS:** skálázás a terhelésnek megfelelően, a Multi-AZ failover automatikus.

## SignalR Redis backplane

A Web alkalmazás `signalr` connection string-et igényel a Redis felé, amit az ALB-n keresztüli sticky sessions-sel kombinálva a Blazor Server körök a megfelelő pod-ra irányítódnak.
