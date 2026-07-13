---
description: "AI pomoćnik. Preporučuje bezbedne postavke za destinaciju kopiranja na osnovu profila rizika pratioca i opisa izvornog (master) računa. Izloženo preko REST API-ja, MCP alata, stranice za kopiranje trgovine…"
---

# AI preporučivač profila za kopiranje

AI pomoćnik. Preporučuje bezbedne postavke za destinaciju kopiranja na osnovu profila rizika pratioca i opisa izvornog (master) računa. Izloženo preko REST API-ja, MCP alata, stranice za kopiranje trgovine. Samo savet — nikada ne kreira/mutira profil; čovek (ili naknadni MCP poziv) primenjuje postavke.

## Model

- `IAiFeatureService.RecommendCopyProfileAsync(riskProfile, sourceDescription, ct)` — gradi zahtev iz
  `AiPrompts.CopyProfileSystem` prompta, vraća `AiResult` čiji tekst = JSON objekat predloženih
  postavki: `riskMode` (naziv `MoneyManagementMode`), `riskParameter`, `maxDrawdownPercent`, `dailyLossLimit`,
  `direction`, `copyStopLoss`, `copyTakeProfit`, `slippagePips`, kratko obrazloženje.
- Kao i svaka AI funkcija, kontrolisano `App:Ai:ApiKey`: bez ključa → poziv vraća
  `AiResult.Fail(disabled)`, aplikacija nepromenjena.

## Površine

| Površina | Ulaz |
|---------|-------|
| REST | `POST /api/ai/recommend-copy-profile` `{ riskProfile, sourceDescription }` → `AiResult` (funkcija `Ai`, rola User+) |
| MCP | `CopyTools.RecommendCopyProfile(riskProfile, sourceDescription)` (funkcija `CopyTrading`, delegira na AI servis) |
| UI | Stranica za kopiranje trgovine → dugme **AI predlog**; preporuka se prikazuje u inline obaveštenju |

Preporuka se ne primenjuje automatski namerno: pratioc pregleda, zatim kreira profil /
destinaciju kroz normalni dijalog za kopiranje trgovine (ili MCP klijent parsira JSON + pozove create
endpoint-e).

## Testovi

- **Unit** — `UnitTests/Ai/AiFeatureServiceRecommendTests.cs`: risk profile + source description
  prosleđeni AI klijentu pod copy-profile system promptom (NSubstitute).
- **Integration** — `IntegrationTests/AiRecommendDisabledTests.cs`: nema API ključa → realni
  `AnthropicAiClient` + `AiFeatureService` degradaju do rezultata greške (aplikacija radi bez ključa).
- **E2E** — `E2ETests/AiCopyRecommendTests.cs`: dugme **AI predlog** poziva endpoint + prikazuje
  rezultat (graceful poruka "nije konfigurisano" u test okruženju), dokazujući UI → endpoint → AI putanju.
