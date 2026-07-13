---
description: "AI pomocník. Odporúča bezpečné nastavenia copy-trading cieľa z rizikového profilu sledovateľa + popisu zdrojového (master) účtu. Vystavené cez REST API, MCP…"
---

# AI copy-profile odporúčač

AI pomocník. Odporúča bezpečné nastavenia copy-trading cieľa z rizikového profilu sledovateľa + popisu zdrojového (master) účtu. Vystavené cez REST API, MCP nástroj, stránku Copy Trading. Iba Advisory — nikdy nevytvára/nemení profil; človek (alebo follow-up MCP volanie) aplikuje nastavenia.

## Model

- `IAiFeatureService.RecommendCopyProfileAsync(riskProfile, sourceDescription, ct)` — zostaví požiadavku z
  `AiPrompts.CopyProfileSystem` promptu, vráti `AiResult` ktoreho text = JSON objekt navrhovaných
  nastavení: `riskMode` (názov `MoneyManagementMode`), `riskParameter`, `maxDrawdownPercent`, `dailyLossLimit`,
  `direction`, `copyStopLoss`, `copyTakeProfit`, `slippagePips`, krátke `rationale`.
- Rovnako ako každá AI funkcia, gated na `App:Ai:ApiKey`: bez kľúča → volanie vráti
  `AiResult.Fail(disabled)`, aplikácia nedotknutá.

## Plochy

| Plocha | Vstup |
|---------|-------|
| REST | `POST /api/ai/recommend-copy-profile` `{ riskProfile, sourceDescription }` → `AiResult` (funkcia `Ai`, rola User+) |
| MCP | `CopyTools.RecommendCopyProfile(riskProfile, sourceDescription)` (funkcia `CopyTrading`, deleguje na AI službu) |
| UI | Stránka Copy Trading → tlačidlo **AI suggest**; odporúčanie sa zobrazuje v inline alert |

Odporúčanie nie je automaticky aplikované zámerne: sledovateľ skontroluje, potom vytvorí profil /
cieľ cez normálny dialóg Copy Trading (alebo MCP klient parsujú JSON + volá create
endpoints).

## Testy

- **Jednotka** — `UnitTests/Ai/AiFeatureServiceRecommendTests.cs`: rizikový profil + popis zdroja
  preposielané AI klientovi pod copy-profile systémovým promptom (NSubstitute).
- **Integrácia** — `IntegrationTests/AiRecommendDisabledTests.cs`: bez API kľúča → reálny
  `AnthropicAiClient` + `AiFeatureService` degraduje na výsledok zlyhania (aplikácia beží bez kľúča).
- **E2E** — `E2ETests/AiCopyRecommendTests.cs`: tlačidlo **AI suggest** volá endpoint + renderuje
  výsledok (graceful "not configured" správa v testovacom prostredí), dokazuje UI → endpoint → AI cestu.
