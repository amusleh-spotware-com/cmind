---
title: Fehler-Pfad-Coverage-Kartographie
description: Jedes Fehler-Szenario, das das Mandat benötigt, kartographiert zu dem/den Test(s), die es tatsächlich ausüben — sodass eine Lücke sichtbar ist, nicht angenommen.
---

# Fehler-Pfad-Coverage-Kartographie

Das Test-Mandat ist explizit: **Fehler-Pfade zählen** — eine Änderung, die auf einer fallgelassenen Verbindung brechen kann, ein abgelehnter Order, ein Desync, eine Token-Rotation oder ein toter Knoten versendet mit einem Test dafür, in der gleichen Commit. Diese Seite kartographiert jedes erforderliches Szenario zu dem/den Test(s), die es ausüben, sodass eine echte Lücke *sichtbar* statt angenommen ist. Wenn Sie einen Fehler-Pfad hinzufügen, fügen Sie eine Zeile hier hinzu.

## Erforderliche Szenarien → Tests

| Szenario | Ebene(n) | Tests |
|---|---|---|
| **Verbindung fallen → Wiederverbindung** | Unit · Stress · E2E | `OpenApiConnectionTests.Dropped_connection_reconnects_and_raises_reconnected`; `FakeTradingSession.Disconnect/ReconnectAsync` und `SyncTradingSession` (DST); `MiscUiTests` Wiederverbindungs-Modal Staaten |
| **Order-Ablehnung** | Unit · Stress | `CopyTransparencyTests.A_rejected_open_emits_a_Failed_execution_fact_with_the_reason`; `CopyCircuitBreakerTests`; DST `CopyDstWorld.FailOrders` / `RejectMarketRange` |
| **Desync / Resync** | Unit · Stress | `CopyPartialFillTests.Resync_tops_up_a_broker_partial_fill…`; `CopyEngineHostTests.Reconnect_resync_closes_orphaned_destination_positions` (+ `…tolerates_a_position_not_found…`); `CopyAdvancedScenariosTests.Reconnect_resync_opens_missing_copies_and_closes_orphans_after_a_desync`; `CopyChaosDstTests` |
| **Token-Rotation / Ungültigmachung** | Unit · Integration · Stress | `OpenApiAuthorizationTests.MarkRefreshFailed_*` (Eskalations-Fenster); `FakeTradingSession.InvalidateToken`; `TokenRotationSignatureTests`, `LiveTokenBootstrapTests`, `OpenApiTokenRefreshPersistenceTests` (Integration); DST `RotateTokens` |
| **Knoten-Tod → Lease-Rückforderung** | Unit · Integration · Stress | `NodeInstanceReclaimerTests` (Unit + Integration); `CopyRulesDomainTests.Lease_is_held_only_by_the_claiming_node_until_it_expires`; `CopyHostWatchdogTests`, `CopyNodeAffinityTests`, `PropFirmTrackingLeaseTests` (Integration); `CopyLeaseReclaimStressTests` |
| **KI-Provider-Fehler (4xx/5xx/Timeout/Malformed)** | Unit · Integration | `AnthropicAiClientTests.Fails_gracefully_on_error_status` / `…on_malformed_json` / `…on_empty_content`; `AiHttpResilienceTests`, `AiRecommendDisabledTests` (Integration) |
| **KI vollständig deaktiviert (kein Schlüssel)** | Unit · Integration · E2E | `AiFeatureServiceTests`; `AiRecommendDisabledTests`; `AiPagesTests` |
| **Datenbank transient Fehler / Migrations-Lock** | Integration | `DatabaseResilienceTests`; `MigrationLockTests` |
| **Knoten-HTTP-Agent-Fehler / Retry** | Integration | `NodeAgentHttpResilienceTests` |
| **Container-Selbst-Ausgang-Versöhnung** | Unit | `BacktestCompletionPollerTests`; `RunCompletionPoller` Coverage in `ContainerCommandHelpersTests` |
| **Prop-Firm-Verstoß** | Unit · Integration | `PropFirmChallengeRulesTests`; `PropFirmAlertNotifierTests`; `PropFirmChallengePersistenceTests` |
| **Ungültige Eingabe / Auth-Ablehnung (UI + Branding)** | Unit · Integration · E2E | `LoginTests.Invalid_credentials_show_an_error`; `HexColorTests.Rejects_invalid_hex`; `BrandingOptionsValidatorTests` |

## Dünne Stellen — vor Annahme Abdeckung überprüfen

Diese sind wert einer expliziten Überprüfung (fügen Sie eine Zeile oben hinzu, sobald bestätigt oder gefüllt):

- **MCP-Tool-Auth-Ablehnung** — `McpKeyAuthHandler` lehnt einen schlechten/abwesenden Schlüssel ab. Kein dediziert Test wurde gefunden; fügen Sie einen Integrations-Test hinzu, der einen MCP-Tool-Endpunkt mit einem fehlenden/ungültig Schlüssel anruft und 401 behauptet.
- **cBot-Konstruktions-Fehler-Oberflächung** — ein Compile-Fehler muss auf der Instance/UI als `Failed` mit der Build-Output landen. `CBotLifecycleTests` abdeckt den Happy-Pfad; bestätigen Sie, dass der Fehler-Zweig behauptet wird.
- **Live-Order-Ausführung** — End-to-End-Copy-Ausführung gegen echte cTrader-Anmeldedaten bleibt gated (benötigt Anmeldedaten + einen Knoten-Cluster); siehe [Live-Copy-Trading](./live-copy-trading.md).

## Wie dies durchgesetzt wird

Die deterministische Stress-Suite (DST, `tests/StressTests`) wiedergeben diese Fehler auf einer komprimierten Uhr ab und muss Grün bleiben — **schwächen Sie nie ein DST-Szenario, um es bestanden zu machen; beheben Sie den Code**. Die [FakeTradingSession](./fake-trading-session.md) ist der cTrader-Treue-Simulator, diese Unit-Tests fahren; erweitern Sie ihn für neue Broker-Verhalten, anstatt eine Behauptung zu entspannen.
