# Azure deployment — step by step

`deploy/azure/main.bicep` provisions stateless tier on **Azure Container Apps** plus **Postgres Flexible Server** + Log Analytics.

## 1. Prerequisites

- Azure CLI (`az login` done), subscription, permission to create resource groups.
- Three images pushed to registry Azure can pull (e.g. GHCR public, or ACR).

## 2. Create a resource group

```bash
az group create -n cmind-rg -l westeurope
```

## 3. Deploy the Bicep

```bash
az deployment group create -g cmind-rg -f deploy/azure/main.bicep \
  -p imageRegistry=ghcr.io/your-org/cmind imageTag=1.0.0 \
     ownerEmail=you@example.com \
     ownerPassword='Change-Me-Str0ng!' \
     pgPassword="$(openssl rand -hex 16)" \
     discoveryJoinToken="$(openssl rand -hex 24)"
```

Creates: Container Apps environment, Web (external ingress), MCP (external ingress), Postgres Flexible Server + `appdb`, Log Analytics, **workspace-based Application Insights** component. Discovery on for Web. Its connection string injected into Web + MCP as `APPLICATIONINSIGHTS_CONNECTION_STRING`, so traces + metrics export natively to App Insights while logs land in same Log Analytics workspace — no collector needed. Pass `-p otlpEndpoint=...` to *also* forward to OTLP collector.

## 4. Get the URLs

```bash
az deployment group show -g cmind-rg -n main --query properties.outputs
# webUrl, mcpUrl
```

Open `webUrl`, sign in with owner (forced password change on first login).

## 5. Add node agents (separate)

Container Apps can't run privileged/DinD, so run agents elsewhere, point at `webUrl`:

- **AKS** — deploy Helm chart ([kubernetes.md](kubernetes.md)) with `nodeAgent.privileged=true`, scale Web/MCP to 0 if want only agent tier there.
- **VM / VMSS** — run `cmind-node-agent` image `--privileged` with `NodeAgent:MainUrl=<webUrl>`, `NodeAgent:AdvertiseUrl=<vm reachable url>`, `NodeAgent:JwtSecret=<discoveryJoinToken>`.

Agents self-register within one heartbeat interval — see [../operations/node-discovery.md](../operations/node-discovery.md).

## 6. Verify

```bash
az containerapp logs show -g cmind-rg -n cmind-web --tail 50   # compact JSON logs
curl -s <webUrl>/version
```

## Production notes

- Front Web with Azure Front Door / App Gateway for TLS + WAF.
- Store secrets in Key Vault; pass stable Data Protection cert (`App__DataProtectionCertBase64` / `...Password`) so key ring survives replica restarts.
- App Insights (traces+metrics) + Log Analytics (logs) wired automatically; correlate on `trace_id`. See [../operations/logging.md](../operations/logging.md#azure--application-insights--log-analytics).
- Set `otlpEndpoint` param (or `OTEL_EXPORTER_OTLP_ENDPOINT` on apps) to *also* forward to collector.
- Container Apps `scale` rules (min/max) wired in Bicep.

## Copy-trading agent + Key Vault (S5)

`deploy/azure/main.bicep` also provisions **copy-agent** Container App hosting `CopyEngineSupervisor` (`App:Copy:Enabled=true`, `App:Features:CopyTrading=true`) with **no ingress** — worker holding long-lived cTrader sockets. Reads DB connection string from **Azure Key Vault** secret via **user-assigned managed identity** (Key Vault Secrets User role) rather than inline plaintext secret. Each replica's `NodeName` defaults to its container hostname (unique), so DB lease attributes running profiles per replica and two replicas never double-host one. Scale `minReplicas`/`maxReplicas` to add copy capacity; DataProtection key ring shared through Postgres, so any replica can decrypt stored Open API tokens. Outputs: `copyAgentName`, `keyVaultName`.