---
description: "deploy/azure/main.bicep fornisce il tier stateless su Azure Container Apps più Postgres Flexible Server + Log Analytics."
---

# Deployment Azure — passo dopo passo

`deploy/azure/main.bicep` fornisce il tier stateless su **Azure Container Apps** più **Postgres Flexible Server** + Log Analytics.

## 1. Prerequisiti

- Azure CLI (`az login` effettuato), sottoscrizione, permesso di creare resource group.
- Tre immagini pushate nel registry Azure che può scaricare (es. GHCR pubblico, o ACR).

## 2. Crea un resource group

```bash
az group create -n cmind-rg -l westeurope
```

## 3. Distribuisci il Bicep

```bash
az deployment group create -g cmind-rg -f deploy/azure/main.bicep \
  -p imageRegistry=ghcr.io/your-org/cmind imageTag=1.0.0 \
     ownerEmail=you@example.com \
     ownerPassword='Change-Me-Str0ng!' \
     pgPassword="$(openssl rand -hex 16)" \
     discoveryJoinToken="$(openssl rand -hex 24)"
```

Crea: ambiente Container Apps, Web (ingresso esterno), MCP (ingresso esterno), Postgres Flexible Server + `appdb`, Log Analytics, **componente Application Insights basato su workspace**. Discovery attivo per Web. La sua stringa di connessione iniettata in Web + MCP come `APPLICATIONINSIGHTS_CONNECTION_STRING`, così traces + metriche esportano nativamente in App Insights mentre i log atterrano nello stesso workspace Log Analytics — nessun collector necessario. Passare `-p otlpEndpoint=...` per *inoltrare anche* a collector OTLP.

## 4. Ottieni gli URL

```bash
az deployment group show -g cmind-rg -n main --query properties.outputs
# webUrl, mcpUrl
```

Apri `webUrl`, accedi con il proprietario ( cambio password forzato al primo login).

## 5. Aggiungi agenti nodo (separato)

Container Apps non può eseguire privileged/DinD, quindi eseguire gli agenti altrove, puntare a `webUrl`:

- **AKS** — deployare Helm chart ([kubernetes.md](kubernetes.md)) con `nodeAgent.privileged=true`, scalare Web/MCP a 0 se si desidera solo il tier agente lì.
- **VM / VMSS** — eseguire immagine `cmind-node-agent` `--privileged` con `NodeAgent:MainUrl=<webUrl>`, `NodeAgent:AdvertiseUrl=<url vm raggiungibile>`, `NodeAgent:JwtSecret=<discoveryJoinToken>`.

Gli agenti si auto-registrano entro un intervallo di heartbeat — vedere [../operations/node-discovery.md](../operations/node-discovery.md).

## 6. Verifica

```bash
az containerapp logs show -g cmind-rg -n cmind-web --tail 50   # log JSON compatti
curl -s <webUrl>/version
```

## Note di produzione

- Mettere davanti Web con Azure Front Door / App Gateway per TLS + WAF.
- Memorizzare i secret in Key Vault; passare un certificato Data Protection stabile (`App__DataProtectionCertBase64` / `...Password`) così il key ring sopravvive ai riavvii delle repliche.
- App Insights (traces+metriche) + Log Analytics (log) cablati automaticamente; correlare su `trace_id`. Vedere [../operations/logging.md](../operations/logging.md#azure--application-insights--log-analytics).
- Impostare il parametro `otlpEndpoint` (o `OTEL_EXPORTER_OTLP_ENDPOINT` sulle app) per *inoltrare anche* a collector.
- Regole `scale` di Container Apps (min/max) cablate nel Bicep.

## Copy-trading agent + Key Vault (S5)

`deploy/azure/main.bicep` fornisce anche **copy-agent** Container App che ospita `CopyEngineSupervisor` (`App:Copy:Enabled=true`, `App:Features:CopyTrading=true`) **senza ingresso** — worker che mantiene socket cTrader long-lived. Legge la stringa di connessione DB dal secret **Azure Key Vault** tramite **identità gestita assegnata dall'utente** (ruolo Key Vault Secrets User) invece di secret inline in testo chiaro. Il `NodeName` di ogni replica default al suo container hostname (unico), così il lease DB attribuisce i profili in esecuzione per replica e due repliche non hostano mai doppione. Scalare `minReplicas`/`maxReplicas` per aggiungere capacità di copy; il key ring DataProtection condiviso tramite Postgres, così qualsiasi replica può decrittare i token Open API memorizzati. Output: `copyAgentName`, `keyVaultName`.
