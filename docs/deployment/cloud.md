# Cloud deployment — overview

Stateless tier (Web + MCP) run on any container platform; Postgres = managed database.
**Node agents need privileged Docker (DinD)** — serverless container runtimes (Azure Container
Apps, AWS Fargate) block it. Run agents on Kubernetes ([kubernetes.md](kubernetes.md)) or
VM/EC2, point at Web URL.

| Cloud | Stateless tier | Database | Guide |
| ----- | -------------- | -------- | ----- |
| Azure | Container Apps (Bicep) | Postgres Flexible Server | [cloud-azure.md](cloud-azure.md) |
| AWS | ECS Fargate + ALB (Terraform) | RDS Postgres | [cloud-aws.md](cloud-aws.md) |

Common prerequisites, both:

1. Build + push three images to registry cloud can pull (`cmind-web`, `cmind-mcp`,
   `cmind-node-agent`).
2. Pick secrets: DB password, owner email/password, **discovery join token** (≥ 32 chars)
   shared by Web app + every node agent.
3. Deploy IaC (below), then bring node agents up separately (K8s/VM) with
   `NodeAgent__MainUrl` = deployed Web URL, `NodeAgent__JwtSecret` = join token.

Discovery, logging, probes behave same as local/K8s setups — see
[../operations/node-discovery.md](../operations/node-discovery.md) and
[../operations/logging.md](../operations/logging.md).