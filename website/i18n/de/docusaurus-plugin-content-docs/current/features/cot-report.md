# Commitment of Traders (COT)

cMind bietet einen integrierten **Commitment of Traders**-Bericht — die wöchentliche CFTC-Aufschlüsselung, wer am US-Futures-Markt long und short positioniert ist (kommerzielle Hedger, große Spekulanten, Fonds), mit interaktiven Verlaufsdiagrammen, einem normalisierten **COT-Index**, einer authentifizierten REST-API für cBots und MCP-Tools für KI-Clients. Die Daten stammen direkt aus den **öffentlichen CFTC-Socrata-Datensätzen** — ohne API-Schlüssel, ohne Aggregator. Wie der Wirtschaftskalender ist es ein entkoppeltes Modul, das ohne Auswirkungen auf den Handelskern deaktiviert werden kann.

## Was es Ihnen bietet

- **Alle drei Berichtsfamilien, nur Futures und Futures + Optionen kombiniert:**
  - **Altbestand (Legacy)** — Nicht-kommerziell (große Spekulanten), Kommerziell (Hedger), Nicht meldepflichtig.
  - **Disaggregiert (Disaggregated)** — Hersteller/Händler, Swap-Dealer, verwaltete Gelder, andere Meldepflichtige.
  - **Händler in Finanz-Futures (TFF)** — Dealer, Asset Manager, gehebelte Fonds, andere Meldepflichtige.
- **Ein kuratierter Marktkatalog** — FX-Majors, Gold/Silber/Kupfer, Rohöl & Erdgas, Staatsanleihen, Aktienindizes, Kryptowährungen und Haupt-Getreidesorten/Soft Commodities — jeweils auf seinen stabilen CFTC-Kontraktcode abgebildet und, wo eindeutig, auf ein handelbares Symbol (z. B. Euro FX → `EURUSD`, Gold → `XAUUSD`).
- **Der COT-Index (0–100)** — wobei die aktuelle Netto-Position des Spekulanten in seinem historischen Bereich liegt (Standard ~3-Jahres-Lookback). Ablesungen nahe den Extremen kennzeichnen überfüllte Positionierung, die oft einer Umkehrung vorausgeht; der Bericht kennzeichnet ein **Langzeit-Extrem** (≥80) oder **Kurz-Extrem** (≤20).
- **Zeitpunktgenauigkeit.** Ein Wochenbericht wird am Dienstag gemessen, wird aber erst am Freitag darauf öffentlich; jeder Abruf respektiert diesen Veröffentlichungsmoment, daher sieht ein backtestetes Positionierungssignal nie einen Bericht, bevor er veröffentlicht wurde (kein Look-Ahead).

## Die Seite nutzen

Öffnen Sie **Commitment of Traders** aus der linken Navigation. Wählen Sie einen **Markt**, einen **Berichtstyp** (Legacy / Disaggregiert / Finanziell) und schalten Sie **Futures + Optionen** um, um zwischen nur Futures und der kombinierten Variante zu wechseln. Die Seite zeigt:

- **Netto-Positionierung über die Zeit** — ein interaktives Liniendiagramm der Netto-Position (lang − kurz) jeder Händlerkategorie in der Geschichtsfenster.
- **COT-Index** — ein Liniendiagramm des 0–100-Index mit der neuesten Ablesung und seinem Extremlabel.
- **Aktuelle Momentaufnahme** — eine Tabelle mit lang / kurz / netto / % des offenen Interesses pro Händlerkategorie, plus gesamtes offenes Interesse und Berichtsdatum.

## Wie die Daten fließen

Ein wöchentlicher Erfassungsarbeiter zieht die sechs CFTC-Datensätze für die verfolgten Märkte, führt den Marktkatalog zusammen und fügt jeden neuen Bericht **idempotent** an (erneutes Ausführen dupliziert niemals einen Snapshot). Der erste Lauf füllt mehrere Jahre Verlauf auf; spätere Läufe synchronisieren die letzten Wochen neu, um späte Revisionen zu erfassen. Alles funktioniert sofort ohne Schlüssel; ein optionales Socrata-App-Token erhöht lediglich die Rate Limit.

## Konfiguration

Alle Schlüssel befinden sich unter `App:Cot` (siehe [Feature-Schalter](./feature-toggles.md) und
[White-Label-Besitzer-Einstellungen](./white-label-owner-settings.md)):

| Schlüssel | Standard | Zweck |
|-----|---------|---------|
| `IngestionEnabled` | `true` | Ob der wöchentliche Erfassungsarbeiter läuft. |
| `PollInterval` | `6h` | Wie oft der Arbeiter die CFTC-Datensätze abfragt. |
| `BackfillYears` | `5` | Jahre Verlauf beim ersten Lauf gezogen. |
| `ReconcileLookbackWeeks` | `4` | Letzte Wochen bei jedem Zyklus neu synchronisiert, um Revisionen zu erfassen. |
| `SocrataAppToken` | — | Optionales Token, das das anonyme Rate Limit erhöht. |
| `CotIndexLookbackWeeks` | `156` | Wochenberichte, die als COT-Index-Bereich verwendet werden (~3 Jahre). |

## Gating

Die Sichtbarkeit ist ein zweistufiges Gating, identisch mit dem Wirtschaftskalender: das White-Label-Hartgating `App:Branding:EnableCot` (Build-Ebene) **und** der Runtime-Feature-Schalter `App:Features:Cot`. Wenn einer deaktiviert ist, verschwinden der Navigationslink, die Seite, REST-API und MCP-Tools alle (die API gibt `404` zurück). Da die Datenquelle schlüssellos ist, gibt es kein Datenquellen-Schlüssel-Gating — aktiviert bedeutet sichtbar.

## Für Entwickler

- Domain: `Core.Cot` — `CotMarket` und `CotReport` Aggregate, das `CotPositions` Value Object, der `CotIndexCalculator` Domain Service und die `ICotReports` / `ICotSource` Ports.
- Infrastruktur: `Infrastructure.Cot` — der `CftcSocrataSource` Anti-Corruption-Parser, das Rate Gating, der Append-Only-Write-Service, die Read-Side und der wöchentliche Erfassungsarbeiter (EF `cot` Schema).
- cBot- und KI-Zugang: [COT cBot API](./cot-cbot-api.md) (REST, JWT `market:read`) und MCP-Tools
  `CotMarkets`, `CotLatest`, `CotHistory`, `CotHealth`.
