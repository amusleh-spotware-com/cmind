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
MCP at `/mcp`), security groups, and a CloudWatch log group. Discovery is enabled on Web.

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
- Set `OTEL_EXPORTER_OTLP_ENDPOINT` in the task definitions to forward logs+traces+metrics.
