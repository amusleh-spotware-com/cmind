# Azure deployment — step by step

`deploy/azure/main.bicep` provisions the stateless tier on **Azure Container Apps** plus a
**Postgres Flexible Server** and Log Analytics.

## 1. Prerequisites

- Azure CLI (`az login` done), a subscription, and permission to create resource groups.
- The three images pushed to a registry Azure can pull (e.g. GHCR public, or ACR).

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

This creates: Container Apps environment, Web (external ingress), MCP (external ingress), Postgres
Flexible Server + `appdb`, Log Analytics, and a **workspace-based Application Insights** component.
Discovery is enabled on Web. Its connection string is injected into Web and MCP as
`APPLICATIONINSIGHTS_CONNECTION_STRING`, so traces + metrics export natively to App Insights while
logs land in the same Log Analytics workspace — no collector needed. Pass `-p otlpEndpoint=...` to
*also* forward to an OTLP collector.

## 4. Get the URLs

```bash
az deployment group show -g cmind-rg -n main --query properties.outputs
# webUrl, mcpUrl
```

Open `webUrl`, sign in with the owner (forced password change on first login).

## 5. Add node agents (separate)

Container Apps can't run privileged/DinD, so run agents elsewhere and point them at `webUrl`:

- **AKS** — deploy the Helm chart ([kubernetes.md](kubernetes.md)) with `nodeAgent.privileged=true`,
  scaling Web/MCP to 0 if you only want the agent tier there.
- **VM / VMSS** — run the `cmind-node-agent` image `--privileged` with `NodeAgent:MainUrl=<webUrl>`,
  `NodeAgent:AdvertiseUrl=<vm reachable url>`, `NodeAgent:JwtSecret=<discoveryJoinToken>`.

Agents self-register within one heartbeat interval — see
[../operations/node-discovery.md](../operations/node-discovery.md).

## 6. Verify

```bash
az containerapp logs show -g cmind-rg -n cmind-web --tail 50   # compact JSON logs
curl -s <webUrl>/version
```

## Production notes

- Front Web with Azure Front Door / App Gateway for TLS + WAF.
- Store secrets in Key Vault; pass a stable Data Protection cert
  (`App__DataProtectionCertBase64` / `...Password`) so the key ring survives replica restarts.
- App Insights (traces+metrics) + Log Analytics (logs) are wired automatically; correlate on
  `trace_id`. See [../operations/logging.md](../operations/logging.md#azure--application-insights--log-analytics).
- Set the `otlpEndpoint` param (or `OTEL_EXPORTER_OTLP_ENDPOINT` on the apps) to *also* forward to a
  collector.
- Container Apps `scale` rules (min/max) are wired in the Bicep.
