# Cloud deployment — overview

The stateless tier (Web + MCP) runs on any container platform; Postgres is a managed database.
**Node agents need privileged Docker (DinD)**, which serverless container runtimes (Azure Container
Apps, AWS Fargate) do not allow — run agents on Kubernetes ([kubernetes.md](kubernetes.md)) or a
VM/EC2 and point them at the Web URL.

| Cloud | Stateless tier | Database | Guide |
| ----- | -------------- | -------- | ----- |
| Azure | Container Apps (Bicep) | Postgres Flexible Server | [cloud-azure.md](cloud-azure.md) |
| AWS | ECS Fargate + ALB (Terraform) | RDS Postgres | [cloud-aws.md](cloud-aws.md) |

Common prerequisites for both:

1. Build + push the three images to a registry the cloud can pull (`cmind-web`, `cmind-mcp`,
   `cmind-node-agent`).
2. Choose secrets: DB password, owner email/password, and a **discovery join token** (≥ 32 chars)
   shared by the Web app and every node agent.
3. Deploy the IaC (below), then bring node agents up separately (K8s/VM) with
   `NodeAgent__MainUrl` = the deployed Web URL and `NodeAgent__JwtSecret` = the join token.

Discovery, logging, and probes behave identically to the local/K8s setups — see
[../operations/node-discovery.md](../operations/node-discovery.md) and
[../operations/logging.md](../operations/logging.md).
