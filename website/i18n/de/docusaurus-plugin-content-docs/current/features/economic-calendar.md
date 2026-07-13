# Economic Calendar

cMind bringt seinen **eigenen** Economic Calendar heraus – Veröffentlichungsplan, Ist-Werte, Prognosen,
Revisionen und ein datengesteuertes Impact-Modell – aus **primären Quellen** (Zentralbanken und
nationale Statistikbehörden), mit **Null-Abhängigkeit** von ForexFactory, FXStreet, Investing.com oder
einem Aggregator. Er ist zeitpunktgenau korrekt, bewahrt ≥10 Jahre Geschichte und ist mit Trading,
der öffentlichen API, MCP, cBots, AI, Alerts und Backtests verdrahtet. Er ist ein entkoppeltes Modul:
Er kann mit Null-Effekt auf den Trading-Kern deaktiviert werden.

> **Status.** Die Domain-Kerne (Impact-Modell, Land→Symbol-Mapping, News-Window-Policy,
> Point-in-Time-Revisionsketten, Two-Tier-Gating) **und** Persistence (das `calendar` Postgres-Schema,
> die Append-Only-Read-/Write-Seite, der FRED-Connector und der Config-gesteuerte Ingestion-Worker)
> sind implementiert und getestet (Unit + Testcontainers-Integration). Die JWT-REST-API, die MCP-Tools
> und die UI landen in den nachfolgenden Rollout-Phasen, die unten beschrieben werden.

## Was ihn anders macht

Die wiederkehrenden Beschwerden gegen die führenden Kalender wurden zu unseren Design-Constraints:

- **Keine stillen Impact-Rating-Änderungen.** Unser Impact-Rating ist **deterministisch, versioniert
  und prüfbar**. Jede Änderung ist eine aufgezeichnete Revision mit Zeitstempel – niemals eine stille
  Überschreibung. Ein Benutzer kann genau sehen, *warum* ein Ereignis Hoch ist.
- **Ein UTC-Anker pro Ereignis.** Jedes Ereignis ist auf einen einzigen UTC-Instant von der primären
  Quelle ihres offiziellen Plans verankert; die eigene Zeitzone der Quelle wird gespeichert, und das
  Rendering pro Benutzer verwendet eine explizite IANA-Zeitzone mit DST, gehandhabt durch die
  Zonendatenbank – niemals ein manuelles ±1h-Toggle.
- **Vollständige Revisionsketten, überall.** Der Originalwert und jede Revision sind erstklassig und
  werden über die API, MCP und cBot-Flächen identisch exponiert.
- **≥10 Jahre Geschichte, keine Mauer.** Uneingeschränkter Browsing-Bereich; kein 60-Tage-Cap, keine
  Registrierungspflicht.
- **Point-in-Time von Bauweise her.** Jede Tatsache trägt `KnownAt` (wann *wir* davon erfuhren) und
  `EffectiveAt` (der Ereignis-Instant). „Wie der Kalender zum Zeitpunkt T aussah" ist eine
  erstklassige Abfrage, sodass ein backgetesteter News-Regel sich genau wie die Live-Version verhält –
  kein Look-Ahead durch die Verwendung revidierter Werte in der Geschichte.

## Das Impact-Modell

Der Impact-Score ist eine reine, deterministische Funktion in `[0, 100]`, eingeteilt in Low / Medium /
High / Critical. Seine Eingaben sind nur Daten, die zum Bewertungszeitpunkt bekannt sind (kein
Future-Leak):

- **Serienpriorität** – ein Basisgewicht pro Indikator-Klasse (eine Zinsentscheidung wiegt schwerer als
  CPI, was wiederum schwerer wiegt als eine kleine Umfrage).
- **Realisierte-Volatilität-Fußabdruck** – der Median der absoluten Rendite der primär betroffenen
  Symbole im Fenster nach *vergangenen* Veröffentlichungen dieser Serie: „diese Veröffentlichung hat
  historisch den Preis so stark bewegt."
- **Überraschungs-Sensitivität** – wie stark die absolute Überraschung (ein z-Score) historisch mit der
  Kursbewegung nach der Veröffentlichung korreliert hat.

Der Score blendet diese mit festen Gewichten und stempelt eine `ImpactModelVersion`. Eine Neuberechnung
ist eine explizite, geloggte Operation, die eine **neue Revision** erzeugt – niemals eine Mutation –
sodass der Score immer aus seinen Eingaben reproduzierbar ist.

## Land → Währung → Symbol-Mapping

Die am häufigsten genannte Algo-Integrations-Papier-Katze ist einmal gelöst, als reine Funktion: Ein
Land mappt zu seiner Währung (jedes Eurogebietsmitglied schlägt auf EUR durch), und eine Währung mappt
zu den Watchlist-Symbolen, die sie auf einem Bein notieren. Also **EURUSD wird sowohl von EU- als auch
US-Ereignissen beeinflusst**; XAUUSD ist USD-exponiert; US500 mappt zu USD. Dies steuert den
News-Filter, die Auflösung betroffener Symbole und die Blackout-Mathematik.

## News-Window-Policy

Eine `NewsWindowRule` ist `{ minImpact, beforeMinutes, afterMinutes, currencies?, series? }`. Eine
einzige, gemeinsame, reine Implementierung beantwortet „ist Instant T in einem Blackout für Symbol S?" –
verwendet vom cBot-News-Filter, der Copy-Trade-Pause und dem KI-Risikoguard, sodass sie nie
divergieren können. Bei Unsicherheit defaultet die Blackout-Antwort auf den konfigurierten konservativen
Wert (Fail-Closed standardmäßig), sodass eine Datenlücke niemals stillos ein Trading durch eine
High-Impact-Veröffentlichung durchlässt.

## Point-in-Time & Revisionen

Ist-Werte, Prognosen und Impact-Scores sind **Append-Only**. Jedes Ereignis besitzt eine geordnete Kette
von Revisionen, monoton in `KnownAt`:

- `Scheduled` – das Ereignis wurde erstmals geplant (Prior-Impact, kein Ist).
- `Released` – der erste gedruckte Ist-Wert ist eingetroffen.
- `Revised` – ein später revidierter Wert ist eingetroffen.
- `Rescheduled` – die Quelle hat den Veröffentlichungsinstant verschoben (prüfbar, alertbar).
- `Rescored` – der Impact-Score wurde unter einer neuen Modellversion neu berechnet.

Die Abfrage `as of` eines vergangenen Instants gibt genau die Revision zurück, die damals bekannt war –
die Garantie, die Look-Ahead in backgetesteten News-Regeln tötet.

## Prognose / Konsens

Der Umfragemedian der Ökonomen wird **nicht** frei von Primärquellen veröffentlicht – er ist der
proprietäre Mehrwert der Aggregatoren, und wir fabrizieren ihn nicht. Das Ereignisschema führt ein
nullable `Forecast`; ein Deployment kann einen lizenzierten Konsens-Feed über den optionalen
`IForecastProvider`-Port verdrahten (bring-your-own-key, standardmäßig aus). Vorherige Werte und
Revisionen kommen immer von der offiziellen Quelle.

## Datenquellen

Zwei entkoppelte Schichten, alle primär – niemals ein Aggregator:

- **Plan / Timing:** FRED-Veröffentlichungskalender; nationale Statistikbehörden (BLS, BEA, Census,
  Eurostat, ONS, Destatis, INSEE, e-Stat, ABS, StatCan); Zentralbank-Kalender (Fed, ECB, BoE, BoJ,
  RBA, BoC, SNB, RBNZ).
- **Ist-Werte:** FRED (mit Vintage-Dates für Revisionen und Point-in-Time), plus BLS, BEA, Census,
  ECB SDW, Eurostat und OECD SDMX APIs.

Eine tote Quelle degradiert die Abdeckung **nur für diese Quelle**; der Kalender bedient weiterhin alles
andere und Oberflächen die Lücke als Frische-Metrik.

## Rate Limiting & der Backup-Plan

Externe Anbieter veröffentlichen Rate-Limits (FRED erlaubt ~120 Anfragen/Minute). Der Kalender ist so
gebaut, dass er **niemals das Limit eines Anbieters überschreitet** und dass ein Gedrosselt- oder
Abgeschnitten-Werden niemals Reads degradiert:

- **Proaktives Throttling.** Jedes Source-HTTP-Click geht durch ein gemeinsames, threadsicheres
  Rate-Gate, das ausgehende Anfragen auf ein konfiguriertes Budget verteilt
  (`App:Calendar:FredRequestsPerMinute`, Standard 100 – bewusst unter der Anbieter-Obergrenze).
  Anfragen werden in eine Warteschlange gestellt und getaktet, niemals geburstet.
- **`429 Retry-After` ehren.** Wenn ein Anbieter jemals `429 Too Many Requests` zurückgibt, sichert das
  Gate die gesamte Quelle um die vom Server angeforderte Cool-down-Zeit ab (oder
  `App:Calendar:RateLimitBackoff`, Standard 60s), bevor der nächste Aufruf erfolgt – keine enge
  Retry-Schleife.
- **Standard-Resilienz.** Jeder Source-Client erbt auch den App-weiten Resilienz-Handler (Retry mit
  Backoff + Jitter, Circuit Breaker, Timeouts), sodass vorübergehende Ausrutscher absorbiert werden und
  eine persistent scheiternde Quelle geparkt wird (ihre Abdeckung wird alt), ohne die anderen zu
  betreffen.
- **Der Backup-Plan – der dauerhafte Read-Through-Cache.** Reads werden **niemals** bedient, indem ein
  Anbieter aufgerufen wird. Sobald ein Bereich abgerufen wird, wird er Append-Only zu Postgres
  persistiert und von dort für immer bedient (siehe §„On-Demand-Last"). Also, selbst wenn eine Quelle
  rate-limited oder ausgefallen ist, beantwortet der Kalender weiterhin aus gecachten,
  zeitpunktgenauen Daten; die fehlende Spanne bleibt ungedeckt und wird im nächsten
  Ingestion-Zyklus erneut versucht. Blackout-Antworten versagen zusätzlich zum konservativen Default
  unter Unsicherheit, sodass eine Datenlücke niemals ein Trading durch eine Veröffentlichung durchlässt.
- **Billiges Pollen.** Conditional Fetch (ETag / If-Modified-Since / Source-Vintage-Cursors) und das
  „hole einen Span einmal, nie wieder"-Cache halten das tatsächliche Anfragevolumen weit unter jedem
  Limit im Normalbetrieb – das Rate-Gate ist ein Sicherheitsnetz, nicht der häufigste Pfad.

## Aktivieren / Deaktivieren

Zwei unabhängige Tiers, genau wie andere cMind-Features:

- **Tier 1 – Runtime-Feature-Toggle** (`Feature.EconomicCalendar`) umgeschaltet aus der Features-Admin-UI;
  kein Redeploy, wirkt live.
- **Tier 2 – White-Label-Hard-Gate** (`App:Branding:EnableEconomicCalendar`, Standard `true`). Ein
  Wiederverkäufer setzt es auf `false`, um das Feature vollständig zu entfernen; ein Operator kann es
  dann nicht wieder aktivieren.

Effektiver Status ist `Branding.EnableEconomicCalendar && FeatureToggle.EconomicCalendar`. Wenn
deaktiviert, wird der Nav-Eintrag ausgeblendet und `/economic-calendar`, `/api/calendar/**` und die
MCP-Kalender-Tools geben einen sauberen Feature-Disabled-`404` zurück – niemals einen `500`.
Persistierte Geschichte wird bei einem Runtime-Toggle-Off beibehalten, sodass ein erneutes Aktivieren
sofort funktioniert.

## Rollout-Phasen

- **P0 – Domain-Kern** *(implementiert)*: Aggregates, Value Objects, Ports, Impact-Modell,
  Land→Symbol-Mapping, News-Window-Policy, Two-Tier-Gating, vollständige Unit-Suite.
- **P1 – Persistence + eine Quelle** *(implementiert)*: EF `calendar`-Schema (eigene Tabellen,
  Append-Only, Hot-Indexes), der Read-Through-`IEconomicCalendar`-Reader mit Point-in-Time-`asOf`,
  der idempotente Append-Only-Write-Service, der FRED-Connector hinter einem resilienten typisierten
  Client, und der Config-gesteuerte Ingestion-Worker; Testcontainers-Integrationstests (Persistence,
  PIT, Idempotenz, Blackout).
- **P2 – Öffentliche JWT-REST-API + Web-UI** *(implementiert)*: die versionierte,
  JWT-gesicherte `/api/calendar/v1`-API – Client-Ausstellung, Token-Austausch und die Core-Read-Endpunkte
  (Ereignisse, Geschichte, Serien, Überraschungen, Next, Blackout, Betroffene-Symbole, Health) mit
  Scope-Durchsetzung und Two-Tier-Gating, Integration-getestet. Plus die Mobile-First
  **`/economic-calendar`-Seite** – eine gesteuerte, vollständig lokalisierte (23 Sprachen) Agenda
  kommender Veröffentlichungen als handyfreundliche Karten mit farbbandierten Impact-Chips und einem
  MudBlazor **Filter-Dialog** (Währungen + Minimum-Impact + ein **Von-Datum**-Picker, um zu **jedem**
  vergangenen Datum in der gesamten Geschichte zu springen – kein 60-Tage-Cap, keine Mauer); Nav-Eintrag,
  Smoke/Mobile/A11y/E2E-getestet. Eine **pro-Serie Serien-Geschichte-Seite**
  (`/economic-calendar/series/{code}`, verlinkt von jedem Ereignis) listet die vollständige
  Druckgeschichte einer Serie. Die Überraschungs-Charts + Infinite-Scroll-Browser folgen.
- **P3 – Mehr Quellen & Warm-up** *(gestartet)*: ein **Kern-Serien-Katalog** (CPI, Core CPI, NFP,
  Arbeitslosigkeit, BIP, PCE, Fed Funds, Retail Sales → ihre FRED-IDs) wird beim Startup automatisch
  gesät, und ein einmaliger, idempotenter, jahr-chunkierter **proaktiver Backfill** zieht ihre
  ≥10-Jahres-Geschichte, sodass der Common Case warm ist, ohne dass ein Benutzer etwas verpasst.
  **Ingestion ist standardmäßig an** (`App:Calendar:IngestionEnabled`, Standard `true`): die
  **Zentralbank-Planquelle** benötigt **keinen API-Schlüssel**, sodass sich der FOMC / ECB / BoE
  Entscheidungskalender out-of-the-box füllt – der Backfill sät diese
  Sitzungstermine über **sowohl die jüngste Geschichte als auch den Forward-Horizont**, sodass das
  Durchsuchen *letzten Monats* (oder jedes vergangene Fenster) die Sitzungen zeigt, noch bevor ein
  FRED/BLS-Schlüssel konfiguriert ist; die Wert-Serien füllen sich, sobald ihre Schlüssel gesetzt
  sind. Die Workers ehren das Two-Tier-Gate des Kalenders – ein White-Label-Deployment oder der
  Eigentümer, der das Economic-Calendar-Feature deaktiviert, stoppt die Ingestion, und
  `App:Calendar:IngestionEnabled=false` schaltet es explizit aus. **Pro-Quelle-Frische** ist jetzt
  auch real: Der Worker zeichnet die letzte erfolgreiche Abfrage jeder Quelle auf, die Anzahl
  aufeinanderfolgender Fehler und eine ausgelöste Circuit-Breaker-Flagge (persistiert in
  App-Einstellungen, prozessübergreifend), und der `/health`-Endpunkt + das `calendar_health` MCP-Tool
  melden ein wahres `stale`-Urteil pro Quelle. **BLS** (eine 2. Wertquelle) und die
  **Zentralbank-Planquelle** (FOMC / ECB / BoE Entscheidungstermine, über Geschichte zurückgefüllt und
  in ein Horizont-Fenster nach vorne synchronisiert vom Worker) sind drin. Noch ausstehend:
  BEA/Census/ECB-SDW/Eurostat/OECD-Wertquellen und der Abgleich-Pass.
- **P4 – Tiefe Integration**: **MCP-Tools** *(implementiert – volle Read-API-Parität:
  `calendar_events`, `calendar_event`, `calendar_history`, `calendar_series`, `calendar_surprises`,
  `calendar_next`, `calendar_blackout`, `calendar_affected_symbols`, `calendar_health`, gesteuert auf
  das Feature)* und der **Alerts `EconomicEvent`-Trigger** *(implementiert – eine `AlertRule`, die N
  Minuten vor einer bevorstehenden Veröffentlichung bei/über einem gewählten Impact auslöst, optional
  eingegrenzt auf Währungen; ausgewertet vom bestehenden Alert-Worker ohne KI, dedupliziert pro
  Veröffentlichung; erstellt via `POST /api/alerts/rules/economic-event`)*. Das
  Prop-Guard-News-Blackout-Gate **und die Copy-Trade-Blackout-Pause** sind drin (§5.1 – ein
  opt-in `App:Copy:NewsPauseEnabled`, Standard aus: eine Quellposition, deren Symbol in einem
  Critical-Impact-Blackout sitzt, wird übersprungen, Byte-identischer Hotpath wenn aus). Das
  **Backtest-Ereignis-Overlay** ist drin – `GET /api/calendar/v1/for-symbol` und das
  `calendar_events_for_symbol` MCP-Tool geben die zeitpunktgenauen, ein Symbol betreffenden
  Ereignisse in einem Fenster zurück, und die **Instance/Backtest-Report-Seite** rendert die
  High-Impact-Veröffentlichungen, die innerhalb des Backtest-Fensters fielen, unter der
  Equity-Kurve (sodass ein Autor sieht, welche Trades auf NFP gelandet sind), gesteuert und
  lokalisiert. Der gesamte Plan ist jetzt implementiert.
- **P5 – Extras**: Überraschungs-Analytik, iCal/CSV-Export, Stichwortsuche, einsteckbarer Konsens.

Siehe die [cBot & REST API-Referenz](calendar-cbot-api.md) für die Integrationsfläche.
