# Azure deployment

`deploy/azure/main.bicep` provisions the stateless tier on **Azure Container Apps** plus a
**Postgres Flexible Server** and Log Analytics.

```bash
az group create -n cmind-rg -l westeurope

az deployment group create -g cmind-rg -f deploy/azure/main.bicep \
  -p imageRegistry=ghcr.io/your-org/cmind imageTag=1.0.0 \
     ownerEmail=you@example.com \
     ownerPassword='Change-Me-Str0ng!' \
     pgPassword="$(openssl rand -hex 16)" \
     discoveryJoinToken="$(openssl rand -hex 24)"
```

Outputs `webUrl` and `mcpUrl`. Container Apps captures stdout (compact JSON logs) into Log
Analytics; set `OTEL_EXPORTER_OTLP_ENDPOINT` on the apps to also forward to a collector.

## Node agents on Azure

Container Apps cannot run privileged/DinD workloads, so **node agents do not run there**. Options:

1. **AKS** — deploy the Helm chart (`deploy/helm/cmind`) with `web.enabled`/`mcp.enabled` scaled to
   0 (or just install the node-agent portion) and `nodeAgent.privileged=true`, pointing
   `NodeAgent__MainUrl` at the Container Apps `webUrl`.
2. **VM / VMSS** — run the `cmind-node-agent` image with `--privileged`, setting `NodeAgent:MainUrl`,
   `NodeAgent:AdvertiseUrl` (the VM's reachable URL), `NodeAgent:JwtSecret` = the deployment's
   `discoveryJoinToken`.

Agents self-register to the Web app; see `docs/operations/node-discovery.md`.

## Production notes

- Front the Web app with Azure Front Door / App Gateway for TLS + WAF.
- Use a Key Vault-backed secret store; pass a stable Data Protection cert
  (`App__DataProtectionCertBase64` / `...Password`) so the key ring survives replica restarts.
- Scale Web/MCP with the Container Apps `scale` rules (already min/max wired in the Bicep).
