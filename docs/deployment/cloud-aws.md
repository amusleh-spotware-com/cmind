# AWS deployment

`deploy/aws` is a Terraform module that provisions the stateless tier on **ECS Fargate** (Web + MCP)
behind an **ALB**, with an **RDS Postgres** database and CloudWatch logs.

```bash
cd deploy/aws
terraform init
terraform apply \
  -var image_registry=ghcr.io/your-org/cmind \
  -var image_tag=1.0.0 \
  -var owner_email=you@example.com \
  -var owner_password='Change-Me-Str0ng!' \
  -var pg_password="$(openssl rand -hex 16)" \
  -var discovery_join_token="$(openssl rand -hex 24)"
```

Outputs `web_url` (ALB root) and `mcp_url` (ALB `/mcp`). Containers log compact JSON to the
`/ecs/cmind` CloudWatch log group; set `OTEL_EXPORTER_OTLP_ENDPOINT` in the task definitions to also
forward to a collector.

> The module uses the account's **default VPC/subnets** for brevity. For production, wire it to your
> own VPC, private subnets, and an HTTPS listener (ACM cert).

## Node agents on AWS

Fargate disallows privileged/DinD, so node agents run elsewhere:

1. **ECS on EC2** — a capacity provider with `privileged = true` task definitions running
   `cmind-node-agent`.
2. **EKS** — the Helm chart (`deploy/helm/cmind`) with `nodeAgent.privileged=true`.

Point `NodeAgent__MainUrl` at the ALB `web_url`, set `NodeAgent__AdvertiseUrl` to each agent's
reachable address, and `NodeAgent__JwtSecret` = `discovery_join_token`. Agents self-register — see
`docs/operations/node-discovery.md`.

## Production notes

- Add an HTTPS listener + ACM certificate; restrict the ALB security group.
- Store secrets in AWS Secrets Manager / SSM and inject via task-definition `secrets` instead of
  plaintext `environment`.
- Enable RDS Multi-AZ + backups for the database.
