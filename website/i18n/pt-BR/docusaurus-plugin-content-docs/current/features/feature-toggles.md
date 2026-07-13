---
description: "A implantação com marca branca raramente envia cada capacidade. Os alternadores de recursos permitem que o operador ative/desative recursos principais do produto — no tempo de implantação via configuração ou mais tarde em…"
---

# Alternadores de recursos

A implantação com marca branca raramente envia cada capacidade. Os alternadores de recursos permitem que o operador ative/desative recursos principais do produto — no tempo de implantação via configuração ou mais tarde no tempo de execução, sem reimplantar. **Todos os recursos são ativados por padrão**; implantação apenas lista os que muda.

## Modelo

- `Core.Features.FeatureFlag` — enum de recursos passíveis de serem fechados: `Authoring`, `Backtesting`, `Execution`, `CopyTrading`, `Ai`, `PortfolioAgent`, `Alerts`, `PropGuard`, `PropFirm`, `Accounts`, `OpenApi`, `Mcp`, `Compliance`. Superfícies de administração central (painel, usuários, nós, autenticação) nunca são passíveis de serem fechados, não aqui.
- `Core.Options.FeaturesOptions` — linha de base de configuração, vinculada a partir de `App:Features`. Cada propriedade padrão `true`.
- `Core.Features.IFeatureGate` — resolve **estado eficaz**: linha de base de configuração sobreposta com substituição de tempo de execução opcional definida pelo proprietário. Implementado por `Infrastructure.Features.FeatureGate`, armazena em cache substituições brevemente (`FeatureSettings.OverrideCacheTtl`), invalida na mudança.

Substituições de tempo de execução armazenadas como linhas `AppSetting` codificadas `feature.<FeatureFlag>` (valor `true`/`false`). Nenhuma linha = "usar linha de base de configuração".

## Duas maneiras de desabilitar um recurso

### 1. Configuração de implantação (linha de base)

Defina sinalização `false` em `App:Features`. Exemplo `appsettings.json`:

```json
{
  "App": {
    "Features": {
      "CopyTrading": false,
      "PropGuard": false
    }
  }
}
```

Ou via variáveis de ambiente (sublinhado duplo):

```
App__Features__CopyTrading=false
```

Linha de base fecha **registro de inicialização** de trabalhadores de background (`Nodes.AddNodes`) e ferramentas MCP (`Mcp` server), para que recurso desabilitado em configuração nunca inicie seus serviços hospedados nem exponha suas ferramentas MCP.

### 2. Substituição de tempo de execução (proprietário)

Proprietário pode inverter qualquer recurso ao vivo a partir de **Settings → Features** (`/settings/features`) ou API:

```
GET  /api/features            -> [{ "flag": "CopyTrading", "enabled": true }, ...]   (Proprietário)
PUT  /api/features/{flag}      corpo { "enabled": false }  -> definir substituição             (Proprietário)
PUT  /api/features/{flag}      corpo { "enabled": null  }  -> limpar substituição (reverter)  (Proprietário)
```

Mudanças de tempo de execução tomam efeito imediatamente para portões de tempo de solicitação (navegação, API). Trabalhadores de background e ferramentas MCP fechados na inicialização, pegam mudança de tempo de execução na próxima reinicialização do processo.

## O que cada portão aplica

| Camada | Mecanismo | Tempo |
|-------|-----------|--------|
| API HTTP | filtro de endpoint `RouteGroupBuilder.RequireFeature(flag)` → `404` quando desabilitado | Tempo de execução |
| Navegação | `NavMenu` oculta links via `IFeatureGate.IsEnabled` | Tempo de execução |
| Trabalhadores de background | `AddHostedService` condicional em `Nodes.AddNodes` | Inicialização (configuração) |
| Ferramentas MCP | `WithTools<>` condicional no servidor MCP | Inicialização (configuração) |

Recurso alcançado por link profundo enquanto desabilitado renderiza página vazia — sua API retorna `404`; nav não o superfície mais.

## Mapa de sinalizador → superfície

| Sinalização | Grupos de API | Nav | Trabalhadores / MCP |
|------|-----------|-----|----------------|
| Authoring | `/api/cbots`, `/api/paramsets`, `/api/builder` | grupo cBots → cBots (conjuntos param por-cBot diálogo) | MCP `CBotTools` |
| Backtesting | (compartilha `/api/instances`) | grupo cBots → Backtest | — |
| Execution | `/api/instances` | grupo cBots → Run | MCP `InstanceTools` |
| CopyTrading | `/api/copy` | Copy Trading | `CopyEngineSupervisor`, `OpenApiTokenRefreshService`, MCP `CopyTools` |
| Ai | `/api/ai` | AI grupo → IA; Settings → AI (chave) | `AiRiskGuard`, MCP `AiTools` |
| PortfolioAgent | `/api/agent` | AI grupo → Portfolio Agent | `PortfolioAgentService` |
| Alerts | `/api/alerts` | AI grupo → Alerts | `AlertEvaluator` |
| PropGuard | `/api/prop` | Prop grupo → Prop Guard | `PropGuardService` |
| PropFirm | `/api/prop-firm` | Prop grupo → Challenges | — |
| Accounts | `/api/ctids` | Trading Accounts | — |
| OpenApi | `/api/openapi` | Settings → Open API | — |
| Mcp | `/api/mcp-keys` | AI grupo → MCP Keys | — |
| Compliance | `/api/compliance` | Settings → Legal & Privacy | — |

## Testes

- **Unidade** — `UnitTests/Features/FeaturesOptionsTests.cs`: padrões de linha de base, mapeamento por sinalização.
- **Integração** — `IntegrationTests/FeatureGateTests.cs`: linha de base de configuração, substituição de tempo de execução bate configuração e persiste como `AppSetting`, limpeza reverte para linha de base (Postgres real).
- **E2E** — `E2ETests/FeatureToggleTests.cs`: desabilitar `CopyTrading` no tempo de execução oculta seu link de navegação e faz `404` em `/api/copy`, re-habilitar restaura ambos.
