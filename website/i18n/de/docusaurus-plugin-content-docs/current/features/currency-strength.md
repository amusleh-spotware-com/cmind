# AI-Makro-Währungsstärke & Forward-Outlook

cMind bietet eine **KI-gestützte, mathematisch-deterministische** Makro-Währungsstärke-Engine. Sie bewertet
ein konfigurierbares Universum von Währungen – die 8 Majors plus Schwellenländer- und Exoten-Währungen –
nach **aktueller** fundamentaler Stärke und projiziert einen **Forward-Direktionalausblick** für jedes
Paar über einen gewählten Horizont (1M / 3M / 6M / 12M). Jeder Rang, jedes Paar-Bias und jede Zahl
werden durch reine deterministische Mathematik im Domain-Kern berechnet; das LLM sammelt *nur* die
zukunftsgerichteten Inputs, die die Daten nicht veröffentlichen können, und *erklärt* das Ergebnis in
Klartext. Es erfindet niemals einen Rang, eine Richtung oder eine Zahl.

> **Ehrliche Einschränkung.** Fundamentals sagen den mittel- bis langfristigen Wert gut und den
> kurzfristigen schlecht voraus. Behandeln Sie dies als Positionierungs-/Konfluenz-Filter, **nicht**
> als kurzfristiges Timing-Signal. Lesungen nahe High-Impact-Veröffentlichungen (NFP/CPI/Zentralbank) sind
> noisig. Keine Finanzberatung.

## Wie es funktioniert

1. **Aktuelle Fundamentals kommen vom Economic Calendar, nicht vom LLM.** Die harten Zahlen –
   Leitzinsen, CPI vs. Ziel, BIP, Beschäftigung, Handelsbilanz – und ihre **Surprise z-Scores** werden
   **zeitpunktgenau** aus dem [Economic Calendar](./economic-calendar.md)-Modul bezogen
   (FRED/BLS/BEA/ECB und Zentralbank-Pläne). Ein historischer Snapshot leakt niemals Look-Ahead.
2. **Das LLM sammelt nur das, was der Kalender nicht veröffentlichen kann** – pro Währung: die
   **Forward**-Trajektorie (erwarteter Leitzinspfad in bp, Inflationstrend-vs.-Ziel,
   Wachstumsmomentum) und einen **geopolitischen** Ausblick (Risk-On/Off, Zölle,
   Fiskal-/Verschuldung, Wahlen), plus etwaige EM/Exoten-Stromgrößen, die dem Kalender fehlen. Strenges
   JSON, tier-bewusste Validierung, Web-Suche an.
3. **Die Domain berechnet Rang und Forward-Matrix deterministisch.** Jeder Treiber wird als
   **Within-Tier z-Score** bewertet (sodass eine 50%-Inflation Exotic niemals die Majors verzerrt),
   winsorisiert, gewichtssummiert zu einem Composite und nach Stärkste→Schwächste mit stabilem
   ISO-Tie-Break gerangelt. Die Forward-Schicht trägt jedes Composite entlang seiner Trajektorie –
   `projected = current + horizonScale · Σ trajectoryDriver·weight` – und bildet jedes Paar-Differential
   auf ein **direktionales Bias** ab (▲ aufwerten / ▬ neutral / ▼ abwerten) mit einer Überzeugung.
4. **Das LLM erklärt** den Rang und die Top-Paar-Rufe in Klartext.

## Die Treiber

| Treiber | Effekt auf Stärke | Anmerkungen |
|---|---|---|
| Leitzins & Trajektorie | Höher / hawkish ⇒ stärker | Höchstes Gewicht; Zentralbank-Divergenz treibt die größten Lücken. |
| Inflation (CPI vs. Ziel) | Über Ziel ⇒ schwächer | Umgekehrt bewertet (Kaufkraftabschreibung). |
| BIP-Wachstum | Höheres relatives Wachstum ⇒ stärker | Differenzial zum Panel. |
| Beschäftigung | Stärkerer Arbeitsmarkt ⇒ stärker | Speist den Leitzinspfad. |
| Handelsbilanz / Leistungsbilanz | Überschuss ⇒ stärker | Strukturelle Nachfrage. |
| geldpolitische Haltung | Hawkish ⇒ stärker | Der primäre langfristige Treiber. |
| Überraschungsmomentum | Jüngste Beats ⇒ stärker | Aus den Surprise z-Scores des Kalenders. |
| Geopolitisch / Risiko | Risk-Off ⇒ sichere Häfen (USD/JPY/CHF) stärker | Begrenztes Forward-Risiko-Delta. |
| Realzins / Carry *(EM/Exoten)* | Positiver Realzins ⇒ stärker | Dominanter EM-Treiber in ruhigen Regimes. |
| Externe Vulnerabilität *(EM/Exoten)* | Defizite / niedrige Reserven / USD-Verschuldung ⇒ schwächer | Struktureller Abwertungsdruck. |
| Terms of Trade *(Rohstoffexporteure)* | Steigende Exportpreise ⇒ stärker | BRL, ZAR, CLP, NOK, AUD, CAD. |
| Politisch / institutionelles Risiko *(EM/Exoten)* | Instabilität ⇒ schwächer | Breiterer Dead-Band, gedeckelte Überzeugung. |

## Tiered Universe (Majors + EM + Exoten)

Das Universum ist **Deployment-konfigurierbar** (`App:CurrencyStrength:Universe`) – eine Währung
hinzuzufügen ist Config, nicht Code. Jede Währung trägt ein **Tier** (`Major` /
`EmergingMarket` / `Exotic`), das Gewichtung, Dead-Band-Breite und Überzeugungs-Cap abstimmt:

- **Majors** — USD, EUR, GBP, JPY, AUD, NZD, CAD, CHF (Zinsniveau-geführt).
- **Schwellenländer** — CNH, INR, BRL, MXN, ZAR, KRW, SGD, PLN (+ Skandinavien NOK/SEK); Carry + Risk +
  externe Vulnerabilität hochgewichtet, mittleres Vertrauen.
- **Exoten** — TRY, HUF, CZK, plus USD-gekoppelte HKD/SAR; niedriges Vertrauen, breiterer Dead-Band,
  gedeckelte Überzeugung. **Gekoppelte/stark verwaltete** Währungen (HKD, SAR, CNH) werden markiert,
  ihre Trajektorie heruntergewichtet und ihr Paar-Outlook Richtung `Neutral` geklemmt, sodass eine
  Kopplung niemals als Free-Floating-Signal gelesen wird.

Da offizielle EM/Exoten-Statistiken niedrigfrequenter, revidiert und manchmal undurchsichtig sind,
tragen die KI-gathering-Zahlen ein **per-Tier-Vertrauen**, gezeigt als Zuverlässigkeits-Badge.

## Graceful Degradation

| Kalender | KI | Ergebnis |
|---|---|---|
| ✅ | ✅ | Voller Rang + Forward-Projektion + Narrative (`CalendarAndAi`). |
| ✅ | ❌ | Nur Kalender aktueller Rang, keine Forward-Projektion (`CalendarOnly`). |
| ❌ | ✅ | KI-gesammelte aktuelle Zahlen + Forward, niedrigeres Vertrauen (`AiOnly`). |
| ❌ | ❌ | Kein Snapshot – das Widget versteckt sich und die Seite zeigt einen leeren State. |

Die App läuft in beiden Fällen unverändert. KI wird durch den KI-Schlüssel gesteuert; der
Kalender-Schenkel respektiert sein eigenes White-Label-Gate + Runtime-Toggle.

## Nutzung

- **KI aktivieren** (Settings → KI) und **das Widget einschalten** aus Ihrem eigenen Dashboard
  **Anpassen**-Dialog („Currency Strength" – Opt-in, standardmäßig versteckt). Das Widget zeigt die
  stärksten/schwächsten Währungen und den Top-3M-Paar-Ruf; es verlinkt zur vollständigen Seite.
- **Vollständige Seite** — `/ai/currency-strength`: ein Horizont-Selektor (1M/3M/6M/12M), ein
  Tier-Filter (Alle/Majors/EM/Exoten), die aktuelle Bewertung, die Forward-Prognose, die
  Paar-Outlook-Matrix (Bias + Überzeugung, gekoppelt/niedriges Vertrauen markiert) und die
  KI-Narrative. Drücken Sie **Jetzt aktualisieren** (Eigentümer), um zu regenerieren. Ein
  Hintergrund-Worker (`App:CurrencyStrength:RefreshEnabled`, **Standard `true`**) aktualisiert nach
  einem Zeitplan, sodass die Seite out-of-the-box bevölkert ist; ein Deployment oder der
  Eigentümer schaltet ihn aus (oder deaktiviert das KI-/Economic-Calendar-Feature, was der
  Refresher durch Degradierung zu No-Snapshot respektiert).

## Programmatischer Zugriff

Ein gemeinsames Read-Modell (`ICurrencyStrengthQuery`) ist auf drei Wegen erreichbar:

- **In-App-KI** — direkt injiziert (In-Process) in KI-Features.
- **MCP** — das `currency_strength`-Tool (Parameter `horizon`, `tier`) für KI-Clients/Agenten.
- **cBot REST** — `GET /api/market/v1/currency-strength/{latest,history,pair/{base}/{quote}}`,
  gesichert durch **dieselbe** `CalendarJwt`-Maschinerie wie die
  [Kalender cBot API](./calendar-cbot-api.md) mit einem zusätzlichen **`market:read`**-Scope. Ein
  cBot registriert einen API-Client mit `market:read`, tauscht seine ID und sein Geheimnis gegen ein
  kurzlebiges JWT bei `POST /api/calendar/v1/token` und ruft die Endpunkte mit einem `Bearer`-Token
  auf. Kein zweites JWT-Schema, kein zweites Geheimnis – ein durchgesickertes Token ist
  schreibgeschützt, marktbezogen, kurzlebig und widerrufbar.

Siehe die [Kalender cBot API](./calendar-cbot-api.md) für den Token-Flow und ein Copy-Paste-Beispiel.
