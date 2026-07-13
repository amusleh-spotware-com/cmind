---
description: "Retail-Prop-Firms (FTMO-Style) verkaufen Evaluierungskonten: Der Trader muss ein Gewinnziel erreichen, während er innerhalb der Risikogrenzen bleibt (maximaler täglicher Verlust, maximaler Gesamt-/Trailing-Drawdown, Konsistenz, Zeitlimits) – bevor er finanziert wird…"
---

# Prop-Firm-Challenge-Simulation

Retail-Prop-Firms (FTMO-Style) verkaufen **Evaluierungskonten**: Der Trader muss ein Gewinnziel erreichen, während er innerhalb der Risikogrenzen bleibt (maximaler täglicher Verlust, maximaler Gesamt-/Trailing-Drawdown, Konsistenz, Zeitlimits), bevor er finanziert wird. cMind ermöglicht es dem Benutzer, **eine individuelle Challenge beliebiger Branchenform** zu erstellen, an ein `TradingAccount` zu binden und sie wie einen **Copy-Trading-Betrieb zu führen** – gestartet/gestoppt, auf einem Node gehostet, **live über die cTrader Open API verfolgt**. Das Aggregate wertet jede Regel deterministisch aus; bei Bestehen oder Verstoß wird die Challenge beendet, markiert und der Benutzer benachrichtigt.

## Domain (Bounded Context: PropFirm)

`PropFirmChallenge` = Aggregate Root (Modul `Core.PropFirm`), referenziert sein `TradingAccount` nur über die
Strong ID (kein domänenübergreifender FK). Eigene Regel-Auswertung, Phasen-/State-Machine, Node-Lease.

### Value Objects & Regelset

- **`Money`** (nicht-negativ), **`MoneyAmount`** (vorzeichenbehaftet), **`Percent`** (0–100], **`TradingDayRequirement`** (0–365).
- **`EquitySnapshot`** `(equity, balance)` — Lesedaten, die dem Aggregate zugeführt werden.
- **`ActivitySnapshot`** `(openPositions, openedInNewsWindow, holdingOverWeekend)` — Fakten außerhalb der Equity.
- **`DailyLossLimit`** `(percent, basis)` — Basis `Equity` (intraday, inkl. Floating P&L) oder `Balance`
  (nur realisiert).
- **`DrawdownLimit`** — `Static` (vom Startguthaben), `TrailingPercent` (vom Equity-Peak) oder
  `TrailingThresholdDollar` (verfolgt den Equity-Peak um einen festen Dollar-Betrag, dann **arretiert auf dem Startguthaben**, sobald die Equity den Schwellenwert erreicht — Futures-Stil).
- **`ConsistencyRule`** `(maxSingleDayShareOfProfit)` — blockiert Bestehen, solange ein Tag den Gesamtgewinn dominiert.
- **`ChallengeRules`** führt oben Genannte zusammen mit `MaxCalendarDays`, `MaxInactivityDays`, `MaxOpenPositions`,
  `AllowWeekendHolding`, `AllowNewsTrading`, `Kind`, `SingleStep`. Die Regelmathematik lebt auf den VOs
  (`DrawdownLimit.IsBreached`, `DailyLossLimit.IsBreached`, `ConsistencyRule.IsSatisfied`); das Aggregate
  orchestriert.

### Challenge-Arten & Templates

`ChallengeTemplates.For(kind)` baut ein gültiges Preset für `OnePhase`, `TwoPhase`, `ThreePhase`,
`InstantFunding` oder `Custom` (volle Kontrolle). Die UI füllt das Template vor; der Benutzer kann jedes Feld anpassen.

### Phasen & Status

- **Phasen:** `Evaluation → Verification → Funded` (Single-Step überspringt Verification).
- **Status:** `Active`, `Passed`, `Failed`, plus Lebenszyklus `Stopped` (Verfolgung pausiert) — `Create` startet
  Challenge als `Active`; `Stop()`/`Resume()` wechselt `Active↔Stopped`.
- **`BreachReason`:** `DailyLoss`, `MaxDrawdown`, `Consistency`, `TimeLimit`, `Inactivity`,
  `WeekendHolding`, `NewsTrading`, `MaxExposure`.

### Regelauswertung

- **`RecordEquity(EquitySnapshot, now)`** — rollt den Trading-Tag an Tagesgrenzen (erfasst den Gewinn des
  Vortages für die Konsistenzregel), aktualisiert Peak/Tagespeaks, dann **fail on first breach**
  (Tagesverlust → Drawdown → Zeitlimit → Inaktivität, in dieser Reihenfolge) oder rückt Phase vor, wenn
  Gewinnziel, Mindest-Handelstage und Konsistenzanforderungen alle erfüllt sind. Out-of-order Snapshots
  und Records auf einer beendeten Challenge werfen `DomainException`.
- **`RecordActivity(ActivitySnapshot, now)`** — wertet Verhaltensregeln aus (max. offene Positionen,
  Wochenendhalten, News-Trading), stempelt Aktivität für die Inaktivitätsregel.
- Das sanfte **`PropFirmDrawdownWarning`** wird einmal ausgelöst, wenn die Equity-Nutzung einen
  konfigurierbaren Schwellenwert überschreitet.

Domain Events: `PropFirmChallengeStarted`, `PropFirmChallengeStopped`, `PropFirmPhasePassed`,
`PropFirmChallengePassed`, `PropFirmChallengeBreached`, `PropFirmDrawdownWarning`.

## Live-Tracking (Execution) — Node-gehostet, selbstheilend

Das Tracking bildet den Copy-Trading-Hosting-Stack exakt ab; der Prop-Tracker ist ein **read-only**
Vetter der Copy-Engine.

- **`PropFirmTrackingSupervisor`** (`src/Nodes/PropFirm`) — `BackgroundService` auf jedem Node, gesteuert
  durch `App:PropFirm:Enabled`. Jeder Zyklus **beansprucht** aktive Challenges über einen selbstheilenden
  Lease (`AssignedNode` + `LeaseExpiresAt`; tote Node-Challenges werden zurückgefordert, sobald der Lease
  abläuft — gleiche atomare `ExecuteUpdate`-Beanspruchung wie beim Copy-Trading, sodass nie zwei Nodes
  gleichzeitig tracken), erneuert Leases, tauscht Token in-place aus, stoppt Hosts, deren Challenge
  `Active` verlassen hat.
- **`PropFirmTrackingHost`** (`src/Nodes/PropFirm`) — einer pro Challenge. Öffnet eine
  `IOpenApiTradingSession` für das Konto und berechnet auf `App:PropFirm:EquityPollInterval` die Live-Equity
  neu, fügt sie dem Aggregate zu. Tauscht das Access-Token in-place bei Rotation aus (kein
  Session-Drop). Beendet sich, wenn die Challenge nicht mehr `Active` ist.
- **`PropFirmEquityCalculator`** (`src/CTraderOpenApi/Client`) — cTrader-faithful Equity-Mathematik.
  Equity wird **nicht** von der Open API geliefert, also abgeleitet: `equity = balance + Σ(unrealized P&L)`,
  wobei jede Position P&L = `priceDifference × units × quote→deposit rate + swap + commission`
  (`units = wire volume / 100`; Long wird zu Bid, Short zu Ask neu bewertet). Balance von
  `ProtoOATrader`; Positionen (Entry-Preis, Swap, Commission) aus Abgleich; Live Bid/Ask aus
  Spot-Subscriptions. Rein und isoliert — der Währungsumrechnungs-Hotspot ist separat mit
  Unit-Tests abgedeckt.

## Alerts

`PropFirmAlertNotifier` (`src/Infrastructure/PropFirm`) abonniert Pass/Breach/Warning Domain Events
(registriert als `IDomainEventHandler<>`, dispatched nach erfolgreichem `SaveChanges`), benachrichtigt den
Benutzer durch strukturierten Alert/Audit-Trail (`LogMessages`). Die Live-UI reflektiert dieselbe
Statusänderung. Dies = domänenübergreifende Reaktion — mutiert niemals das Challenge-Aggregate.

## API (`/api/prop-firm`, Feature `PropFirm`, Rolle User+)

| Methode | Route | Zweck |
|---------|-------|-------|
| GET | `/challenges` | Liste der Challenges des Benutzers (Art, Phase, Status, Live-Equity, Lease) |
| GET | `/challenges/{id}` | Eine Challenge |
| GET | `/templates` | Branchen-Presets für den Erstellen-Dialog |
| POST | `/challenges` | Erstellen aus Template **oder** vollständig individuellem Regelset |
| POST | `/challenges/{id}/start` | Tracking fortsetzen (Stopped → Active) |
| POST | `/challenges/{id}/stop` | Tracking stoppen (Active → Stopped, Lease freigeben) |
| POST | `/challenges/{id}/equity` | Equity-Snapshot aufzeichnen → neu auswerten (manueller/No-Live-Feed-Pfad) |
| DELETE | `/challenges/{id}` | Soft-Delete (verhindert, solange Active) |

MCP: `Mcp/Tools/PropFirmTools.cs` bietet List/Create(von Template)/Record-Equity/Start/Stop, gesteuert
durch `PropFirm`-Feature.

UI: `/prop-firm` (Nav *Prop Firm*, gesteuert durch `PropFirm`-Flag) listet Challenges mit
**Start/Stop/Delete**-Zeilenaktionen (Start wenn Stopped, Stop wenn Active, Delete deaktiviert, solange
Active), erstellt sie über `NewPropFirmChallengeDialog` (Template-Picker + vollständiger
Regeleditor). Alle Erstellen/Bearbeiten über MudBlazor-Dialog.

## Live-Equity-Feed – gelöst

Die frühere Lücke „kein Live-Konto-P&L-Feed" ist geschlossen: wenn `App:PropFirm:Enabled` gesetzt ist,
tracken Nodes das Konto live über die Open API und liefern automatisch Equity. Ohne diese Einstellung
(default) laufen Domain und der **Manual-Equity**-Pfad (`POST …/equity`) unverändert weiter — keine
cTrader-Anmeldedaten für Build/Test/E2E nötig.

## Tests

- **Unit** — `UnitTests/PropFirm/`: `PropFirmChallengeTests` (Phasenvorschritt, Mindest-Tage,
  statischer/trailing Drawdown, Tagesverlust, Terminal/Out-of-Order Guards);
  `PropFirmChallengeRulesTests` (Balance vs Equity Tagesverlust-Basis, Trailing-Threshold-Dollar
  Trail+Lock, Konsistenz-Block/Erlaubnis, Zeitlimit, Inaktivität, Max-Exposure, Wochenende, News,
  Stop/Resume, Lease-Grenze, Pass gibt Lease frei, Drawdown-Warning);
  `PropFirmValueObjectTests` (VO-Bereiche + Regel-VO-Mathematik);
  `PropFirmEquityCalculatorTests` (Long/Short P&L, Swap/Commission, Quote→Deposit-Umrechnung,
  fehlende Preisbildung); `PropFirmTrackingHostTests` (Live-Equity steuert Pass/Fail gegen
  erweiterte Fake-Session); `PropFirmAlertNotifierTests`. Zeitangaben explizit /
  `FakeTimeProvider` — keine Wanduhr-Ablesungen.
- **Integration** — `IntegrationTests/`: `PropFirmChallengePersistenceTests` (Roundtrip + Record-Equity +
  Soft-Delete, angereicherte Regeln + Lease-Roundtrip) und `PropFirmTrackingLeaseTests` (Beanspruchung,
  beanspruchte Lease, Rückforderung nach Ablauf über zwei Node-Identitäten) auf echtem Postgres.
- **E2E** — `E2ETests/PropFirmTests.cs`: Erstellen + Record-Equity bis `Passed`; Stop→Start→Breach-Flow;
  Templates-Endpunkt.
- **Stress / DST** — `StressTests/PropFirm/PropFirmChallengeDstTests.cs`: deterministisch
  gesäte randomisierte Equity/Aktivitäts-Streams (Tagwechsel, Spikes, Abstürze, Duplikate + Out-of-Order
  Snapshots, Exposure/Wochenende/News) über viele gemischte Regel-Challenges, mit der Zusicherung
  exakt-einmaliger Terminalzustände, Peak-beschränkt-aktuell-Invariant, begründete Fehlschläge.

## Konfiguration (`App:PropFirm`)

`Enabled` (standardmäßig aus), `ReconcileInterval`, `EquityPollInterval`, `LeaseTtl`,
`DrawdownWarnThresholdPercent`, `NodeName`.
