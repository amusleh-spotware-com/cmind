---
description: "AI pomočnik. Priporočite varnostni kopiranje-trgovinski odredišne nastavitve od sledilca tveganje profil + vir (glavni) račun opis. Izpostavljeni konec REST API, MCP…"
---

# Priporočilnik profila kopije AI

AI pomočnik. Priporočite varnostni kopiranje-trgovinski odredišne nastavitve od sledilca tveganje profil + vir (glavni) račun opis. Izpostavljeni konec REST API, MCP orodja, Kopiranje-trgovinski stran. Svetovalnega samo — nikoli ustvari/mutate profil; človek (ali sledi-up MCP klica) uporabite nastavitve.

## Model

- `IAiFeatureService.RecommendCopyProfileAsync(riskProfile, sourceDescription, ct)` — gradnja zahteve iz
  `AiPrompts.CopyProfileSystem` poziva, vrniti `AiResult` čigar besedilo = JSON predmet predlagane
  nastavitvi: `riskMode` (a `MoneyManagementMode` ime), `riskParameter`, `maxDrawdownPercent`, `dailyLossLimit`,
  `direction`, `copyStopLoss`, `copyTakeProfit`, `slippagePips`, kratko `rationale`.
- Kot vsaka značilnost AI, zaklenjena na `App:Ai:ApiKey`: brez ključa → klica vrniti
  `AiResult.Fail(disabled)`, aplikacija neizmenjeno.

## Površine

| Površina | Vnos |
|---------|-------|
| REST | `POST /api/ai/recommend-copy-profile` `{ riskProfile, sourceDescription }` → `AiResult` (značilnost `Ai`, vloga User+) |
| MCP | `CopyTools.RecommendCopyProfile(riskProfile, sourceDescription)` (značilnost `CopyTrading`, delegati na AI storitve) |
| UI | Kopiranje-trgovinski stran → **AI predlaga** gumb; priporočilo upodobi v vstavljena opozorila |

Priporočilo ni samodejno-uporabljeno namenoma: sledilec pregleda, nato ustvari profil /
odredišče skozi normalno Kopiranje-trgovinski razlog (ali MCP odjemalec razčleni JSON + klica
ustvariti končne točke).

## Testi

- **Enota** — `UnitTests/Ai/AiFeatureServiceRecommendTests.cs`: tveganje profil + vir opis
  poslan na AI odjemalec pod kopiranje-profil sistem poziva (NSubstitute).
- **Integracija** — `IntegrationTests/AiRecommendDisabledTests.cs`: brez API ključ → pravi
  `AnthropicAiClient` + `AiFeatureService` poslabšati na rezultat napake (aplikacija teče brez ključa).
- **E2E** — `E2ETests/AiCopyRecommendTests.cs`: **AI predlaga** gumb klica končne točke + upodobi
  rezultat (graciozno "ni konfiguriran" sporočilo v testnem env), dokazovanje UI → končne točke → AI pot.
