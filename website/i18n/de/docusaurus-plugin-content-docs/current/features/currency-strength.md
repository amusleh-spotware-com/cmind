# KI-Makro-Währungsstärke & Forward-Ausblick

cMind versendet eine **KI-gestützte, mathematisch-deterministische** Makro-Währungsstärke-Engine. Sie bewertet ein konfigurierbares Universum von Währungen — die 8 Majors plus Schwellenmarkt- und exotische Währungen — nach **aktueller** fundamentaler Stärke, und projiziert einen **Forward-Direktional-Ausblick** für jedes Paar über einen gewählten Horizont (1M / 3M / 6M / 12M). Jede Bewertung, jede Paar-Neigung und jede Zahl wird durch reine deterministische Mathematik im Domänen-Core berechnet; das LLM *sammelt* nur die Forward-Looking-Eingaben, die die Daten nicht veröffentlichen können, und *erklärt* das Ergebnis in klarem Englisch. Es erfindet nie eine Bewertung, eine Richtung oder eine Zahl.

> **Ehrliche Einschränkung.** Fundamentale Wirtschaftsdaten prognostizieren mittelfristige bis langfristige Werte gut und kurzfristige Werte schlecht. Behandeln Sie dies als einen Positionierungs- / Confluenz-Filter, **nicht** als kurzfristiges Timing-Signal. Lesarten in der Nähe von High-Impact-Veröffentlichungen (NFP/CPI/Zentralbank) sind laut. Keine Finanzberatung.

## Wie es funktioniert

1. **Aktuelle Fundamentale kommen vom Wirtschaftskalender, nicht vom LLM.** Die harten Zahlen — politische Sätze, CPI vs Ziel, BIP, Beschäftigung, Handelsbilanz — und ihre **Überraschungs-z-Scores** werden **Point-in-Time** aus dem [Wirtschaftskalender](./economic-calendar.md)-Modul beschafft (FRED/BLS/BEA/ECB und Zentralbank-Zeitpläne). Ein historisches Snapshot-Leak nie Look-Ahead.
2. **Das LLM sammelt nur, was der Kalender nicht veröffentlichen kann** — pro Währung: die **Forward**-Trajektorie (erwarteter Politik-Ratenpfad in bp, Inflationstrend-vs-Ziel, Wachstumsmomentum) und ein **geopolitischer** Ausblick (Risk-On/Off, Tarife, Fiskal/Schulden, Wahlen), plus alle EM/exotischen aktuellen Zahlen, die der Kalender fehlen. Strenge JSON, Tier-bewusste Validierung, Websuche auf.
3. **Die Domäne berechnet die Bewertung und die Forward-Matrix deterministisch.** Jeder Treiber wird als ein **Within-Tier-z-Score** bewertet (sodass eine 50%-Inflation-Exotik nie die Majors verzerrt), winsorisiert, gewicht-summiert in ein Composite, und bewertet stärkst→schwächst mit einer stabilen ISO-Tie-Break. Die Forward-Schicht trägt jede Composite entlang ihrer Trajektorie — `projected = current + horizonScale · Σ trajectoryDriver·weight` — und bildet jedes Paar-projected-Differential auf eine **Direktional-Neigung** (▲ Aufwertung / ▬ Neutral / ▼ Abwertung) mit einer Überzeugung ab.
4. **Das LLM erklärt** die Bewertung und die Top-Paar-Calls in klarer Sprache.

## Die Treiber

| Treiber | Auswirkung auf Stärke | Notizen |
|---|---|---|
| Politik-Rate & Trajektorie | Höher / Hawkish ⇒ stärker | Höchstes Gewicht; Zentralbank-Divergenz treibt die größten Lücken. |
| Inflation (CPI vs Ziel) | Über Ziel ⇒ schwächer | Rückwärts bewertet (Kaufkraft-Drag). |
| BIP-Wachstum | Höheres relatives Wachstum ⇒ stärker | Differential vs das Panel. |
| Beschäftigung | Stärkere Arbeit ⇒ stärker | Füttert den Politik-Pfad. |
| Handelsbilanz / Leistungsbilanz | Überschuss ⇒ stärker | Strukturelle Nachfrage. |
| Politik-Haltung | Hawkish ⇒ stärker | Der primäre langfristige Treiber. |
| Überraschungsmomentum | Jüngste Übererfüllungen ⇒ stärker | Aus den Überraschungs-z-Scores des Kalenders. |
| Geopolitisch / Risiko | Risk-Off ⇒ sichere Häfen (USD/JPY/CHF) stärker | Begrenzte Forward-Risiko-Delta. |
| Realrendite / Carry *(EM/exotisch)* | Positive Realquote ⇒ stärker | Dominanter EM-Treiber in ruhigen Regimen. |
| Externe Anfälligkeit *(EM/exotisch)* | Defizite / niedrige Reserven / USD-Schulden ⇒ schwächer | Struktureller Abwertungsdruck. |
| Terms of Trade *(Rohstoffexporteure)* | Steigende Exportpreise ⇒ stärker | BRL, ZAR, CLP, NOK, AUD, CAD. |
| Politisches / institutionelles Risiko *(EM/exotisch)* | Instabilität ⇒ schwächer | Breiteres Totband, begrenzte Überzeugung. |

## Gestaffeltes Universum (Majors + EM + Exotics)

Das Universum ist **Bereitstellungs-konfigurierbar** (`App:CurrencyStrength:Universe`) — das Hinzufügen einer Währung ist Config, kein Code. Jede Währung trägt einen **Tier** (`Major` / `EmergingMarket` / `Exotic`), der Gewichtung, Dead-Band-Breite und Überzeugungsobergrenze abstimmt:

- **Majors** — USD, EUR, GBP, JPY, AUD, NZD, CAD, CHF (Rate-Level-geführt).
- **Schwellenmärkte** — CNH, INR, BRL, MXN, ZAR, KRW, SGD, PLN (+ Scandi NOK/SEK); Carry + Risiko + externe-Anfälligkeit-Gewicht, mittleres Vertrauen.
- **Exotics** — TRY, HUF, CZK, plus USD-gekoppelt HKD/SAR; niedriges Vertrauen, breiteres Dead-Band, begrenzte Überzeugung. **Gekoppelte / stark verwaltete** Währungen (HKD, SAR, CNH) sind flagged, ihre Trajektorie ist herab-gewichtet, und ihre Paar-Ausblick ist gegen `Neutral` geklemmt, sodass ein Peg niemals als Free-Float-Signal gelesen wird.

Da offizielle EM/exotische Statistiken niedriger-Häufigkeit, überarbeitet und manchmal undurchsichtig sind, tragen die KI-gesammelten Zahlen ein **pro-Tier-Vertrauen**, das als Zuverlässigkeits-Badge angezeigt wird.

## Graceful Degradation

| Kalender | KI | Ergebnis |
|---|---|---|
| ✅ | ✅ | Vollständige Bewertung + Forward-Projektion + Narrative (`CalendarAndAi`). |
| ✅ | ❌ | Kalender-Only-aktuelle Bewertung, keine Forward-Projektion (`CalendarOnly`). |
| ❌ | ✅ | KI-gesammelte aktuelle Zahlen + Forward, niedriges Vertrauen (`AiOnly`). |
| ❌ | ❌ | Keine Snapshot — das Widget verbirgt und die Seite zeigt einen leeren Zustand. |

Die App läuft unverändert. KI wird auf dem KI-Schlüssel gated; das Kalender-Bein respektiert sein eigenes White-Label-Tor + Runtime-Toggle.

## Mit ihm nutzen

- **Aktiviere AI** (Einstellungen → AI) und **schalte das Widget** aus Deinem eigenen Dashboard **Customize**-Dialog ein ("Currency strength" — optional, standardmäßig verborgen). Das Widget zeigt die Top-Stark/Schwach-Währungen und den Top-3M-Paar-Call; es verlinkt zur vollständigen Seite.
- **Vollständige Seite** — `/ai/currency-strength`: ein Horizont-Selector (1M/3M/6M/12M), ein Tier-Filter (All/Majors/EM/Exotics), die aktuelle Bewertung, die Forward-Vorhersage, die Paar-Ausblick-Matrix (Neigung + Überzeugung, gekoppelt/niedriges-Vertrauen flagged), und die KI-Narrative. Drücken Sie **Jetzt aktualisieren** (Besitzer) zum Regenerieren. Ein Background-Worker (`App:CurrencyStrength:RefreshEnabled`, **Standard `true`**) aktualisiert auf einem Schedule, sodass die Seite aus der Box bevölkert wird; eine Bereitstellung oder der Besitzer dreht es aus (oder deaktiviert die KI / Wirtschaftskalender-Feature, das der Refresher durch Abbau zu keinem Snapshot erkennt).

## Programmatischer Zugang

Ein gemeinsames Read-Modell (`ICurrencyStrengthQuery`) ist auf drei Arten erreichbar:

- **In-App-KI** — direkt (In-Prozess) in KI-Features injiziert.
- **MCP** — das `currency_strength`-Tool (Parameter `horizon`, `tier`) für KI-Clients/Agents.
- **cBot REST** — `GET /api/market/v1/currency-strength/{latest,history,pair/{base}/{quote}}`, gesichert durch die **gleiche** `CalendarJwt`-Maschinerie wie die [Kalender-cBot-API](./calendar-cbot-api.md) mit hinzugefügtem **`market:read`**-Scope. Ein cBot registriert einen API-Client mit `market:read`, tauscht seine ID + Geheimnis für ein kurzfristiges JWT unter `POST /api/calendar/v1/token` aus, und ruft die Endpunkte mit einem `Bearer`-Token auf. Kein zweites JWT-Schema, kein zweites Geheimnis — ein durchgesickertes Token ist Read-Only, Market-Scoped, kurzfristig und widerufbar.

Siehe die [Kalender-cBot-API](./calendar-cbot-api.md) für den Token-Fluss und ein Copy-Paste-Sample.
