---
title: Dashboard
description: Das cMind-Dashboard – ein Live-, Mobile-First-Kommandozentrum für Ihre cBot-Runs, Backtests, Ressourcen und Node-Cluster.
---

# Dashboard

Die erste Seite nach dem Anmelden – und tatsächlich die Seite, die Sie den ganzen Tag geöffnet lassen.
Die Landing Page (`/`, `Components/Pages/Index.razor`) ist ein **Live-, Mobile-First-Kommandozentrum** für
die Aktivitäten des eingeloggten Benutzers über cBot-Runs, Backtests, Ressourcen und (für Admins) den
Node-Cluster. Sie aktualisiert sich selbst, sieht auf dem Handy großartig aus und lässt Sie nie F5
drücken.

## Was es zeigt

Von oben nach unten, priorisiert für ein Handy (jeder Block ist ein Vollbreiten-Stack-Item auf Mobile,
ein responsives Grid auf Tablet/Desktop):

1. **Header** — Titel, ein Live-Indikator (ein echter pulsierender Punkt; statisch unter
   `prefers-reduced-motion`), die letzte Aktualisierungszeit und ein **Perioden-Toggle**
   (`1H · 24H · 7D · 30D`), das die KPIs und den Chart steuert.
2. **Hero-KPIs** — vier Blickfang-Karten, jeweils eine große Zahl + eine Inline-SVG-Sparkline und
   (wo sinnvoll) ein **Delta gegenüber der Vorperiode**:
   - **Aktiv jetzt** — Runs + Backtests, die gerade starten/laufen.
   - **Erfolgsrate** — abgeschlossen ÷ (abgeschlossen + fehlgeschlagen) über den Zeitraum; Delta in
     Prozentpunkten.
   - **Abgeschlossen** — beendete Runs/Backtests in dieser Periode; Delta gegenüber Vorperiode.
   - **Fehlgeschlagen** — Fehler in dieser Periode; Delta (weniger ist besser, also zeigt ein
     Rückgang Grün).
3. **Aktivitäts-Chart** — eine ApexCharts-Flächen-Zeitachse mit gestarteten / abgeschlossenen /
   fehlgeschlagen pro Zeitbucket.
4. **Instance-Status-Ring** — ein Donut mit Running / Backtests / Pending / Completed / Failed,
   Gesamtzahl in der Mitte.
5. **Backtests** — eine Drei-Kachel-Übersicht (Running / Completed / Failed), Klick durch zu
   `/backtest`.
6. **Copy-Trading** — Ihre Copy-Trading-Profile mit Live-Status-Dot, Zielcount und einem **Live**-Badge
   auf laufenden Profilen; Klick durch zu `/copy-trading`.
7. **KI-Agenten** — Ihre persona-getriebenen Trading-Agenten mit Run-Status (Archetyp · Status) und
   Zeitpunkt der letzten Aktion; Klick durch zu `/agent-studio`.
8. **Live-Aktivitäts-Feed** — die 20 neuesten Ereignisse (neueste zuerst) mit farbcodiertem Status-Dot
   und relativer Zeitangabe.
9. **Cluster-Gesundheit** (nur Admins) — Active-vs.-Total-Nodes und ein Kapazitäts-Nutzungs-Gauge.
10. **Ressourcen-Kacheln** — cBots, Trading-Konten, cTrader-IDs, MCP-Schlüssel (Klick durch zu den
    jeweiligen Seiten).

## Ihr Dashboard anpassen

Jeder oben genannte Block ist ein **steuerbares Widget**. Klicken Sie auf **Anpassen** (oben rechts im
Header), um einen Dialog zu öffnen, in dem Sie **jedes Widget ein-/ausblenden** und mit Hoch-/Runter-Pfeilen
**neu anordnen** können. **Auf Standard zurücksetzen** stellt die Katalogreihenfolge wieder her. Ihre
Wahl wird **serverseitig pro Benutzer persistiert**, sodass sie Ihnen über Browser und Geräte hinweg folgt
— nicht nur in diesem Tab.

- Feature-gesteuerte und Admin-only-Widgets (Copy-Trading, KI-Agenten, Cluster-Gesundheit) erscheinen nur
  dann im Dialog, wenn Ihr Deployment/Ihre Rolle sie nutzen kann.
- Der Widget-Katalog ist eine einzige Quelle der Wahrheit in `Core/Dashboard/DashboardWidgets.cs`; die
  Darstellung (Label + Icon + Verfügbarkeit) lebt in
  `Components/Dashboard/DashboardWidgetMeta.cs`.

## Wie es live bleibt

Die Seite polled `GET /api/dashboard/overview?period=<1h|24h|7d|30d>` alle 10 Sekunden und rendert die
Widgets in-place neu — kein manuelles Neuladen. Ein vorübergehender Fetch-Fehler wird verschluckt und im
nächsten Tick erneut versucht; die Schleife stoppt sauber beim Dispose. Der erste Load zeigt ein Skeleton;
ein anhaltender Fehler zeigt eine Fehlerkarte mit **Wiederholen**; ein Benutzer ohne Daten sieht
nullsifizierte KPIs und leeren State-Text.

## Backend

- `Endpoints/DashboardEndpoints.cs` mappt `/overview` (und behält die älteren skalaren `/stats` bei).
  Es ist pro Benutzer und Admin-gesteuert über `ICurrentUser`; die Uhr kommt von `TimeProvider`. Es
  mappt auch `GET/PUT /api/dashboard/layout` — das Widget-Layout des Benutzers, geladen beim
  Seitenstart und gespeichert aus dem Anpassen-Dialog.
- **Layout-Persistenz** ist das `UserDashboard`-Aggregate (`Core/Dashboard/UserDashboard.cs`): ein Board
  pro Benutzer (eindeutig auf `UserId`), das eine geordnete Liste von Widget-Einstellungen (sichtbar +
  Reihenfolge) als `jsonb`-Spalte speichert. Die geordnete Liste wird nur über `Apply` / `Reset`
  mutiert, die jeden Schlüssel gegen den `DashboardWidgets`-Katalog validieren und die Sammlung
  vollständig und dedupliziert halten. Unbekannte Schlüssel werden mit einer `DomainException` → `400`
  abgelehnt.
- `Endpoints/DashboardQuery.cs` baut das zusammengesetzte `DashboardOverview`-Read-Modell: eine
  Allzeit-Status-Snapshot (gruppierte Counts), ein windowed Set von Instanzen, einmalig materialisiert,
  und Ressourcen/Node-Counts. Instance-Status und Terminal-Timestamps leben auf TPH-Subtypen (nicht
  Spalten), sodass Zeilen im Speicher über die gemeinsamen
  `InstanceEndpoints.GetStartedAt/GetStoppedAt`-Helfer gelesen werden. Event-Zeit =
  `stopped ?? started ?? created`.
- `Endpoints/DashboardModels.cs` hält die DTOs, den Period→(Window, Bucket-Count)-Plan und
  `DashboardMath` — rein, deterministisch, Bucketing + KPI/Delta-Mathematik (kein I/O, `now` wird
  übergeben).

KPI-Deltas vergleichen das aktuelle Fenster mit dem unmittelbar vorhergehenden (die Abfrage holt ein
doppeltes Fenster dafür). Es gibt **keinen Live-Konto-P&L-Feed** — die Plattform hat nur Equity für
Backtests und Prop-Firm-Tracking — sodass das Dashboard bewusst *operational* ist (Aktivität, Durchsatz,
Erfolgsrate), kein Brokerage-Bilanz-Ticker.

## Design & Tokens

Alle Farbe kommt aus Design-Tokens (`var(--app-success|-warning|-error|-info|-primary|-text*)`), sodass
eine White-Label-Palette kostenlos durchfließt — einschließlich des Charts, dessen Serienfarben zur
Laufzeit aus den aufgelösten Tokens gelesen werden via `window.appReadTokens` (SVG kann CSS-Variablen
nicht direkt konsumieren). Keine hardcodierte Hex-Farbe irgendwo im Dashboard. Siehe
[../ui-guidelines.md](../ui-guidelines.md).

## Der „Powered by cMind"-Link

Das Dashboard zeigt einen kleinen, geschmackvollen **„Powered by cMind"**-Link, der auf diese
Dokumentationsseite verweist. Er wird **standardmäßig angezeigt** — wir sind stolz auf das Projekt und
er hilft anderen Händlern, es zu finden — aber es ist ganz Ihre Entscheidung. Wiederverkäufer, die eine
vollständig white-labelte Instanz betreiben, setzen `App:Branding:ShowSiteLink` auf `false` und er
verschwindet. Siehe [White-Label-Branding](./white-label.md#powered-by-link).

## Tests

- **Unit-Style** (`tests/IntegrationTests/DashboardMathTests.cs`) — Bucketing, Erfolgsrate,
  Vorperioden-Deltas, Perioden-Parsing, leer/grenzwertig (Event bei `now`, Division-by-Zero-Guard).
- **Unit** (`tests/UnitTests/Dashboard/UserDashboardTests.cs`) — das `UserDashboard`-Aggregate:
  Standard-Seed, Apply-Reihenfolge/Sichtbarkeit, Append-Omited, Duplikat-Collapse,
  unbekannter-Schlüssel-Ablehnung, Reset.
- **Integration** (`tests/IntegrationTests/DashboardQueryTests.cs`, `DashboardLayoutTests.cs`) —
  das Read-Modell gegen echtes Postgres (Status/KPIs/Aktivität/Ressourcen, Admin-Node-Gesundheit,
  leerer-Benutzer-Pfad), die neuen Backtests/Copy-Profiles/Agenten-Abschnitte, und ein Layout
  **Roundtrip** (Custom-Layout speichern → neu laden → Reihenfolge + Sichtbarkeit persistiert).
- **E2E** (`tests/E2ETests/DashboardTests.cs`, `DashboardCustomizeTests.cs`) — Desktop + Mobile:
  KPI-Karten, Chart, Ring und Feed rendern; der Perioden-Toggle wechselt die aktive Periode und lädt neu;
  eine KPI bohrt durch zu `/run`; **ein Widget ausblenden persistiert über einen Reload**,
  **Zurücksetzen** stellt es wieder her, und der Anpassen-Dialog funktioniert auf einem Handy ohne
  horizontalen Overflow. `/` ist auch in `PageSmokeTests`, `MobileLayoutTests` (Shell + kein Overflow)
  und `MobileJourneyTests`.
