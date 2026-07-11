# AWS deployment — step by step

`deploy/aws` = Terraform module: **ECS Fargate** (Web + MCP) behind **ALB**, **RDS Postgres**, CloudWatch logs.

## 1. Prerequisites

- Terraform ≥ 1.5 + AWS credentials (`aws configure` / env vars) with rights to make VPC-scoped
  resources, ECS, RDS, ALB, IAM.
- Three images in registry ECS can pull (ECR, or GHCR public).

## 2. Initialize

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

Makes: RDS Postgres (`appdb`), ECS cluster, Fargate services for Web + MCP, ALB (Web at `/`,
MCP at `/mcp`), security groups, CloudWatch log group, **ADOT (AWS Distro for
OpenTelemetry) collector sidecar** in each task. App exports OTLP to sidecar, which ships
traces to **X-Ray**, metrics to **CloudWatch** (EMF, namespace `cmind`); logs stay on
`awslogs` driver as compact JSON. Discovery on for Web. Task role grants sidecar
X-Ray + CloudWatch write access — no collector to run yourself.

> Uses account's **default VPC/subnets** for brevity. For production, wire own VPC, private
> subnets, HTTPS listener (ACM cert).

## 4. Get the URLs

```bash
terraform output web_url   # ALB root
terraform output mcp_url   # ALB /mcp
```

Open `web_url`, sign in with owner (forced password change on first login).

## 5. Add node agents (separate)

Fargate disallows privileged/DinD, so run agents elsewhere pointing at `web_url`:

- **ECS on EC2** — capacity provider with `privileged = true` task definitions running
  `cmind-node-agent`.
- **EKS** — Helm chart ([kubernetes.md](kubernetes.md)) with `nodeAgent.privileged=true`.

Set `NodeAgent__MainUrl=<web_url>`, `NodeAgent__AdvertiseUrl=<agent reachable url>`,
`NodeAgent__JwtSecret=<discovery_join_token>`. Agents self-register — see
[../operations/node-discovery.md](../operations/node-discovery.md).

## 6. Verify

```bash
aws logs tail /ecs/cmind --since 5m         # compact JSON logs
curl -s "$(terraform output -raw web_url)/version"
```

## Production notes

- Add HTTPS listener + ACM certificate; restrict ALB security group.
- Store secrets in AWS Secrets Manager / SSM, inject via task-definition `secrets` instead of
  plaintext `environment`.
- Enable RDS Multi-AZ + backups.
- Traces (X-Ray), metrics (CloudWatch EMF), logs (CloudWatch Logs) wired automatically via
  ADOT sidecar; correlate on `trace_id`. See
  [../operations/logging.md](../operations/logging.md#aws--x-ray--cloudwatch-adot-sidecar).
- App already points `OTEL_EXPORTER_OTLP_ENDPOINT` at in-task sidecar; repoint to external
  collector if you prefer to centralize.

## Copy-trading agent + Secrets Manager (S5)

`deploy/aws/copy-agent.tf` adds **copy-agent** ECS Fargate service hosting `CopyEngineSupervisor`
(`App:Copy:Enabled=true`, `App:Features:CopyTrading=true`) with **no ALB** — worker holding long-lived
cTrader sockets. DB connection string stored in **AWS Secrets Manager**, injected through
task's `secrets` block (execution role granted `secretsmanager:GetSecretValue` on just that secret),
not plaintext env. Each task's `NodeName` defaults to its container hostname (unique per Fargate task), so
DB lease attributes running profiles per task — two tasks never double-host one. Scale
`copy_agent_count` to add copy capacity; DataProtection key ring shared through Postgres, so any task
can decrypt stored Open API tokens.