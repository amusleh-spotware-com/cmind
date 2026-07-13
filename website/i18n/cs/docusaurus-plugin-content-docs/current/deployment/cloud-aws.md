---
description: "deploy/aws = Terraform modul: ECS Fargate (Web + MCP) za ALB, RDS Postgres, CloudWatch logs."
---

# Nasazení na AWS — krok za krokem

`deploy/aws` = Terraform modul: **ECS Fargate** (Web + MCP) za **ALB**, **RDS Postgres**, CloudWatch logs.

## 1. Předpoklady

- Terraform ≥ 1.5 + AWS credentials (`aws configure` / env vars) s právy na vytváření VPC-scoped
  zdrojů, ECS, RDS, ALB, IAM.
- Tři image v registru, ze kterého může ECS stahovat (ECR, nebo GHCR public).

## 2. Inicializace

```bash
cd deploy/aws
terraform init
```

## 3. Apply

```bash
terraform apply \
  -var image_registry=ghcr.io/your-org/cmind \
  -var image_tag=1.0.0 \
  -var owner_email=you@example.com \
  -var owner_password='Change-Me-Str0ng!' \
  -var pg_password="$(openssl rand -hex 16)" \
  -var discovery_join_token="$(openssl rand -hex 24)"
```

Vytvoří: RDS Postgres (`appdb`), ECS cluster, Fargate služby pro Web + MCP, ALB (Web na `/`,
MCP na `/mcp`), security groups, CloudWatch log group, **ADOT (AWS Distro for
OpenTelemetry) collector sidecar** v každém tasku. Aplikace exportuje OTLP do sidecaru, ten posílá
trace do **X-Ray**, metriky do **CloudWatch** (EMF, namespace `cmind`); logy zůstávají na
`awslogs` driveru jako compact JSON. Discovery zapnuto pro Web. Task role uděluje sidecaru
přístup k X-Ray + CloudWatch pro zápis — žádný collector není třeba spouštět samostatně.

> Používá **default VPC/subnets** účtu pro stručnost. Pro produkci připojte vlastní VPC, privátní
> subnets, HTTPS listener (ACM cert).

## 4. Získejte URL

```bash
terraform output web_url   # ALB root
terraform output mcp_url   # ALB /mcp
```

Otevřete `web_url`, přihlaste se jako owner (při prvním přihlášení vynucena změna hesla).

## 5. Přidání node agentů (samostatně)

Fargate nepovoluje privilegované/DinD, takže agenty spouštjte jinde a směřujte je na `web_url`:

- **ECS na EC2** — capacity provider s `privileged = true` task definitions, které spouštějí
  `cmind-node-agent`.
- **EKS** — Helm chart ([kubernetes.md](kubernetes.md)) s `nodeAgent.privileged=true`.

Nastavte `NodeAgent__MainUrl=<web_url>`, `NodeAgent__AdvertiseUrl=<agent reachable url>`,
`NodeAgent__JwtSecret=<discovery_join_token>`. Agenti se sami registrují — viz
[../operations/node-discovery.md](../operations/node-discovery.md).

## 6. Ověření

```bash
aws logs tail /ecs/cmind --since 5m         # compact JSON logy
curl -s "$(terraform output -raw web_url)/version"
```

## Produkční poznámky

- Přidejte HTTPS listener + ACM certifikát; omezte ALB security group.
- Ukládejte tajemství do AWS Secrets Manager / SSM, injectujte přes task-definition `secrets` místo
  plaintextového `environment`.
- Zapněte RDS Multi-AZ + zálohy.
- Traces (X-Ray), metriky (CloudWatch EMF), logy (CloudWatch Logs) jsou automaticky propojeny přes
  ADOT sidecar; korelace přes `trace_id`. Viz
  [../operations/logging.md](../operations/logging.md#aws--x-ray--cloudwatch-adot-sidecar).
- Aplikace již směřuje `OTEL_EXPORTER_OTLP_ENDPOINT` na in-task sidecar; přesměrujte na externí
  collector, pokud preferujete centralizaci.

## Copy-trading agent + Secrets Manager (S5)

`deploy/aws/copy-agent.tf` přidává **copy-agent** ECS Fargate službu hostující `CopyEngineSupervisor`
(`App:Copy:Enabled=true`, `App:Features:CopyTrading=true`) **bez ALB** — worker držící dlouho běžící
cTrader sockety. Connection string k DB je uložen v **AWS Secrets Manager**, injectovaný přes
task's `secrets` block (execution role má `secretsmanager:GetSecretValue` pouze pro toto tajemství),
ne plaintextový env. Každý task's `NodeName` defaultuje na svůj container hostname (unique per Fargate task), takže
DB lease atribuuje běžící profily per task — dva tasky nikdy nehostují jeden profil. Škálujte
`copy_agent_count` pro přidání copy kapacity; DataProtection key ring sdílen přes Postgres, takže jakýkoliv task
může dešifrovat uložené Open API tokeny.
