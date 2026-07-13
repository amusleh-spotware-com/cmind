# Wirtschaftskalender

cMind versendet seinen **eigenen** Wirtschaftskalender — Release-Zeitplan, Actuals, Forecasts, Revisionen und ein datengesteuertes Impact-Modell — stammt aus **primären Behörden** (Zentralbanken und nationale statistische Agenturen), mit **Null-Abhängigkeit** von ForexFactory, FXStreet, Investing.com oder einem Aggregator. Es ist Point-in-Time-korrekt, behält ≥10 Jahre Geschichte und ist in Handel, die öffentliche API, MCP, cBots, KI, Warnungen und Backtests verdrahtet. Es ist ein entkoppeltes Modul: es kann mit Null-Effekt auf den Trading-Core deaktiviert werden.

> **Status.** Der Domain-Core (Impact-Modell, Land→Symbol-Mapping, News-Window-Policy, Point-in-Time-Revisions-Chains, Two-Tier-Gating) **und** Persistierung (das `calendar`-Postgres-Schema, die Append-Only-Lese/Schreib-Seite, der FRED-Connector und der Config-Gated-Ingestion-Worker) sind implementiert und getestet (Unit + Testcontainers-Integration). Die JWT-REST-API, die MCP-Tools und die UI landen in den nachfolgenden Rollout-Phasen, die unten beschrieben sind.

## Was es anders macht

Die wiederkehrenden Beschwerden gegen die führenden Kalender wurden unsere Design-Zwänge:

- **Keine stillen Impact-Rating-Änderungen.** Unser Impact-Rating ist **deterministisch, versioniert und überprüfbar**. Jede Änderung ist eine aufgezeichnete Revision mit einem Zeitstempel — nie eine stille Überschreibung. Ein Benutzer kann genau sehen, *warum* ein Event High ist.
- **Ein UTC-Anker pro Event.** Jedes Event ist an einen einzelnen UTC-Moment aus dem offiziellen Zeitplan der primären Quelle verankert; die eigene Zeitzone der Quelle wird gespeichert, und Pro-Benutzer-Rendering verwendet eine explizite IANA-Zeitzone mit DST, bearbeitet von der Zone-Datenbank — nie ein manueller ±1h-Toggle.
- **Vollständige Revisions-Chains, überall.** Der ursprüngliche Wert und jede Revision sind First-Class, identisch durch die API, MCP und cBot-Oberflächen verfügbar gemacht.
- **≥10 Jahre Geschichte, keine Mauer.** Uneingeschränktes Browsing-Bereich; keine 60-Tage-Kappe, kein Registrierungs-Tor.
- **Point-in-Time durch Konstruktion.** Jede Tatsache trägt `KnownAt` (wann *wir* es gelernt haben) und `EffectiveAt` (das Event-Moment). "Wie der Kalender zu Zeit T aussah" ist eine First-Class-Abfrage, sodass eine backtestete News-Regel genau wie Live funktioniert — kein Look-Ahead von überarbeiteten Werten in der Geschichte.

## Das Impact-Modell

Der Impact-Score ist eine pure, deterministische Funktion in `[0, 100]`, gebunden zu Low / Medium / High / Critical. Seine Eingaben sind nur Daten, die zum Scoring-Zeit bekannt sind (kein Future-Leak):

- **Series Prior** — ein Baseline-Gewicht pro Indikator-Klasse (eine Rate-Entscheidung überwieght CPI, was eine kleine Umfrage überwägt).
- **Realisierte-Volatilität-Fußabdruck** — der Median-absolute Rückkehr der primären betroffenen Symbole im Fenster nach dieser Series' *vorherigen* Freigaben: "diese Freigabe bewegt historisch Preis diesen Betrag."
- **Überraschungs-Empfindlichkeit** — wie stark die absolute Überraschung (ein z-Score) historisch mit der Post-Release-Bewegung korreliert hat.

Der Score vermischt diese mit festen Gewichten und stemelt einen `ImpactModelVersion`. Neuberechnung ist eine explizite, protokollierte Operation, die eine **neue Revision** erzeugt — nie eine Mutation — sodass der Score immer aus seinen Eingaben reproduzierbar ist.

## Land → Währung → Symbol-Mapping

Der am häufigsten zitierte Algo-Integrations-Schnittstellenschmerz wird einmal gelöst, als pure Funktion: Ein Land ordnet sich seiner Währung zu (jedes Euro-Bereichs-Mitglied fächert sich in EUR auf), und eine Währung ordnet sich den Watchlist-Symbolen zu, die es auf beiden Beinen zitieren. Also **EURUSD wird durch EU- und US-Events betroffen**; XAUUSD ist USD-exponiert; US500 ordnet sich USD zu. Dies treibt den News-Filter, die betroffenen-Symbole-Auflösung und die Blackout-Mathematik.

## News-Window-Policy

Eine `NewsWindowRule` ist `{ minImpact, beforeMinutes, afterMinutes, currencies?, series? }`. Eine einzige, gemeinsame, pure Implementierung beantwortet "Ist Moment T in einem Blackout für Symbol S?" — verwendet vom cBot-News-Filter, der Copy-Trade-Pause und dem KI-Risiko-Schutz, sodass sie nie abweichen können. Bei Unsicherheit wird die Blackout-Antwort auf den konfigurierten konservativen Wert (Fail-Closed standardmäßig) standardisiert, sodass eine Datenlücke nie stillschweigend das Grünlicht zum Handeln durch eine High-Impact-Release gibt.

## Point-in-Time & Revisionen

Actuals, Forecasts und Impact-Scores sind **Append-Only**. Jedes Event besitzt eine geordnete Chain von Revisionen, monoton in `KnownAt`:

- `Scheduled` — das Event war ursprünglich geplant (Prior Impact, kein Actual).
- `Released` — die erste gedruckte Actual kam an.
- `Revised` — ein später überarbeiteter Wert kam an.
- `Rescheduled` — die Quelle hat den Release-Moment bewegt (überprüfbar, alarmierbar).
- `Rescored` — der Impact-Score wurde unter einer neuen Modellversion neu berechnet.

Abfragen `asOf` eines Moments in der Vergangenheit geben exakt die damals bekannte Revision zurück — die Garantie, die Look-Ahead in backtestete News-Regeln tötet.

## Forecast / Consensus

Der Umfrage-Median von Ökonomen wird **nicht** frei von primären Quellen veröffentlicht — es ist der Aggregators' proprietärer Value-Add, und wir fabricieren es nicht. Das Event-Schema trägt einen nullable `Forecast`; eine Bereitstellung kann eine lizenzierte Consensus-Feed über den optionalen `IForecastProvider`-Port verdrahten (bringen Sie Ihren eigenen Schlüssel, standardmäßig aus). Vorherige Werte und Revisionen stammen immer aus der offiziellen Quelle.

## Datenquellen

Zwei entkoppelte Schichten, alle primär — niemals ein Aggregator:

- **Zeitplan / Timing:** FRED-Release-Kalender; nationale statistische Agenturen (BLS, BEA, Census, Eurostat, ONS, Destatis, INSEE, e-Stat, ABS, StatCan); Zentralbank-Treffen-Kalender (Fed, ECB, BoE, BoJ, RBA, BoC, SNB, RBNZ).
- **Tatsächliche Werte:** FRED (mit Vintage-Daten für Revisionen und Point-in-Time), plus BLS, BEA, Census, ECB SDW, Eurostat und OECD SDMX APIs.

Eine tote Quelle degradiert Abdeckung für **nur diese Quelle**; der Kalender bedient weiterhin alles andere und erscheint die Lücke als Freshness-Metrik.

## Rate Limiting & der Backup-Plan

Externe Provider veröffentlichen Rate Limits (FRED erlaubt ~120 Anfragen/Minute). Der Kalender ist so gebaut, dass er **ein Provider's Limit nie überschreitet**, und so, dass Drossel oder Abschnitt keine Reads degradiert:

- **Proaktive Drossel.** Jede Quelle des HTTP-Client geht durch ein gemeinsames, Thread-sicheres Rate-Tor, das Outbound-Anfragen zu einem konfigurierten Budget abteilt (`App:Calendar:FredRequestsPerMinute`, Standard 100 — absichtlich unter der Provider-Decke). Anfragen werden in die Warteschlange eingereiht und gepaced, nie gebursted.
- **Ehre `429 Retry-After`.** Wenn ein Provider je `429 Too Many Requests` zurückgibt, setzt das Tor die ganze Quelle um die Server-geforderte Abkühlung zurück (oder `App:Calendar:RateLimitBackoff`, Standard 60s), bevor der nächste Call — kein enger Retry-Loop.
- **Standard Resilience.** Jeder Quellen-Client erbt auch den App-weiten Resilience-Handler (Retry mit Backoff + Jitter, Leistungsschalter, Timeouts), sodass transiente Blips aufgenommen werden und eine persistierend ausfallende Quelle geparkt wird (ihre Abdeckung wird stale) ohne die anderen zu beeinflussen.
- **Der Backup-Plan — der haltbare Read-Through-Cache.** Reads werden **nie** durch den Aufruf eines Providers serviert. Einmal eine Bereich abgerufen, wird es Append-Only nach Postgres persistiert und von da an für immer dort serviert (siehe §"On-Demand-Load"). Also selbst wenn eine Quelle Rate-Limitiert oder Down ist, bedient der Kalender weiterhin aus zwischengespeichert, Point-in-Time-korrektis Daten; die fehlende Spanne bleibt einfach unabgedeckt und wird beim nächsten Ingestion-Zyklus erneut versucht. Blackout-Antworten können zusätzlich unter Unsicherheit zum konservativen Standard fehlschlagen, sodass eine Datenlücke nie das Grünlicht zum Handeln durch eine Release gibt.
- **Billige Umfrage.** Bedingter Abruf (ETag / If-Modified-Since / Source Vintage Cursors) und der "Span einmal abrufen, nie wieder" Cache halten das tatsächliche Request-Volumen weit unter einem Limit im normalen Betrieb — das Rate-Tor ist ein Sicherheitsnetz, nicht der gemeinsame Pfad.

## Aktivieren / Deaktivieren

Zwei unabhängige Ebenen, genau wie andere cMind-Features:

- **Tier 1 — Runtime Feature-Toggle** (`Feature.EconomicCalendar`) umgeschaltet vom Features-Admin UI; kein Redeploy, tritt live in Kraft.
- **Tier 2 — White-Label Hard-Gate** (`App:Branding:EnableEconomicCalendar`, Standard `true`). Ein Wiederverkäufer setzt es `false` um die Feature ganz zu entfernen; ein Operator kann sie dann nicht erneut aktivieren.

Der effektive Zustand ist `Branding.EnableEconomicCalendar && FeatureToggle.EconomicCalendar`. Bei Deaktivierung ist der Nav-Eintrag verborgen und `/economic-calendar`, `/api/calendar/**` und die MCP-Kalender-Tools geben einen sauberen Feature-Deaktiviert-`404` zurück — nie einen `500`. Persistierte Geschichte wird bei einem Runtime-Toggle-Off beibehalten, sodass Reaktivierung sofort ist.

## Rollout-Phasen

- **P0 — Domain-Core** *(implementiert)*: Aggregate, Value-Objekte, Ports, Impact-Modell, Land→Symbol-Mapping, News-Window-Policy, Two-Tier-Gating, vollständige Unit-Suite.
- **P1 — Persistierung + eine Quelle** *(implementiert)*: EF `calendar`-Schema (eigene Tabellen, Append-Only, Hot-Indizes), der Read-Through `IEconomicCalendar`-Reader mit Point-in-Time `asOf`, der idempotent Append-Only-Write-Service, der FRED-Connector hinter einem resilient typisierten Client, und der Config-Gated-Ingestion-Worker; Testcontainers-Integrations-Tests (Persistierung, PIT, Idempotenz, Blackout).
- **P2 — Öffentliche JWT-REST-API + Web-UI** *(implementiert)*: die versionierte, JWT-gesicherte `/api/calendar/v1`-API — Client-Ausstellung, Token-Austausch und die Core-Read-Endpunkte (Events, History, Series, Surprises, Next, Blackout, Affected-Symbols, Health) mit Scope-Durchsetzung und Two-Tier-Gating, Integrations-getestet. Plus die mobil-erste **`/economic-calendar`-Seite** — ein gated, vollständig lokalisiert (23 Sprachen) Agenda von bevorstehenden Releases als telefon-freundliche Karten mit Farb-gebundenen Impact-Chips und ein MudBlazor **Filter-Dialog** (Währungen + minimale Impact + ein **Von-Datum** Picker zum Springen zu **beliebigen** Vergangenheits-Daten über die volle Geschichte — keine 60-Tage-Kappe, keine Mauer); Nav-Eintrag, Rauch/Mobil/A11Y/E2E getestet. Eine **pro-Indikator-Serie-Geschichtsseite** (`/economic-calendar/series/{code}`, verlinkt von jedem Event) listet eine Serien-volle Print-Historie auf. Die Überraschungs-Charts + Unendlich-Scroll-Browser folgen.
- **P3 — mehr Quellen & Aufwärmung** *(gestartet)*: ein **Core-Series-Katalog** (CPI, Core CPI, NFP, Arbeitslosigkeit, BIP, PCE, Fed-Gelder, Einzelhandelsumsätze → ihre FRED-IDs) wird beim Start automatisch gesät, und ein einmaliges, idempotent, Jahr-geschnittenes **proaktives Backfill** zieht ihre ≥10-Jahre-Historie, sodass der häufige Fall warm ohne Warten auf einen Benutzer-Fehlversuch ist. **Ingestion ist standardmäßig aktiviert** (`App:Calendar:IngestionEnabled`, Standard `true`): die **Zentralbank-Zeitplan-Quelle** benötigt **keinen API-Schlüssel**, sodass der FOMC / ECB / BoE-Entscheidungs-Kalender standardmäßig bevölkert wird — das Backfill besät diese Treffens-Daten über **sowohl aktuelle Geschichte als auch Forward-Horizont**, sodass Browsing *letzter Monat* (oder beliebige vergangene Fenster) die Treffen zeigt, selbst bevor ein FRED/BLS-Schlüssel konfiguriert ist; die Wert-Series füllen sich einmal ihre Schlüssel gesetzt werden. Die Worker ehren das Kalender-Two-Tier-Tor — eine White-Label-Bereitstellung oder der Besitzer, der die Wirtschaftskalender-Feature deaktiviert, stoppt Ingestion, und `App:Calendar:IngestionEnabled=false` dreht es explizit aus. **Pro-Quellen-Freshness** ist jetzt wirklich auch: der Worker notiert jede Quellen-letzte erfolgreiche Umfrage, Konsecutive-Fehler-Zähler und ein Trip-Circuit-Flag (persistiert in App-Einstellungen, Cross-Prozess), und das `/health`-Endpunkt + `calendar_health`-MCP-Tool meldet ein wahrheitsgemäßes `stale`-Urteil pro Quelle. **BLS** (eine 2. Wert-Quelle) und die **Zentralbank-Zeitplan-Quelle** (FOMC / ECB / BoE-Entscheidungs-Daten, Backfilled über Geschichte und sync Forward in ein Horizont-Fenster vom Worker) sind drin. Noch zu kommen: BEA/Census/ECB-SDW/Eurostat/OECD-Wert-Quellen und der Versöhnungs-Pass.
- **P4 — Tiefe Integration**: **MCP-Tools** *(implementiert — vollständige Read-API-Parität: `calendar_events`, `calendar_event`, `calendar_history`, `calendar_series`, `calendar_surprises`, `calendar_next`, `calendar_blackout`, `calendar_affected_symbols`, `calendar_health`, gated auf die Feature)* und die **alerts `EconomicEvent`-Trigger** *(implementiert — eine `AlertRule`, die N Minuten vor einer bevorstehenden Release bei/über einem gewählten Impact feuert, optional begrenzt auf Währungen; evaluiert vom existierenden Alert-Worker ohne KI, dedupliciert pro Release; erstellt via `POST /api/alerts/rules/economic-event`)*. Das Prop-Guard-News-Blackout-Tor **und die Copy-Trade-Blackout-Pause** sind drin (§5.1 — ein optionales `App:Copy:NewsPauseEnabled`, Standard aus: ein Quellen-offen, dessen Symbol in einem Critical-Impact-Blackout sitzt, wird übersprungen, Byte-identisch heißer Pfad wenn aus). Das **Backtest-Event-Overlay** ist drin — `GET /api/calendar/v1/for-symbol` und das `calendar_events_for_symbol`-MCP-Tool geben die Point-in-Time-korrektis Events zurück, die ein Symbol in einem Fenster beeinflussen, und die **Instanz/Backtest-Berichtseite** rendert die High-Impact-Releases, die in das Backtest-Fenster unter der Eigenkapital-Kurve fielen (sodass ein Autor sieht, welche Trades auf NFP landeten), gated und lokalisiert. Der ganze Plan ist jetzt implementiert.
- **P5 — Extras**: Überraschungs-Analytik, iCal/CSV-Export, Stichwortsuche, Pluggable Consensus.

Siehe die [cBot & REST-API-Referenz](calendar-cbot-api.md) für die Integrationsoberfläche.
