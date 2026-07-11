# AWS deployment — step by step

`deploy/aws` is a Terraform module: **ECS Fargate** (Web + MCP) behind an **ALB**, **RDS Postgres**,
and CloudWatch logs.

## 1. Prerequisites

- Terraform ≥ 1.5 and AWS credentials (`aws configure` / env vars) with rights to create VPC-scoped
  resources, ECS, RDS, ALB, IAM.
- The three images in a registry ECS can pull (ECR, or GHCR public).

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

Creates: RDS Postgres (`appdb`), ECS cluster, Fargate services for Web + MCP, an ALB (Web at `/`,
MCP at `/mcp`), security groups, a CloudWatch log group, and an **ADOT (AWS Distro for
OpenTelemetry) collector sidecar** in each task. The app exports OTLP to the sidecar, which ships
traces to **X-Ray** and metrics to **CloudWatch** (EMF, namespace `cmind`); logs stay on the
`awslogs` driver as compact JSON. Discovery is enabled on Web. A task role grants the sidecar
X-Ray + CloudWatch write access — no collector to run yourself.

> Uses the account's **default VPC/subnets** for brevity. For production, wire your own VPC, private
> subnets, and an HTTPS listener (ACM cert).

## 4. Get the URLs

```bash
terraform output web_url   # ALB root
terraform output mcp_url   # ALB /mcp
```

Open `web_url`, sign in with the owner (forced password change on first login).

## 5. Add node agents (separate)

Fargate disallows privileged/DinD, so run agents elsewhere pointing at `web_url`:

- **ECS on EC2** — a capacity provider with `privileged = true` task definitions running
  `cmind-node-agent`.
- **EKS** — the Helm chart ([kubernetes.md](kubernetes.md)) with `nodeAgent.privileged=true`.

Set `NodeAgent__MainUrl=<web_url>`, `NodeAgent__AdvertiseUrl=<agent reachable url>`,
`NodeAgent__JwtSecret=<discovery_join_token>`. Agents self-register — see
[../operations/node-discovery.md](../operations/node-discovery.md).

## 6. Verify

```bash
aws logs tail /ecs/cmind --since 5m         # compact JSON logs
curl -s "$(terraform output -raw web_url)/version"
```

## Production notes

- Add an HTTPS listener + ACM certificate; restrict the ALB security group.
- Store secrets in AWS Secrets Manager / SSM and inject via task-definition `secrets` instead of
  plaintext `environment`.
- Enable RDS Multi-AZ + backups.
- Traces (X-Ray), metrics (CloudWatch EMF), and logs (CloudWatch Logs) are wired automatically via
  the ADOT sidecar; correlate on `trace_id`. See
  [../operations/logging.md](../operations/logging.md#aws--x-ray--cloudwatch-adot-sidecar).
- The app already points `OTEL_EXPORTER_OTLP_ENDPOINT` at the in-task sidecar; repoint it to an
  external collector if you prefer to centralize.

## Copy-trading agent + Secrets Manager (S5)

`deploy/aws/copy-agent.tf` adds a **copy-agent** ECS Fargate service that hosts the `CopyEngineSupervisor`
(`App:Copy:Enabled=true`, `App:Features:CopyTrading=true`) with **no ALB** — a worker holding the long-lived
cTrader sockets. The DB connection string is stored in **AWS Secrets Manager** and injected through the
task's `secrets` block (the execution role is granted `secretsmanager:GetSecretValue` on just that secret),
not as plaintext env. Each task's `NodeName` defaults to its container hostname (unique per Fargate task), so
the DB lease attributes running profiles per task and two tasks never double-host one. Scale
`copy_agent_count` to add copy capacity; the DataProtection key ring is shared through Postgres, so any task
can decrypt the stored Open API tokens.
