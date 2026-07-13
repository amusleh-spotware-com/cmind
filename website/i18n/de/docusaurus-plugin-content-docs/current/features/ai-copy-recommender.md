---
description: "KI-Helfer. Empfehlen Sie sichere Copy-Trading-Zieleinstellungen aus dem Risikoprofil des Followers + Kontobeschreibung der Quelle (Master). Über REST-API, MCP…"
---

# KI-Copy-Profil-Empfehler

KI-Helfer. Empfehlen Sie sichere Copy-Trading-Zieleinstellungen aus dem Risikoprofil des Followers + Kontobeschreibung der Quelle (Master). Über REST-API, MCP-Tool, Copy-Trading-Seite verfügbar. Nur beratend — Profile nie erstellen/ändern; Mensch (oder nachfolgende MCP-Aufrufe) wenden Einstellungen an.

## Modell

- `IAiFeatureService.RecommendCopyProfileAsync(riskProfile, sourceDescription, ct)` — Anfrage aus `AiPrompts.CopyProfileSystem`-Prompt zusammenstellen, `AiResult` zurückgeben, dessen Text = JSON-Objekt der vorgeschlagenen Einstellungen: `riskMode` (ein `MoneyManagementMode`-Name), `riskParameter`, `maxDrawdownPercent`, `dailyLossLimit`, `direction`, `copyStopLoss`, `copyTakeProfit`, `slippagePips`, kurze `rationale`.
- Wie jedes KI-Feature, gated auf `App:Ai:ApiKey`: kein Schlüssel → Anruf gibt `AiResult.Fail(disabled)` zurück, App unbeeinträchtigt.

## Oberflächen

| Oberfläche | Eintrag |
|---------|-------|
| REST | `POST /api/ai/recommend-copy-profile` `{ riskProfile, sourceDescription }` → `AiResult` (Feature `Ai`, Rolle User+) |
| MCP | `CopyTools.RecommendCopyProfile(riskProfile, sourceDescription)` (Feature `CopyTrading`, delegiert zum KI-Service) |
| UI | Copy-Trading-Seite → **KI-Vorschlag**-Schaltfläche; die Empfehlung wird in einer Inline-Benachrichtigung angezeigt |

Empfehlung wird absichtlich nicht automatisch angewendet: Follower prüft, dann erstellt Profil / Ziel über normalen Copy-Trading-Dialog (oder MCP-Client analysiert JSON + ruft Create-Endpoints auf).

## Tests

- **Unit** — `UnitTests/Ai/AiFeatureServiceRecommendTests.cs`: Risikoprofil + Kontobeschreibung an KI-Client unter Copy-Profil-Systemprompt weitergeleitet (NSubstitute).
- **Integration** — `IntegrationTests/AiRecommendDisabledTests.cs`: kein API-Schlüssel → real `AnthropicAiClient` + `AiFeatureService` degenieren zum Fehlerergebnis (App läuft ohne Schlüssel).
- **E2E** — `E2ETests/AiCopyRecommendTests.cs`: **KI-Vorschlag**-Schaltfläche ruft Endpoint auf + rendert Ergebnis (in der Test-Umgebung elegante "nicht konfiguriert"-Meldung), beweis UI → Endpoint → KI-Pfad.
