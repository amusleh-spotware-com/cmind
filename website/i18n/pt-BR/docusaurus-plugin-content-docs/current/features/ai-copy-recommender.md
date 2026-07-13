---
description: "Ajuda AI. Recomenda configuracoes seguras de destino de copy-trading a partir do perfil de risco do seguidor + descricao da conta fonte (mestre). Exposto via REST API, MCP…"
---

# Recomendador de perfil de copia com IA

Ajuda AI. Recomenda configuracoes seguras de destino de copy-trading a partir do perfil de risco do seguidor + descricao da conta fonte (mestre). Exposto via REST API, ferramenta MCP, pagina de Copy Trading. Somente consultoria — nunca cria/muta perfil; humano (ou chamada MCP de seguimento) aplica as configuracoes.

## Modelo

- `IAiFeatureService.RecommendCopyProfileAsync(riskProfile, sourceDescription, ct)` — constroi requisicao a partir do
  prompt `AiPrompts.CopyProfileSystem`, retorna `AiResult` cujo texto = objeto JSON com configuracoes sugeridas:
  `riskMode` (um nome `MoneyManagementMode`), `riskParameter`, `maxDrawdownPercent`, `dailyLossLimit`,
  `direction`, `copyStopLoss`, `copyTakeProfit`, `slippagePips`, curta `rationale`.
- Como toda funcionalidade AI, controlado por `App:Ai:ApiKey`: sem chave → chamada retorna
  `AiResult.Fail(disabled)`, app inalterado.

## Superficies

| Superficie | Entrada |
|---------|-------|
| REST | `POST /api/ai/recommend-copy-profile` `{ riskProfile, sourceDescription }` → `AiResult` (funcionalidade `Ai`, papel User+) |
| MCP | `CopyTools.RecommendCopyProfile(riskProfile, sourceDescription)` (funcionalidade `CopyTrading`, delega ao servico AI) |
| UI | Pagina Copy Trading → botao **AI suggest**; a recomendacao renderiza em um alerta inline |

Recomendacao nao aplicada automaticamente de proposito: seguidor revisa, entao cria perfil /
destino atraves do dialogo normal de Copy Trading (ou cliente MCP parseia JSON + chama
endpoints de criacao).

## Testes

- **Unidade** — `UnitTests/Ai/AiFeatureServiceRecommendTests.cs`: perfil de risco + descricao da fonte
  encaminhadas ao cliente AI sob o prompt de sistema de copy-profile (NSubstitute).
- **Integracao** — `IntegrationTests/AiRecommendDisabledTests.cs`: sem chave de API → real
  `AnthropicAiClient` + `AiFeatureService` degradam para resultado de falha (app executa sem chave).
- **E2E** — `E2ETests/AiCopyRecommendTests.cs`: botao **AI suggest** chama endpoint + renderiza
  resultado (mensagem graceful "not configured" no ambiente de teste), provando caminho UI → endpoint → AI.
