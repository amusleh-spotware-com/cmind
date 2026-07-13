---
description: "deploy/azure/main.bicep provisiona camada sem estado em Azure Container Apps mais Postgres Flexible Server + Log Analytics."
---

# Implantação Azure — passo a passo

`deploy/azure/main.bicep` provisiona camada sem estado em **Azure Container Apps** mais **Postgres Flexible Server** + Log Analytics.

## 1. Pré-requisitos

- Azure CLI (`az login` feito), subscrição, permissão para criar grupos de recursos.
- Três imagens enviadas para o registro que Azure pode extrair (por exemplo, GHCR público ou ACR).

## 2. Crie um grupo de recursos

```bash
az group create -n cmind-rg -l westeurope
```

## 3. Implante o Bicep

```bash
az deployment group create -g cmind-rg -f deploy/azure/main.bicep \
  -p imageRegistry=ghcr.io/your-org/cmind imageTag=1.0.0 \
     ownerEmail=you@example.com \
     ownerPassword='Change-Me-Str0ng!' \
     pgPassword="$(openssl rand -hex 16)" \
     discoveryJoinToken="$(openssl rand -hex 24)"
```

Cria: ambiente Container Apps, Web (ingresso externo), MCP (ingresso externo), Postgres Flexible Server + `appdb`, Log Analytics, componente **Application Insights baseado em workspace**. Descoberta ativada para Web. Sua string de conexão injetada em Web + MCP como `APPLICATIONINSIGHTS_CONNECTION_STRING`, então rastreamentos + métricas exportam nativamente para App Insights enquanto logs pousam no mesmo espaço de trabalho Log Analytics — nenhum coletor necessário. Passe `-p otlpEndpoint=...` para *também* encaminhar para coletor OTLP.

## 4. Obter os URLs

```bash
az deployment group show -g cmind-rg -n main --query properties.outputs
# webUrl, mcpUrl
```

Abra `webUrl`, entre com proprietário (alteração de senha forçada no primeiro login).

## 5. Adicionar agentes de nó (separadamente)

Container Apps não pode executar privilegiado/DinD, então execute agentes em outro lugar, aponte para `webUrl`:

- **AKS** — implante gráfico Helm ([kubernetes.md](kubernetes.md)) com `nodeAgent.privileged=true`, escale Web/MCP para 0 se quiser apenas camada de agente lá.
- **VM / VMSS** — execute imagem `cmind-node-agent` `--privileged` com `NodeAgent:MainUrl=<webUrl>`, `NodeAgent:AdvertiseUrl=<vm reachable url>`, `NodeAgent:JwtSecret=<discoveryJoinToken>`.

Agentes se auto-registram em um intervalo de batida cardíaca — veja [../operations/node-discovery.md](../operations/node-discovery.md).

## 6. Verifique

```bash
az containerapp logs show -g cmind-rg -n cmind-web --tail 50   # logs JSON compactos
curl -s <webUrl>/version
```

## Notas de produção

- Frente Web com Azure Front Door / App Gateway para TLS + WAF.
- Armazene segredos no Key Vault; passe certificado Data Protection estável (`App__DataProtectionCertBase64` / `...Password`) para que anel de chave sobreviva reinicializações de réplica.
- App Insights (rastreamentos+métricas) + Log Analytics (logs) conectados automaticamente; correlacionar em `trace_id`. Veja [../operations/logging.md](../operations/logging.md#azure--application-insights--log-analytics).
- Defina parâmetro `otlpEndpoint` (ou `OTEL_EXPORTER_OTLP_ENDPOINT` em apps) para *também* encaminhar para coletor.
- Regras de `scale` do Container Apps (min/max) conectadas em Bicep.

## Agente de copy-trading + Key Vault (S5)

`deploy/azure/main.bicep` também provisiona **copy-agent** Container App hospedando `CopyEngineSupervisor` (`App:Copy:Enabled=true`, `App:Features:CopyTrading=true`) com **sem ingresso** — trabalhador segurando soquetes cTrader de longa vida. Lê string de conexão DB do segredo **Azure Key Vault** via **identidade gerenciada atribuída pelo usuário** (função User de Segredos do Key Vault) em vez de segredo de texto puro inline. O `NodeName` padrão de cada réplica é seu nome de host do contêiner (único), então DB arrendamento atribui perfis em execução por réplica e duas réplicas nunca duplo-host um. Escale `minReplicas`/`maxReplicas` para adicionar capacidade de cópia; anel de chave DataProtection compartilhado através de Postgres, então qualquer réplica pode descriptografar tokens Open API armazenados. Saídas: `copyAgentName`, `keyVaultName`.
