---
description: "deploy/azure/main.bicep dostarcza warstwę bezstanową na Azure Container Apps plus Postgres Flexible Server + Log Analytics."
---

# Wdrażanie Azure — krok po kroku

`deploy/azure/main.bicep` dostarcza warstwę bezstanową na **Azure Container Apps** plus **Postgres Flexible Server** + Log Analytics.

## 1. Wymagania wstępne

- Azure CLI (`az login` zrobione), subskrypcja, uprawnieni do tworzenia grup zasobów.
- Trzy obrazy popchnięte do rejestru, który Azure może pobrać (np. GHCR public, lub ACR).

## 2. Utwórz grupę zasobów

```bash
az group create -n cmind-rg -l westeurope
```

## 3. Wdrażaj Bicep

```bash
az deployment group create -g cmind-rg -f deploy/azure/main.bicep \
  -p imageRegistry=ghcr.io/your-org/cmind imageTag=1.0.0 \
     ownerEmail=you@example.com \
     ownerPassword='Change-Me-Str0ng!' \
     pgPassword="$(openssl rand -hex 16)" \
     discoveryJoinToken="$(openssl rand -hex 24)"
```

Tworzy: środowisko Container Apps, Web (ingress zewnętrzny), MCP (ingress zewnętrzny), Postgres Flexible Server + `appdb`, Log Analytics, **komponent Application Insights oparty na workspace**. Odkrycie włączone dla Web. Jego ciąg połączenia wtryskiwany do Web + MCP jako `APPLICATIONINSIGHTS_CONNECTION_STRING`, więc ślady + metryki eksportują natywnie do App Insights, podczas gdy dzienniki lądują w tym samym workspace Log Analytics — nie potrzeba kolektora. Przejdź `-p otlpEndpoint=...` do *także* do przodu do kolektora OTLP.

## 4. Uzyskaj adresy URL

```bash
az deployment group show -g cmind-rg -n main --query properties.outputs
# webUrl, mcpUrl
```

Otwórz `webUrl`, zaloguj się właścicielem (zmuszony zmiana hasła przy pierwszym logowaniu).

## 5. Dodaj agentów węzłów (oddzielnie)

Container Apps nie mogą uruchamiać uprzywilejowanych/DinD, więc uruchom agentów indziej, wskaż na `webUrl`:

- **AKS** — wdrażaj Helm chart ([kubernetes.md](kubernetes.md)) z `nodeAgent.privileged=true`, skaluj Web/MCP do 0, jeśli chcesz tam tylko warstwę agenta.
- **VM / VMSS** — uruchom obraz `cmind-node-agent` z `--privileged` z `NodeAgent:MainUrl=<webUrl>`, `NodeAgent:AdvertiseUrl=<vm reachable url>`, `NodeAgent:JwtSecret=<discoveryJoinToken>`.

Agenci samodzielnie rejestrują się w obrębie jednego interwału bicia serca — patrz [../operations/node-discovery.md](../operations/node-discovery.md).

## 6. Zweryfikuj

```bash
az containerapp logs show -g cmind-rg -n cmind-web --tail 50   # kompaktowe dzienniki JSON
curl -s <webUrl>/version
```

## Notatki produkcyjne

- Front Web z Azure Front Door / App Gateway dla TLS + WAF.
- Przechowuj sekrety w Key Vault; przejdź stabilny certyfikat Data Protection (`App__DataProtectionCertBase64` / `...Password`), więc pierścień klucza przetrwa ponowne uruchomienia repliki.
- App Insights (ślady+metryki) + Log Analytics (dzienniki) przewodowała automatycznie; korelyuj na `trace_id`. Patrz [../operations/logging.md](../operations/logging.md#azure--application-insights--log-analytics).
- Ustaw parametr `otlpEndpoint` (lub `OTEL_EXPORTER_OTLP_ENDPOINT` na aplikacjach) do *także* do przodu do kolektora.
- Container Apps `scale` zasady (min/max) przewodowała w Bicep.

## Agent kopii + Key Vault (S5)

`deploy/azure/main.bicep` również dostarcza **copy-agent** Container App hosting `CopyEngineSupervisor` (`App:Copy:Enabled=true`, `App:Features:CopyTrading=true`) z **bez ingress** — pracownik trzymający długotrwałe gniazda cTrader. Czyta ciąg połączenia DB z **Azure Key Vault** sekretu poprzez **użytkownika przypisaną tożsamość zarządzaną** (rola Vault Secrets User) zamiast wbudowanego sekretu plaintext. `NodeName` każdej repliki domyślnie wynosi jej nazwę hosta kontenera (unikalna), więc atrybuty leasingu DB profilami biegającymi na replika — dwie repliki nigdy nie gośćią dwa razy. Skaluj `minReplicas`/`maxReplicas` do dodania pojemności kopii; pierścień klucza DataProtection współdzielony przez Postgres, więc każda replika może odszyfrować przechowywane tokeny Open API. Wyjścia: `copyAgentName`, `keyVaultName`.
