---
description: "deploy/aws = modulo Terraform: ECS Fargate (Web + MCP) dietro ALB, RDS Postgres, CloudWatch logs."
---

# Deployment AWS — passo dopo passo

`deploy/aws` = modulo Terraform: **ECS Fargate** (Web + MCP) dietro **ALB**, **RDS Postgres**, CloudWatch logs.

## 1. Prerequisiti

- Terraform ≥ 1.5 + credenziali AWS (`aws configure` / env vars) con diritti per creare risorse VPC-scoped,
  ECS, RDS, ALB, IAM.
- Tre immagini nel registry ECS che può scaricare (ECR, o GHCR pubblico).

## 2. Inizializza

```bash
cd deploy/aws
terraform init
```

## 3. Applica

```bash
terraform apply \
  -var image_registry=ghcr.io/your-org/cmind \
  -var image_tag=1.0.0 \
  -var owner_email=you@example.com \
  -var owner_password='Change-Me-Str0ng!' \
  -var pg_password="$(openssl rand -hex 16)" \
  -var discovery_join_token="$(openssl rand -hex 24)"
```

Crea: RDS Postgres (`appdb`), cluster ECS, servizi Fargate per Web + MCP, ALB (Web a `/`,
MCP a `/mcp`), security groups, gruppo log CloudWatch, **ADOT (AWS Distro for
OpenTelemetry) collector sidecar** in ogni task. L'app esporta OTLP al sidecar, che invia
traces a **X-Ray**, metriche a **CloudWatch** (EMF, namespace `cmind`); i log restano su
driver `awslogs` come JSON compatto. Discovery attivo per Web. Il task role concede al sidecar
accesso scrittura X-Ray + CloudWatch — nessun collector da gestire.

> Usa il **VPC/subnet predefiniti** dell'account per brevità. Per produzione, collegare proprio VPC,
> subnet private, listener HTTPS (certificato ACM).

## 4. Ottieni gli URL

```bash
terraform output web_url   # radice ALB
terraform output mcp_url   # ALB /mcp
```

Apri `web_url`, accedi con il proprietario ( cambio password forzato al primo login).

## 5. Aggiungi agenti nodo (separato)

Fargate non consente privileged/DinD, quindi eseguire gli agenti altrove puntando a `web_url`:

- **ECS su EC2** — capacity provider con `privileged = true` task definitions eseguendo
  `cmind-node-agent`.
- **EKS** — Helm chart ([kubernetes.md](kubernetes.md)) con `nodeAgent.privileged=true`.

Impostare `NodeAgent__MainUrl=<web_url>`, `NodeAgent__AdvertiseUrl=<url raggiungibile dall'agente>`,
`NodeAgent__JwtSecret=<discovery_join_token>`. Gli agenti si auto-registrano — vedere
[../operations/node-discovery.md](../operations/node-discovery.md).

## 6. Verifica

```bash
aws logs tail /ecs/cmind --since 5m         # log JSON compatti
curl -s "$(terraform output -raw web_url)/version"
```

## Note di produzione

- Aggiungere listener HTTPS + certificato ACM; restringere il security group ALB.
- Memorizzare i secret in AWS Secrets Manager / SSM, iniettare tramite `secrets` nella task definition invece di
  environment in testo chiaro.
- Abilitare RDS Multi-AZ + backup.
- Traces (X-Ray), metriche (CloudWatch EMF), log (CloudWatch Logs) cablati automaticamente tramite
  sidecar ADOT; correlare su `trace_id`. Vedere
  [../operations/logging.md](../operations/logging.md#aws--x-ray--cloudwatch-adot-sidecar).
- L'app punta già `OTEL_EXPORTER_OTLP_ENDPOINT` al sidecar in-task; riorientare a collector esterno
  se si preferisce centralizzare.

## Copy-trading agent + Secrets Manager (S5)

`deploy/aws/copy-agent.tf` aggiunge servizio ECS Fargate **copy-agent** che ospita `CopyEngineSupervisor`
(`App:Copy:Enabled=true`, `App:Features:CopyTrading=true`) **senza ALB** — worker che mantiene socket cTrader
long-lived. La stringa di connessione DB memorizzata in **AWS Secrets Manager**, iniettata attraverso
il `secrets` block del task (execution role con `secretsmanager:GetSecretValue` solo su quel secret),
non env in testo chiaro. Il `NodeName` di ogni task default al suo container hostname (unico per Fargate task), così
gli attributi di lease DB assegnano i profili in esecuzione per task — due task non hostano mai doppione uno.
Scalare `copy_agent_count` per aggiungere capacità di copy; il key ring DataProtection condiviso tramite Postgres, così
qualsiasi task può decrittare i token Open API memorizzati.
