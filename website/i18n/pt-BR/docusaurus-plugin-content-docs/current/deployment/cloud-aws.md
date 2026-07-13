---
description: "deploy/aws = módulo Terraform: ECS Fargate (Web + MCP) atrás de ALB, RDS Postgres, logs CloudWatch."
---

# Implantação AWS — passo a passo

`deploy/aws` = módulo Terraform: **ECS Fargate** (Web + MCP) atrás de **ALB**, **RDS Postgres**, logs CloudWatch.

## 1. Pré-requisitos

- Terraform ≥ 1.5 + credenciais AWS (`aws configure` / variáveis de ambiente) com direitos para fazer
  recursos com escopo VPC, ECS, RDS, ALB, IAM.
- Três imagens em registro que ECS pode extrair (ECR ou GHCR público).

## 2. Inicializar

```bash
cd deploy/aws
terraform init
```

## 3. Aplicar

```bash
terraform apply \
  -var image_registry=ghcr.io/your-org/cmind \
  -var image_tag=1.0.0 \
  -var owner_email=you@example.com \
  -var owner_password='Change-Me-Str0ng!' \
  -var pg_password="$(openssl rand -hex 16)" \
  -var discovery_join_token="$(openssl rand -hex 24)"
```

Faz: RDS Postgres (`appdb`), cluster ECS, serviços Fargate para Web + MCP, ALB (Web em `/`,
MCP em `/mcp`), grupos de segurança, grupo de log CloudWatch, **coletor ADOT (AWS Distro for
OpenTelemetry) sidecar** em cada tarefa. O aplicativo exporta OTLP para sidecar, que envia
rastreamentos para **X-Ray**, métricas para **CloudWatch** (EMF, namespace `cmind`); logs permanecem em
driver `awslogs` como JSON compacto. Descoberta ativada para Web. Papel de tarefa concede sidecar
acesso de escrita X-Ray + CloudWatch — nenhum coletor para executar você mesmo.

> Usa **VPC padrão/subnets** da conta para brevidade. Para produção, conecte VPC próprio, subnets
> privadas, ouvinte HTTPS (certificado ACM).

## 4. Obter os URLs

```bash
terraform output web_url   # raiz ALB
terraform output mcp_url   # ALB /mcp
```

Abra `web_url`, entre com proprietário (alteração de senha forçada no primeiro login).

## 5. Adicionar agentes de nó (separadamente)

Fargate não permite privilegiado/DinD, então execute agentes em outro lugar apontando para `web_url`:

- **ECS em EC2** — provedor de capacidade com definições de tarefa `privileged = true` executando
  `cmind-node-agent`.
- **EKS** — gráfico Helm ([kubernetes.md](kubernetes.md)) com `nodeAgent.privileged=true`.

Defina `NodeAgent__MainUrl=<web_url>`, `NodeAgent__AdvertiseUrl=<agent reachable url>`,
`NodeAgent__JwtSecret=<discovery_join_token>`. Agentes se auto-registram — veja
[../operations/node-discovery.md](../operations/node-discovery.md).

## 6. Verifique

```bash
aws logs tail /ecs/cmind --since 5m         # logs JSON compactos
curl -s "$(terraform output -raw web_url)/version"
```

## Notas de produção

- Adicionar ouvinte HTTPS + certificado ACM; restringir grupo de segurança ALB.
- Armazene segredos no AWS Secrets Manager / SSM, injete via tarefa-definição `secrets` em vez de
  `environment` de texto puro.
- Ativar RDS Multi-AZ + backups.
- Rastreamentos (X-Ray), métricas (CloudWatch EMF), logs (CloudWatch Logs) conectados automaticamente via
  sidecar ADOT; correlacionar em `trace_id`. Veja
  [../operations/logging.md](../operations/logging.md#aws--x-ray--cloudwatch-adot-sidecar).
