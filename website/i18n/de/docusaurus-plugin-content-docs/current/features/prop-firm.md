---
description: "Retail Prop-Firmen (FTMO-Stil) verkaufen Evaluierungs-Konten: Trader muss Gewinn-Ziel erreichen während innerhalb Risiko-Grenzen bleibend (Max täglich-Verlust, Max…"
---

# Prop-Firm-Herausforderungs-Simulation

Retail Prop-Firmen (FTMO-Stil) verkaufen **Evaluierungs-Konten**: Trader muss Gewinn-Ziel erreichen während innerhalb Risiko-Grenzen bleibend (Max täglich-Verlust, Max total/Trailing Drawdown, Konsistenz, Zeit-Grenzen) bevor Finanziert. cMind lässt Benutzer **Maßgeschneiderte Herausforderung jeder Industrie-Form erstellen**, Bild an `TradingAccount`, **läufe wie Copy-Trading-Operation** — gestartet/gestoppt, gehostet auf Knoten, verfolgter **live über cTrader Open API**. Aggregate evaluiert jeden Regel deterministisch; auf Pass oder Bruch, endet Herausforderung, markiert es, warnt Benutzer.

## Domäne (Bounded Context: PropFirm)

`PropFirmChallenge` = Aggregate-Wurzel (Modul `Core.PropFirm`), verweist auf sein `TradingAccount` durch Stark-ID nur (kein Cross-Aggregate FK). Besitzt Regel-Evaluierung, Phase/Zustand-Maschine, Knoten-Anspruch.

### Wertobjekte & Regelwerk

- **`Money`** (nicht-negativ), **`MoneyAmount`** (unterzeichnet), **`Percent`** (0–100], **`TradingDayRequirement`** (0–365).
- **`EquitySnapshot`** `(equity, balance)` — lese an Aggregate gefüttert.
- **`ActivitySnapshot`** `(openPositions, openedInNewsWindow, holdingOverWeekend)` — nicht-Eigenkapital Fakten.
- **`DailyLossLimit`** `(percent, basis)` — Basis `Equity` (intraday, enthält floating P&L) oder `Balance` (realisiert nur).
- **`DrawdownLimit`** — `Static` (von Startsaldo), `TrailingPercent` (von Peak-Eigenkapital), oder `TrailingThresholdDollar` (Trails Eigenkapital Peak um festen Dollar-Betrag, dann **Sperren bei Startsaldo** einmal Eigenkapital erreicht Schwelle — Futures-Stil).
- **`ConsistencyRule`** `(maxSingleDayShareOfProfit)` — blockiert Pass während ein Tag dominiert Gesamtgewinn.
- **`ChallengeRules`** trägt oben plus `MaxCalendarDays`, `MaxInactivityDays`, `MaxOpenPositions`, `AllowWeekendHolding`, `AllowNewsTrading`, `Kind`, `SingleStep`. Regel-Mathe leben auf VOs (`DrawdownLimit.IsBreached`, `DailyLossLimit.IsBreached`, `ConsistencyRule.IsSatisfied`); Aggregate orchestriert.

### Herausforderungs-Arten & Vorlagen

`ChallengeTemplates.For(kind)` baut gültig Vorset für `OnePhase`, `TwoPhase`, `ThreePhase`, `InstantFunding`, oder `Custom` (vollständig Kontrolle). UI pre-füllt Vorlag; Benutzer kann jedes Feld anpassen.

### Phasen & Status

- **Phasen:** `Evaluation → Verification → Funded` (einzeln-Schritt überspringt Verification).
- **Status:** `Active`, `Passed`, `Failed`, plus Lebenszyklus `Stopped` (Verfolgung pausiert) — `Create` beginnt Herausforderung `Active`; `Stop()`/`Resume()` umschalter `Active↔Stopped`.
- **`BreachReason`:** `DailyLoss`, `MaxDrawdown`, `Consistency`, `TimeLimit`, `Inactivity`, `WeekendHolding`, `NewsTrading`, `MaxExposure`.

### Regel-Evaluierung

- **`RecordEquity(EquitySnapshot, now)`** — Walzen Handels-Tag bei Tag-Grenzen (erfasst vorher Tag Gewinn für Konsistenz Regel), aktualisiert Peak/täglich Peaks, dann **fehlschlag bei erste Bruch** (täglich Verlust → Drawdown → Zeit-Limit → Inaktivität, in Reihenfolge) oder Fortschritt Phase wenn Gewinn-Ziel, Minimum-Handels-Tag, Konsistenz Anforderungen alles erfüllt. Out-of-Order Snapshots und Datensätze auf Terminal Herausforderung werfen `DomainException`.
- **`RecordActivity(ActivitySnapshot, now)`** — evaluiert Verhalten-Regeln (Max offene Positionen, Wochenend-Haltung, Nachrichten-Handel), Stempel Aktivität für Inaktivitäts-Regel.
- Sanft **`PropFirmDrawdownWarning`** Feuer einmal wenn Eigenkapital-Verwendung überquert konfigurierbar Schwelle.

Domänen-Ereignisse: `PropFirmChallengeStarted`, `PropFirmChallengeStopped`, `PropFirmPhasePassed`, `PropFirmChallengePassed`, `PropFirmChallengeBreached`, `PropFirmDrawdownWarning`.

## Live-Verfolgung (Execution) — Knoten-gehostet, Selbst-heilend

Verfolgung Spiegel Copy-Trading-Hosting-Stack exakt; Prop Tracker = **lesen-nur** Cousin Copy-Engine.

- **`PropFirmTrackingSupervisor`** (`src/Nodes/PropFirm`) — `BackgroundService` auf jedem Knoten, gated auf `App:PropFirm:Enabled`. Jeder Zyklus **Anspruch** aktiven Herausforderungen auf Selbst-heilend Anspruch (`AssignedNode` + `LeaseExpiresAt`; Tote Knoten Herausforderungen rückgefordert einmal Anspruch lapses — gleich atomare `ExecuteUpdate` Anspruch wie Copy-Trading, daher zwei Knoten doppelten Spur nie), erneuert Ansprüche, drückt rotierter Tokens an Stelle, stoppt Hosts deren Herausforderung verlief `Active`.
- **`PropFirmTrackingHost`** (`src/Nodes/PropFirm`) — eins pro Herausforderung. Öffnet `IOpenApiTradingSession` für Konto und auf `App:PropFirm:EquityPollInterval`, berechnet neu Live-Eigenkapital, füttert zu Aggregate. Tauscht Zugriff-Token an Ort auf Rotation (kein Sitzungs-Tropfen). Beendet wenn Herausforderung nicht mehr `Active`.
- **`PropFirmEquityCalculator`** (`src/CTraderOpenApi/Client`) — cTrader-treue Eigenkapital-Mathe. Eigenkapital **nicht** geliefert von Open API, daher abgeleitet: `Eigenkapital = Balance + Σ(unrealisiert P&L)`, wo jeder Position P&L ist `Preis-Differenz × Einheiten × Quote→Einzahlungs-Rate + Tausch + Provision` (`Einheiten = Draht-Volumen / 100`; Long bewertet bei Gebot, Kurz bei Angebot). Balance von `ProtoOATrader`; Positionen (Eintritt-Preis, Tausch, Provision) von Abstimmung; Live Gebot/Angebot von Spot-Abos. Rein und isoliert — Währungs-Umwandlung Heiß-Punkt Einheit-Test auf seine Eigen.

## Benachrichtigungen

`PropFirmAlertNotifier` (`src/Infrastructure/PropFirm`) abonniert Pass/Bruch/Warnung Domänen-Ereignisse (registriert als `IDomainEventHandler<>`, versand nach erfolgreichem `SaveChanges`), benachrichtigt Benutzer durch strukturiert Warnung/Audit-Trail (`LogMessages`). Live UI spiegelt gleich Status-Änderung. Dies = Cross-Kontext Reaktion — mutiert nie Herausforderungs-Aggregate.

## API (`/api/prop-firm`, Feature `PropFirm`, Rolle User+)

| Methode | Route | Zweck |
|--------|-------|---------|
| GET | `/challenges` | Liste Benutzer-Herausforderungen (Art, Phase, Status, Live-Eigenkapital, Anspruch) |
| GET | `/challenges/{id}` | eine Herausforderung |
| GET | `/templates` | Industrie-Vorgaben für Create-Dialog |
| POST | `/challenges` | erstelle aus Vorlag **oder** vollständig Maßgeschneidert Regelwerk |
| POST | `/challenges/{id}/start` | nehme Verfolgung auf (Stopped → Active) |
| POST | `/challenges/{id}/stop` | Verfolgung stoppen (Active → Stopped, Anspruch freigeben) |
| POST | `/challenges/{id}/equity` | Eigenkapital-Snapshot aufzeichnen → re-evaluieren (manuell/kein-Live-Feed Pfad) |
| DELETE | `/challenges/{id}` | Soft-löschen (blockiert während Active) |

MCP: `Mcp/Tools/PropFirmTools.cs` verfügbar machen Liste/erstelle(aus Vorlag)/Eigenkapital-Aufzeichnung/Start/Stop, gated auf `PropFirm` Feature.

UI: `/prop-firm` (Nav *Prop Firm*, gated durch `PropFirm` Flag) listet Herausforderungen mit **Start/Stop/Löschen** Reihen-Aktionen (Start wenn Stopped, Stop wenn Active, Löschen deaktiviert während Active), erstelle sie durch `NewPropFirmChallengeDialog` (Vorlag-Wähler + vollständig Regel-Editor). Alle erstelle/bearbeiten via MudBlazor Dialog.

## Live-Eigenkapital-Feed — gelöst

Früher "nein Live-Konto P&L Feed" Lücke geschlossen: wenn `App:PropFirm:Enabled` gesetzt, Knoten Verfolgung Konto Live über Open API, Fütterin Eigenkapital automatisch. Ohne es (Standard), Domäne und **manuell-Eigenkapital** Pfad (`POST …/equity`) laufe unverändert — kein cTrader Anmeldedaten nötig für Build/Test/E2E.

## Tests

- **Unit** — `UnitTests/PropFirm/`: `PropFirmChallengeTests` (Phase Fortschritt, Min-Tage, Statisch/Trailing Drawdown, täglich Verlust, Terminal/Out-of-Order Guards); `PropFirmChallengeRulesTests` (Balance vs Eigenkapital täglich-Verlust Basis, Trailing-Schwelle-Dollar Trail+Sperren, Konsistenz Block/Erlauben, Zeit-Limit, Inaktivität, Max-Exposition, Wochenend, Nachrichten, Stop/Fortsetzen, Anspruch Grenze, Pass gibt Anspruch frei, Drawdown Warnung); `PropFirmValueObjectTests` (VO Bereiche + Regel-VO Mathe); `PropFirmEquityCalculatorTests` (Long/Kurz P&L, Tausch/Provision, Quote→Einzahlungs-Umwandlung, fehlende Preisgestaltung); `PropFirmTrackingHostTests` (Live-Eigenkapital Laufwerk Pass/Fehler gegen erweitert Fake-Sitzung); `PropFirmAlertNotifierTests`. Zeit explizit / `FakeTimeProvider` — kein Wanduhr-Lesevorgänge.
- **Integration** — `IntegrationTests/`: `PropFirmChallengePersistenceTests` (Rund-fahrt + Eigenkapital-Aufzeichnung + Soft-Löschen, angereichert-Regeln + Anspruch Rund-fahrt) und `PropFirmTrackingLeaseTests` (Anspruch, Umstritten Anspruch, Rückforderung nach Lapse über zwei Knoten-Identitäten) auf real Postgres.
- **E2E** — `E2ETests/PropFirmTests.cs`: erstelle + Eigenkapital-Aufzeichnung zu `Passed`; Stop→Start→Bruch Fluss; Vorlag Endpoint.
- **Stress / DST** — `StressTests/PropFirm/PropFirmChallengeDstTests.cs`: Besamt zufälliges Eigenkapital/Aktivität Ströme (Tag-Rollen, Spitzen, Abstürze, Duplikat + Out-of-Order Snapshots, Exposition/Wochenend/Nachrichten) über viele gemischte-Regel Herausforderungen, Behauptung Klebrig exakt-einmal Terminal Zustände, Peak-Grenzen-aktuell Invariante, begründet Misserfolge.

## Konfiguration (`App:PropFirm`)

`Enabled` (Standard aus), `ReconcileInterval`, `EquityPollInterval`, `LeaseTtl`, `DrawdownWarnThresholdPercent`, `NodeName`.
