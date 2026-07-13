---
description: "deploy/azure/main.bicep stellt zustandslosen Tier auf Azure Container Apps plus Postgres Flexible Server + Log Analytics bereit."
---

# Azure-Bereitstellung — Schritt für Schritt

`deploy/azure/main.bicep` stellt zustandslosen Tier auf **Azure Container Apps** plus **Postgres Flexible Server** + Log Analytics bereit.

## 1. Voraussetzungen

- Azure CLI (`az login` erledigt), Abonnement, Berechtigung zum Erstellen von Ressourcengruppen.
- Drei Images in eine Registry gepusht, die Azure abrufen kann (z. B. GHCR öffentlich oder ACR).

## 2. Eine Ressourcengruppe erstellen

```bash
az group create -n cmind-rg -l westeurope
```

## 3. Das Bicep bereitstellen

```bash
az deployment group create -g cmind-rg -f deploy/azure/main.bicep \
  -p imageRegistry=ghcr.io/your-org/cmind imageTag=1.0.0 \
     ownerEmail=you@example.com \
     ownerPassword='Change-Me-Str0ng!' \
     pgPassword="$(openssl rand -hex 16)" \
     discoveryJoinToken="$(openssl rand -hex 24)"
```

Erstellt: Container Apps-Umgebung, Web (externe Eingangsroute), MCP (externe Eingangsroute), Postgres Flexible Server + `appdb`, Log Analytics, **arbeitsbereichsbasierte Application Insights**-Komponente. Ermittlung für Web aktiviert. Seine Verbindungszeichenfolge wird in Web + MCP als `APPLICATIONINSIGHTS_CONNECTION_STRING` eingespritzt, damit Traces + Metriken nativ zu App Insights exportieren, während Protokolle im gleichen Log Analytics-Arbeitsbereich landen — kein Collector erforderlich. Übergeben Sie `-p otlpEndpoint=...`, um *auch* zu OTLP Collector weiterzuleiten.

## 4. URLs abrufen

```bash
az deployment group show -g cmind-rg -n main --query properties.outputs
# webUrl, mcpUrl
```

Öffnen Sie `webUrl`, melden Sie sich mit dem Besitzer an (erzwungener Passwortänderung beim ersten Login).

## 5. Node-Agenten hinzufügen (separate)

Container Apps können nicht privilegiert/DinD ausführen, daher führen Sie Agenten an anderer Stelle aus und verweisen auf `webUrl`:

- **AKS** — stellen Sie Helm-Diagramm ([kubernetes.md](kubernetes.md)) mit `nodeAgent.privileged=true` bereit, skalieren Sie Web/MCP auf 0, wenn Sie dort nur den Agent-Tier möchten.
- **VM / VMSS** — führen Sie `cmind-node-agent`-Image `--privileged` mit `NodeAgent:MainUrl=<webUrl>`, `NodeAgent:AdvertiseUrl=<vm erreichbare url>`, `NodeAgent:JwtSecret=<discoveryJoinToken>` aus.

Agenten registrieren sich innerhalb eines Heartbeat-Intervalls selbst — siehe [../operations/node-discovery.md](../operations/node-discovery.md).

## 6. Überprüfung

```bash
az containerapp logs show -g cmind-rg -n cmind-web --tail 50   # kompakte JSON-Protokolle
curl -s <webUrl>/version
```

## Produktionshinweise

- Front-Web mit Azure Front Door / App Gateway für TLS + WAF.
- Speichern Sie Geheimnisse in Key Vault; übergeben Sie stabiles Data Protection-Zertifikat (`App__DataProtectionCertBase64` / `...Password`), damit der Schlüsselring die Replikarestarts überlebt.
- App Insights (Traces + Metriken) + Log Analytics (Protokolle) werden automatisch verdrahtet; korrelieren Sie auf `trace_id`. Siehe [../operations/logging.md](../operations/logging.md#azure--application-insights--log-analytics).
- Setzen Sie `otlpEndpoint`-Parameter (oder `OTEL_EXPORTER_OTLP_ENDPOINT` auf Apps), um *auch* zu Collector weiterzuleiten.
- Container Apps `scale`-Regeln (min/max) sind in Bicep verdrahtet.

## Copy-Trading-Agent + Key Vault (S5)

`deploy/azure/main.bicep` stellt auch **copy-agent** Container App bereit, die `CopyEngineSupervisor` hostet (`App:Copy:Enabled=true`, `App:Features:CopyTrading=true`) ohne **Eingangsroute** — Worker, der langlebige cTrader-Sockets hält. Liest DB-Verbindungszeichenfolge aus dem **Azure Key Vault**-Secret über die **vom Benutzer zugewiesene verwaltete Identität** (Key Vault Secrets User-Rolle) anstelle von Inline-Klartext-Secret. Der `NodeName` jedes Replikats standardmäßig auf ihrem Container-Hostnamen (eindeutig), daher DB-Lease-Attribute ausführende Profile pro Replikat und zwei Replikate hosten niemals eine doppelt. Skalieren Sie `minReplicas`/`maxReplicas`, um Kopierkapazität zu erhöhen; DataProtection-Schlüsselring, der über Postgres freigegeben wird, daher kann jedes Replikat gespeicherte Open-API-Token entschlüsseln. Ausgaben: `copyAgentName`, `keyVaultName`.
