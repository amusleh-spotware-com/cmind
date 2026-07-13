---
description: "White-Label-Bereitstellung verschifft selten jede Fähigkeit. Feature-Umschalter ermöglichen es dem Betreiber, Hauptprodukt-Features ein-/auszuschalten — zur Bereitstellungszeit über Konfiguration oder später…"
---

# Feature-Umschalter

White-Label-Bereitstellung verschifft selten jede Fähigkeit. Feature-Umschalter ermöglichen es dem Betreiber, Hauptprodukt-Features ein-/auszuschalten — zur Bereitstellungszeit über Konfiguration oder später zur Laufzeit, ohne Neubereitstellung. **Alle Features sind standardmäßig aktiviert**; Bereitstellung listet nur auf, welche sie ändert.

## Modell

- `Core.Features.FeatureFlag` — Enum von verschließbaren Features: `Authoring`, `Backtesting`, `Execution`, `CopyTrading`, `Ai`, `PortfolioAgent`, `Alerts`, `PropGuard`, `PropFirm`, `Accounts`, `OpenApi`, `Mcp`, `Compliance`. Core Admin-Oberflächen (Dashboard, Benutzer, Knoten, Authentifizierung) können nie verschlossen werden, nicht hier.
- `Core.Options.FeaturesOptions` — Konfiguration Basis, gebunden aus `App:Features`. Jede Eigenschaft wird standardmäßig `true`.
- `Core.Features.IFeatureGate` — löst **effektiven** Status auf: Konfiguration Basis überlagert mit optionaler vom Besitzer gesetzter Laufzeit-Überschreibung. Implementiert von `Infrastructure.Features.FeatureGate`, cached Überschreibungen kurzzeitig (`FeatureSettings.OverrideCacheTtl`), invalidiert bei Änderung.

Laufzeit-Überschreibungen gespeichert als `AppSetting` Reihen Schlüssel `feature.<FeatureFlag>` (Wert `true`/`false`). Keine Reihe = "Konfiguration Basis verwenden".

## Zwei Wege um ein Feature zu deaktivieren

### 1. Bereitstellungskonfiguration (Basis)

Flag `false` unter `App:Features` setzen. Beispiel `appsettings.json`:

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

Oder via Umgebungsvariablen (Doppelunterstrich):

```
App__Features__CopyTrading=false
```

Basis verschließt **Startup-Registrierung** von Background-Workern (`Nodes.AddNodes`) und MCP-Tools (`Mcp` Server), daher Feature deaktiviert in Konfiguration niemals starten seine gehosteten Services noch verfügbar machen seine MCP-Tools.

### 2. Laufzeit-Überschreibung (Besitzer)

Besitzer kann jedes Feature live von **Einstellungen → Features** (`/settings/features`) oder API umschalten:

```
GET  /api/features            -> [{ "flag": "CopyTrading", "enabled": true }, ...]   (Owner)
PUT  /api/features/{flag}      body { "enabled": false }  -> Überschreibung setzen             (Owner)
PUT  /api/features/{flag}      body { "enabled": null  }  -> Überschreibung löschen (zurücksetzen)  (Owner)
```

Laufzeit-Änderungen gelten sofort für Anfrage-Zeit-Verschließungen (Navigation, API). Background-Worker und MCP-Tools werden beim Startup verschlossen, nehmen Laufzeit-Änderung beim nächsten Prozess-Neustart auf.

## Was jedes Gate erzwingt

| Ebene | Mechanismus | Zeitpunkt |
|-------|-----------|--------|
| HTTP API | `RouteGroupBuilder.RequireFeature(flag)` Endpoint-Filter → `404` wenn deaktiviert | Laufzeit |
| Navigation | `NavMenu` verbergt Links via `IFeatureGate.IsEnabled` | Laufzeit |
| Background-Worker | bedingt `AddHostedService` in `Nodes.AddNodes` | Startup (Konfiguration) |
| MCP-Tools | bedingt `WithTools<>` im MCP-Server | Startup (Konfiguration) |

Feature erreichbar über Deep-Link während deaktiviert rendert leere Seite — seine API gibt `404` zurück; nav macht es nicht mehr an die Oberfläche.

## Flag → Oberflächen-Karte

| Flag | API-Gruppen | Nav | Worker / MCP |
|------|-----------|-----|----------------|
| Authoring | `/api/cbots`, `/api/paramsets`, `/api/builder` | cBots Gruppe → cBots (Param-Sets pro-cBot Dialog) | MCP `CBotTools` |
| Backtesting | (teilt sich `/api/instances`) | cBots Gruppe → Backtest | — |
| Execution | `/api/instances` | cBots Gruppe → Run | MCP `InstanceTools` |
| CopyTrading | `/api/copy` | Copy Trading | `CopyEngineSupervisor`, `OpenApiTokenRefreshService`, MCP `CopyTools` |
| Ai | `/api/ai` | KI Gruppe → KI; Einstellungen → KI (Schlüssel) | `AiRiskGuard`, MCP `AiTools` |
| PortfolioAgent | `/api/agent` | KI Gruppe → Portfolio Agent | `PortfolioAgentService` |
| Alerts | `/api/alerts` | KI Gruppe → Benachrichtigungen | `AlertEvaluator` |
| PropGuard | `/api/prop` | Prop Gruppe → Prop Guard | `PropGuardService` |
| PropFirm | `/api/prop-firm` | Prop Gruppe → Herausforderungen | — |
| Accounts | `/api/ctids` | Handelskonten | — |
| OpenApi | `/api/openapi` | Einstellungen → Open API | — |
| Mcp | `/api/mcp-keys` | KI Gruppe → MCP-Tasten | — |
| Compliance | `/api/compliance` | Einstellungen → Legal & Datenschutz | — |

## Tests

- **Unit** — `UnitTests/Features/FeaturesOptionsTests.cs`: Basis-Standard, Pro-Flag-Abbildung.
- **Integration** — `IntegrationTests/FeatureGateTests.cs`: Konfiguration Basis, Laufzeit-Überschreibung besiegt Konfiguration und persistiert als `AppSetting`, Löschen kehrt zu Basis zurück (real Postgres).
- **E2E** — `E2ETests/FeatureToggleTests.cs`: Deaktivieren `CopyTrading` zur Laufzeit verbergt seinen Nav-Link und `404`s `/api/copy`, Wiederaktivieren stellt beide wieder her.
