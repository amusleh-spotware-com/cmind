---
description: "Alle Anmeldedaten die Test-Suiten benötigen leben in einer einzigen Gitignore-Datei: secrets/dev-credentials.local.json. Kopieren Sie die festgeschriebene Vorlage und füllen Sie aus was Sie…"
---

# Dev-Anmeldedaten — eine Datei für jeden Test

Alle Anmeldedaten die Test-Suiten benötigen leben in einer einzigen Gitignore-Datei: `secrets/dev-credentials.local.json`. Kopieren Sie die festgeschriebene Vorlage und füllen Sie aus, was Sie haben — jeder Wert ist optional und die Tests die einen fehlenden Wert brauchen, überspringen sauber.

```bash
mkdir -p secrets
cp dev-credentials.example.json secrets/dev-credentials.local.json
# edit secrets/dev-credentials.local.json
```

## Was jede Test-Stufe liest

| Stufe | Braucht | Von |
|------|-------|------|
| **Unit** (`tests/UnitTests`) | nichts | — Deterministisch, keine Geheimnisse, kein Netzwerk |
| **Integration** (`tests/IntegrationTests`) | Postgres | Testcontainer (Docker) — Auto |
| **Live Copy** (`tests/IntegrationTests/CopyLive`) | OpenAPI App + Token Cache | `OpenApi.App`, `OpenApi.Tokens` |
| **E2E Onboarding** (`tests/E2ETests/CopyLive`) | OpenAPI App + cID Logins | `OpenApi.App`, `OpenApi.Cids` |
| **E2E echten Run/Backtest** (`CBotRealRunBacktestTests`) | ein cID Login + ein **Demo** Konto-Nummer | `OpenApi.Cids[].{Username,Password,Accounts}` |
| **KI-Features** | Anthropic-Schlüssel | `Ai.ApiKey` (nicht gesetzt ⇒ KI-Features geben deaktiviert zurück, App läuft noch) |
| **Live economic-calendar sources** (`tests/IntegrationTests/Calendar/CalendarSourceLiveTests`) | FRED / BLS API keys | `Calendar.FredApiKey`, `Calendar.BlsApiKey` (unset ⇒ that source's live test skips; the keyless central-bank schedule still works) |

## Schema

Siehe `dev-credentials.example.json` im Repo-Root. Abschnitte:

- `OpenApi.App` — `{ ClientId, ClientSecret }` der cTrader Open API-Anwendung.
- `OpenApi.Cids` — cTrader ID Logins verwendet von der Headless-OAuth-Onboarding. Jeder Eintrag trägt auch ein **`Accounts`** Array — die cTrader Handels-Konten-Zahlen (das Login/Konten-Nummer, z. B. `3635817`) unter die cID dass die Test-Infrastruktur erlaubt zu linken in die App und fahren. `CBotRealRunBacktestTests` liest den ersten Eintrag, der ein nicht-leeres `Accounts` Array hat, fügt die cID + Konto zur App hinzu, dann läuft echte und backtestet ein cBot auf es. **Nur Demo-Konten-Nummern hier** — niemals ein Live-Konto; die Run/Backtest Tests platzieren echte Bestellungen auf, was auch Konto Sie auflisten. Leer/omitted `Accounts` ⇒ der echte Run/Backtest Test überspringt sauber.
- `OpenApi.Tokens` — der Multi-cID Token Cache (ein Eintrag pro autorisiert cID mit sein Aktualisierungs-/Zugriff-Token + Konten-Liste). Automatisch geschrieben durch Onboarding und Token-Aktualisierungs-Schritt; Sie bearbeiten es selten von Hand.
- `Owner` — Seed-Besitzer Login für die App unter E2E.
- `Database.ConnectionString` — nur wenn Tests auf einen externen Postgres anstatt Testcontainer verweisen.
- `Ai.ApiKey` — Anthropic API-Schlüssel für die KI-Features.
- `Calendar.FredApiKey` — [FRED](https://fredaccount.stlouisfed.org/apikeys) (St. Louis Fed) API key. The primary economic-calendar value source (interest rates, inflation, employment).
- `Calendar.BlsApiKey` — [BLS](https://data.bls.gov/registrationEngine/) (US Bureau of Labor Statistics) v2 registration key (CPI, PPI, employment, JOLTS). Absent ⇒ the low-quota public tier.

  Both feed the exact `FredSource`/`BlsSource` the ingestion worker uses. With a key present, `CalendarSourceLiveTests` hits the real provider and asserts observations come back; absent, that source's test skips cleanly. The app also reads these at runtime via `App:Calendar:FredApiKey` / `App:Calendar:BlsApiKey` (environment variables override — e.g. `FRED_API_KEY`, `BLS_API_KEY`).

## Präzedenz

1. **Umgebungsvariablen** überschreiben alles (z. B. `App__OwnerPassword`, `App:Ai:ApiKey`).
2. **`secrets/dev-credentials.local.json`** — die einheitliche Datei (bevorzugt).
3. **Legacy Split-Dateien** — `openapi-test-app.local.json`, `openapi-cids.local.json`, `openapi-tokens.local.json` werden noch gelesen wenn die einheitliche Datei fehlend ist, daher halten vorhandene Maschinen arbeiten. Neue Setups sollten die einzelne Datei verwenden.

## Sicherheit

- `secrets/` und `*.local.json` werden gitignoriert — nichts hier wird jemals festgeschrieben.
- Live Copy Tests weigern sich gegen nicht-Demo Konten zu laufen (`IsLive` Konten werden gefiltert von `LiveCopyFixture`). Halten Sie nur Demo Konten im Token Cache.
- In-Cluster (Kubernetes) läufe montiert die Datei als Lese-nur Secret; Token-Aktualisierungen werden im Speicher gehalten und die Lese-nur Schreib-Rück ist eine stille Noop.
