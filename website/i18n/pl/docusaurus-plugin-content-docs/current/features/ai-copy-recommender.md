---
description: "Helper AI. Rekomenduj bezpieczne ustawienia docelowe copy-tradingu z profilu ryzyka obserwatora + opisu konta źródła (mastera). Dostępne przez REST API, MCP…"
---

# Rekomendator profilu kopii AI

Helper AI. Rekomenduj bezpieczne ustawienia docelowe copy-tradingu z profilu ryzyka obserwatora + opisu konta źródła (mastera). Dostępne przez REST API, narzędzie MCP, stronę Copy Trading. Tylko doradztwo — nigdy nie tworzy/nie mutuje profilu; człowiek (lub kolejne wywołanie MCP) stosuje ustawienia.

## Model

- `IAiFeatureService.RecommendCopyProfileAsync(riskProfile, sourceDescription, ct)` — zbuduj żądanie z
  promptu `AiPrompts.CopyProfileSystem`, zwróć `AiResult` którego text = obiekt JSON sugerowanych
  ustawień: `riskMode` (nazwa `MoneyManagementMode`), `riskParameter`, `maxDrawdownPercent`, `dailyLossLimit`,
  `direction`, `copyStopLoss`, `copyTakeProfit`, `slippagePips`, krótkie `rationale`.
- Jak każda funkcja AI, gated na `App:Ai:ApiKey`: brak klucza → wywołanie zwraca
  `AiResult.Fail(disabled)`, aplikacja bez zmian.

## Powierzchnie

| Powierzchnia | Wpis |
|---------|-------|
| REST | `POST /api/ai/recommend-copy-profile` `{ riskProfile, sourceDescription }` → `AiResult` (funkcja `Ai`, rola User+) |
| MCP | `CopyTools.RecommendCopyProfile(riskProfile, sourceDescription)` (funkcja `CopyTrading`, delegaty do usługi AI) |
| UI | Strona Copy Trading → przycisk **AI suggest**; rekomendacja renderuje się w inline alert'cie |

Rekomendacja nie zostaje automatycznie zastosowana celowo: obserwator przegląda, następnie tworzy profil /
miejsce docelowe przez normalny dialog Copy Trading (lub klient MCP parsuje JSON + wywołuje create
endpoints).

## Testy

- **Unit** — `UnitTests/Ai/AiFeatureServiceRecommendTests.cs`: profil ryzyka + opis źródła
  przesyłane do klienta AI pod kopii-profilu system prompt (NSubstitute).
- **Integracja** — `IntegrationTests/AiRecommendDisabledTests.cs`: brak klucza API → rzeczywisty
  `AnthropicAiClient` + `AiFeatureService` degradują się do wyniku niepowodzenia (aplikacja działa bez klucza).
- **E2E** — `E2ETests/AiCopyRecommendTests.cs`: przycisk **AI suggest** wywołuje endpoint + renderuje
  wynik (łagodna wiadomość "not configured" w środowisku testowym), udowadniając ścieżkę UI → endpoint → AI.
