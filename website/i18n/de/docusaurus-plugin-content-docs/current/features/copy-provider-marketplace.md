---
description: "Browsbar-Verzeichnis von Copy-Strategien. Provider *veröffentlicht* Copy-Profil als Eintrag mit überprüft-live Badge (Strategie-Quellkonto handelt echtes Geld, nicht…"
---

# Copy-Anbieter-Marktplatz (Phase 4)

Durchsuchbares Verzeichnis von Copy-Strategien. Provider **veröffentlicht** Copy-Profil als Eintrag mit **verifiziert-live** Badge (Strategie-Quellkonto handelt echtes Geld, nicht Demo) plus Performancegebühr. Follower durchsuchen Marktplatz, sortiert nach Leistungsscore projiziert von Ausführungs-Transparenzdaten.

## Modell

- `CopyProviderListing` = Aggregate: `UserId`, `ProfileId`, Anzeigename, Beschreibung, Performancegebühr, `VerifiedLive`, `Published` + `PublishedAt`. Ein Eintrag pro Profil (eindeutiger Index).
- **Verifiziert-live** abgeleitet zur Veröffentlichungszeit aus Profil-Quelle `TradingAccount.IsLive` — Anbieter kann nicht selbst-behaupten.
- Leistungsstats **nicht auf Eintrag gespeichert** — Read-Model-Projektion über `CopyExecution` Transparenz-Log (Füllrate, durchschnittliche Latenz, durchschnittlicher realisierter Slippage), daher Marktplatz widerspiegelt immer Live-Ausführungsqualität.

## Ranking

`CopyEndpoints.MarketplaceScore(fillRate, avgLatencyMs, avgSlippagePoints, verifiedLive)` → 0–100 Score: Füllrate dominiert (×60), niedrige Latenz + niedriger Slippage adden (×20 jeweils), verifiziert-live Badge addet kleinen Vertrauens-Bonus. Deterministisch + monoton, daher Bestellung stabil.

## API

- `POST /api/copy/profiles/{id}/publish` — veröffentliche/update Profil-Eintrag (`DisplayName`, `Description`, `PerformanceGebührProzent`); verifiziert-live von Quellkonto gesetzt.
- `DELETE /api/copy/profiles/{id}/publish` — rückgängig-veröffentliche.
- `GET /api/copy/marketplace` — alle veröffentlichten Einträge, sortiert, jeweils mit Leistungs-Zusammenfassung (Ausführungen, Füllrate, durchschnittliche Latenz, durchschnittlicher Slippage, Score) + verifiziert-live Badge.

## Tests

- **Unit** (`CopyProviderListingTests`) — Aggregate-Invarianten: Anzeigename erforderlich; Veröffentlichung set Zeitstempel; Veröffentlichung-Rückgängig verbergen; Update ersetze Anzeige-Felder + Gebühr + Badge.
- **Integration** (`CopyMarketplaceTests`, real Postgres) — veröffentlichter Eintrag persistiert mit Badge; ein Eintrag pro Profil (eindeutiger Index); Ranking-Score bevorzugt verifiziert/hohe-Füllung Anbieter.

Copy-Host unberührt (Einträge + Lese-Modell nur), daher Copy-DST-Stress-Suite unbeeinträchtigt.
