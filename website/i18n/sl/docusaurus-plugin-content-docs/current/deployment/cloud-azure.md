---
description: "deploy/azure/main.bicep predvidi brezstalnega nivoja na Azure Container Apps plus Postgres Flexible Server + Log Analytics."
---

# Postavitev Azure — korak za korakom

`deploy/azure/main.bicep` predvidi brezstalnega nivoja na **Azure Container Apps** plus **Postgres Flexible Server** + Log Analytics.

## 1. Predpogoji

- Azure CLI (`az login` narejen), naročnina, dovoljenje za ustvarjanje skupin virov.
- Tri slike potisnjen v register Azure lahko povlečkuje (npr. GHCR javno ali ACR).

## 2. Ustvarite skupino virov

```bash
az group create -n cmind-rg -l westeurope
```

## 3. Uvrstite Bicep

```bash
az deployment group create -g cmind-rg -f deploy/azure/main.bicep \
  -p imageRegistry=ghcr.io/your-org/cmind imageTag=1.0.0 \
     ownerEmail=you@example.com \
     ownerPassword='Change-Me-Str0ng!' \
     pgPassword="$(openssl rand -hex 16)" \
     discoveryJoinToken="$(openssl rand -hex 24)"
```

Ustvari: Container Apps okolje, Web (zunanji vstop), MCP (zunanji vstop), Postgres Flexible Server + `appdb`, Log Analytics, **delavnici-primerka Application Insights** komponento. Odkrivanje za Web. Njen niz povezave injiciran v Web + MCP kot `APPLICATIONINSIGHTS_CONNECTION_STRING`, zato se sledi + metrike izvozijo rodno na App Insights, medtem ko dnevniki pristanejo v isto Log Analytics delovno — nobenega zbiranja potreba. Prosledi `-p otlpEndpoint=...` do *tudi* naprej na OTLP zbiralnik.

## 4. Pridobite naslove URL

```bash
az deployment group show -g cmind-rg -n main --query properties.outputs
# webUrl, mcpUrl
```

Odprite `webUrl`, se prijavite z lastnikom (prisilna sprememba gesla ob prvem prijavi).

## 5. Dodajte vozlišče agente (ločeno)

Container Apps ne more teči privilegiran/DinD, zato tečete agente drugje, imejte točko na `webUrl`:

- **AKS** — uvrstite Helm grafikon ([kubernetes.md](kubernetes.md)) z `nodeAgent.privileged=true`, lestvica Web/MCP na 0, če želite samo agent nivo tam.
- **VM / VMSS** — teče `cmind-node-agent` sliko `--privileged` z `NodeAgent:MainUrl=<webUrl>`, `NodeAgent:AdvertiseUrl=<vm dosegljiv URL>`, `NodeAgent:JwtSecret=<discoveryJoinToken>`.

Agenti se samogostovajo znotraj intervala utripa — glej [../operations/node-discovery.md](../operations/node-discovery.md).

## 6. Potrdite

```bash
az containerapp logs show -g cmind-rg -n cmind-web --tail 50   # kompaktni JSON dnevniki
curl -s <webUrl>/version
```

## Produkcijske opombe

- Sprednja Web z Azure Front Door / App Gateway za TLS + WAF.
- Shranjujte skrivnosti v Key Vault; prosledi stabilen certifikat Data Protection (`App__DataProtectionCertBase64` / `...Password`) zato ključni prstan preživi replika ponovno zagon.
- App Insights (sledi+metrike) + Log Analytics (dnevniki) žičani avtomatično; korelativno na `trace_id`. Glej [../operations/logging.md](../operations/logging.md#azure--application-insights--log-analytics).
- Nastavite `otlpEndpoint` param (ali `OTEL_EXPORTER_OTLP_ENDPOINT` na aplikacijami) do *tudi* naprej na zbiralnik.
- Container Apps `scale` pravili (min/max) žičani v Bicep.

## Agent kopiranja + Key Vault (S5)

`deploy/azure/main.bicep` tudi predvidi **copy-agent** Container App gostovanje `CopyEngineSupervisor` (`App:Copy:Enabled=true`, `App:Features:CopyTrading=true`) z **brez vstopa** — delavec, ki drži dolgoživje cTrader vtičnike. Bere DB niz povezave iz **Azure Key Vault** skrivnost preko **uporabnika-dodeljene upravljane istovetnosti** (Key Vault Secrets User vloga) precej kot vstaviti golo besediloskkrivnost. Vsaka replika `NodeName` privzeto njene vsebnik ime (edinstveno), zato DB zakupa atributov tečeče profile na repliko in dve repliki nikoli ne sogostuje ene. Lestvica `minReplicas`/`maxReplicas` za dodajanje kopije zmogljivosti; DataProtection ključni prstan deliti skozi Postgres, zato lahko vsaka replika dešifrira shranjene Open API žetone. Izlazi: `copyAgentName`, `keyVaultName`.
