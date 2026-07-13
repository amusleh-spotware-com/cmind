---
description: "deploy/azure/main.bicep provisionuje bezestavovou vrstvu na Azure Container Apps plus Postgres Flexible Server + Log Analytics."
---

# Nasazení na Azure — krok za krokem

`deploy/azure/main.bicep` provisionuje bezestavovou vrstvu na **Azure Container Apps** plus **Postgres Flexible Server** + Log Analytics.

## 1. Předpoklady

- Azure CLI (`az login` hotovo), subscription, oprávnění vytvářet resource groups.
- Tři image pushnuté do registru, ze kterého může Azure stahovat (např. GHCR public, nebo ACR).

## 2. Vytvořte resource group

```bash
az group create -n cmind-rg -l westeurope
```

## 3. Deploy Bicep

```bash
az deployment group create -g cmind-rg -f deploy/azure/main.bicep \
  -p imageRegistry=ghcr.io/your-org/cmind imageTag=1.0.0 \
     ownerEmail=you@example.com \
     ownerPassword='Change-Me-Str0ng!' \
     pgPassword="$(openssl rand -hex 16)" \
     discoveryJoinToken="$(openssl rand -hex 24)"
```

Vytvoří: Container Apps environment, Web (external ingress), MCP (external ingress), Postgres Flexible Server + `appdb`, Log Analytics, **workspace-based Application Insights** komponent. Discovery zapnuto pro Web. Jeho connection string je injectovaný do Web + MCP jako `APPLICATIONINSIGHTS_CONNECTION_STRING`, takže traces + metriky exportují nativně do App Insights zatímco logy přistávají ve stejném Log Analytics workspace — žádný collector není třeba. Předejte `-p otlpEndpoint=...` pro *také* forwardování do OTLP collectoru.

## 4. Získejte URL

```bash
az deployment group show -g cmind-rg -n main --query properties.outputs
# webUrl, mcpUrl
```

Otevřete `webUrl`, přihlaste se jako owner (při prvním přihlášení vynucena změna hesla).

## 5. Přidání node agentů (samostatně)

Container Apps nemůže spouštět privilegované/DinD, takže agenty spouštějte jinde a směřujte je na `webUrl`:

- **AKS** — deploy Helm chart ([kubernetes.md](kubernetes.md)) s `nodeAgent.privileged=true`, škálujte Web/MCP na 0 pokud chcete pouze agent vrstvu.
- **VM / VMSS** — spusťte `cmind-node-agent` image `--privileged` s `NodeAgent:MainUrl=<webUrl>`, `NodeAgent:AdvertiseUrl=<vm reachable url>`, `NodeAgent:JwtSecret=<discoveryJoinToken>`.

Agenti se sami registrují do jednoho heartbeat intervalu — viz [../operations/node-discovery.md](../operations/node-discovery.md).

## 6. Ověření

```bash
az containerapp logs show -g cmind-rg -n cmind-web --tail 50   # compact JSON logy
curl -s <webUrl>/version
```

## Produkční poznámky

- Předejte Azure Front Door / App Gateway pro TLS + WAF.
- Ukládejte tajemství do Key Vault; předejte stabilní Data Protection cert (`App__DataProtectionCertBase64` / `...Password`) aby key ring přežil restarty replik.
- App Insights (traces+metriky) + Log Analytics (logy) jsou automaticky propojeny; korelace přes `trace_id`. Viz [../operations/logging.md](../operations/logging.md#azure--application-insights--log-analytics).
- Nastavte `otlpEndpoint` param (nebo `OTEL_EXPORTER_OTLP_ENDPOINT` na aplikacích) pro *také* forwardování do collectoru.
- Container Apps `scale` pravidla (min/max) jsou zapojená v Bicep.

## Copy-trading agent + Key Vault (S5)

`deploy/azure/main.bicep` také provisionuje **copy-agent** Container App hostující `CopyEngineSupervisor` (`App:Copy:Enabled=true`, `App:Features:CopyTrading=true`) **bez ingressu** — worker držící dlouho běžící cTrader sockety. Čte DB connection string z **Azure Key Vault** secret přes **user-assigned managed identity** (Key Vault Secrets User role) místo inline plaintext secret. Každá replica's `NodeName` defaultuje na svůj container hostname (unique), takže DB lease atribuuje běžící profily per replica a dvě repliky nikdy nehostují jednu duplikátně. Škálujte `minReplicas`/`maxReplicas` pro přidání copy kapacity; DataProtection key ring sdílen přes Postgres, takže jakákoliv replica může dešifrovat uložené Open API tokeny. Výstupy: `copyAgentName`, `keyVaultName`.
