---
description: "deploy/azure/main.bicep zriaďuje bezstavovú vrstvu na Azure Container Apps plus Postgres Flexible Server + Log Analytics."
---

# Azure nasadenie — krok za krokom

`deploy/azure/main.bicep` zriaďuje bezstavovú vrstvu na **Azure Container Apps** plus **Postgres Flexible Server** + Log Analytics.

## 1. Predpoklady

- Azure CLI (`az login` hotovo), predplatné, oprávnenie na vytvorenie skupín zdrojov.
- Tri obrázky zasunuté do registra, ktorý Azure môže ťahať (napr. GHCR verejné, alebo ACR).

## 2. Vytvorte skupinu zdrojov

```bash
az group create -n cmind-rg -l westeurope
```

## 3. Nasaďte Bicep

```bash
az deployment group create -g cmind-rg -f deploy/azure/main.bicep \
  -p imageRegistry=ghcr.io/your-org/cmind imageTag=1.0.0 \
     ownerEmail=you@example.com \
     ownerPassword='Change-Me-Str0ng!' \
     pgPassword="$(openssl rand -hex 16)" \
     discoveryJoinToken="$(openssl rand -hex 24)"
```

Vytvára: prostredie Container Apps, Web (externý ingress), MCP (externý ingress), Postgres Flexible Server + `appdb`, Log Analytics, **workspace-based Application Insights** komponent. Objavovanie zapnuté pre Web. Jeho connection string injektovaný do Web + MCP ako `APPLICATIONINSIGHTS_CONNECTION_STRING`, takže stopy + metriky exportujú natívne do App Insights, zatiaľ čo logy pristanú v rovnakom Log Analytics pracovnom priestore — žiadny kolektor nepotrebný. Prejdite `-p otlpEndpoint=...` na *tiež* doprednú do OTLP kolektora.

## 4. Získajte URLs

```bash
az deployment group show -g cmind-rg -n main --query properties.outputs
# webUrl, mcpUrl
```

Otvorte `webUrl`, prihláste sa vlastníkom (vynútená zmena hesla pri prvom prihlásení).

## 5. Pridajte agenti uzlov (samostatne)

Container Apps nemôžu spustiť privilegované/DinD, takže spustite agenti inde, nasmerujte na `webUrl`:

- **AKS** — nasaďte Helm chart ([kubernetes.md](kubernetes.md)) s `nodeAgent.privileged=true`, škálujte Web/MCP na 0, ak chcete iba vrstvu agenta tam.
- **VM / VMSS** — spustite `cmind-node-agent` obrázok `--privileged` s `NodeAgent:MainUrl=<webUrl>`, `NodeAgent:AdvertiseUrl=<vm reachable url>`, `NodeAgent:JwtSecret=<discoveryJoinToken>`.

Agenti sa samoreg stratujú v rámci jedného intervalu pulzov — pozrite si [../operations/node-discovery.md](../operations/node-discovery.md).

## 6. Overujte

```bash
az containerapp logs show -g cmind-rg -n cmind-web --tail 50   # kompaktné JSON logy
curl -s <webUrl>/version
```

## Poznámky výroby

- Front Web s Azure Front Door / App Gateway pre TLS + WAF.
- Uložte tajomstvá v Key Vault; prejdite stabilný Data Protection certifikát (`App__DataProtectionCertBase64` / `...Password`) aby kľúčenka prežila restartovanie replík.
- App Insights (stopy+metriky) + Log Analytics (logy) zapojené automaticky; korelácia na `trace_id`. Pozrite si [../operations/logging.md](../operations/logging.md#azure--application-insights--log-analytics).
- Nastavte parameter `otlpEndpoint` (alebo `OTEL_EXPORTER_OTLP_ENDPOINT` na aplikáciách) na *tiež* doprednú do kolektora.
- Container Apps pravidlá `scale` (min/max) zapojené v Bicep.

## Copy-trading agent + Key Vault (S5)

`deploy/azure/main.bicep` tiež zriaďuje **copy-agent** Container App hostovanie `CopyEngineSupervisor` (`App:Copy:Enabled=true`, `App:Features:CopyTrading=true`) s **bez ingress** — pracovník s dlhodobými zásuvkami cTrader. Číta connection string DB z **Azure Key Vault** tajomstva cez **uživateľský pridelený spravovanú identitu** (úloha Key Vault Secrets User) skôr ako inline plaintext tajomstvo. Každá replika `NodeName` štandardne na svoj hostname kontajnera (jedinečný), takže DB lease atribúty bežiace profily na repliku a dvaja repliky nikdy neushostujú jeden. Škálujte `minReplicas`/`maxReplicas` na pridanie kapacity kópií; DataProtection kľúčenka zdieľaná cez Postgres, takže akákoľvek replika môže dešifrovať uložené Open API tokeny. Výstupy: `copyAgentName`, `keyVaultName`.
