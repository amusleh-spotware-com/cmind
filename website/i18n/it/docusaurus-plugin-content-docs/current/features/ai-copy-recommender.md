---
description: "AI helper. Consiglia impostazioni sicure per copy-trading dal profilo di rischio del follower + descrizione account sorgente (master). Esposto su REST API, MCP tool, pagina Copy Trading. Solo Advisory — non crea/muta mai profilo; umano (o chiamata MCP di follow-up) applica le impostazioni."
---

# AI copy-profile recommender

AI helper. Consiglia impostazioni sicure per copy-trading dal profilo di rischio del follower + descrizione account sorgente (master). Esposto su REST API, MCP tool, pagina Copy Trading. Solo Advisory — non crea/muta mai profilo; umano (o chiamata MCP di follow-up) applica le impostazioni.

## Modello

- `IAiFeatureService.RecommendCopyProfileAsync(riskProfile, sourceDescription, ct)` — costruisce richiesta da
  prompt `AiPrompts.CopyProfileSystem`, restituisce `AiResult` il cui text = oggetto JSON delle impostazioni
  suggerite: `riskMode` (un nome `MoneyManagementMode`), `riskParameter`, `maxDrawdownPercent`, `dailyLossLimit`,
  `direction`, `copyStopLoss`, `copyTakeProfit`, `slippagePips`, breve `rationale`.
- Come ogni feature AI, gated su `App:Ai:ApiKey`: no key → chiamata restituisce
  `AiResult.Fail(disabled)`, app non modificata.

## Superfici

| Superficie | Entry |
|---------|-------|
| REST | `POST /api/ai/recommend-copy-profile` `{ riskProfile, sourceDescription }` → `AiResult` (feature `Ai`, role User+) |
| MCP | `CopyTools.RecommendCopyProfile(riskProfile, sourceDescription)` (feature `CopyTrading`, delega all'AI service) |
| UI | Pagina Copy Trading → pulsante **AI suggest**; la raccomandazione renderizza in un alert inline |

Raccomandazione non applicata automaticamente di proposito: il follower revisiona, poi crea profilo /
destinazione attraverso il normale dialogo Copy Trading (o il client MCP parsifica JSON + chiama create
endpoints).

## Test

- **Unit** — `UnitTests/Ai/AiFeatureServiceRecommendTests.cs`: profilo di rischio + descrizione sorgente
  inoltrati all'AI client sotto copy-profile system prompt (NSubstitute).
- **Integration** — `IntegrationTests/AiRecommendDisabledTests.cs`: nessuna API key → real
  `AnthropicAiClient` + `AiFeatureService` degradano a failure result (app gira senza key).
- **E2E** — `E2ETests/AiCopyRecommendTests.cs`: pulsante **AI suggest** chiama endpoint + renderizza
  risultato (messaggio graceful "not configured" in test env), provando il percorso UI → endpoint → AI.
