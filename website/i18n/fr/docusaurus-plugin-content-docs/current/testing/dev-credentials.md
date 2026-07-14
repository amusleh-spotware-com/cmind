---
description: "Tous les identifiants dont les suites de test ont besoin vivent dans un fichier gitignored unique : secrets/dev-credentials.local.json. Copiez le modèle commis et remplissez ce que vous"
---

# Identifiants de dev — un fichier pour chaque test

Tous les identifiants dont les suites de test ont besoin vivent dans un fichier gitignored unique :
`secrets/dev-credentials.local.json`. Copiez le modèle commis et remplissez ce que vous
avez — chaque valeur est optionnelle et les tests qui ont besoin d'une valeur manquante sautent proprement.

```bash
mkdir -p secrets
cp dev-credentials.example.json secrets/dev-credentials.local.json
# modifiez secrets/dev-credentials.local.json
```

## Ce que chaque tier de test lit

| Tier | Besoins | De |
|------|-------|------|
| **Unit** (`tests/UnitTests`) | rien | — déterministe, pas de secrets, pas de réseau |
| **Integration** (`tests/IntegrationTests`) | Postgres | Testcontainers (Docker) — auto |
| **Live copy** (`tests/IntegrationTests/CopyLive`) | app OpenAPI + cache de token | `OpenApi.App`, `OpenApi.Tokens` |
| **E2E onboarding** (`tests/E2ETests/CopyLive`) | app OpenAPI + logins cID | `OpenApi.App`, `OpenApi.Cids` |
| **E2E real run/backtest** (`CBotRealRunBacktestTests`) | un login cID + un numéro de compte **démo** | `OpenApi.Cids[].{Username,Password,Accounts}` |
| **AI features** | clé Anthropic | `Ai.ApiKey` (non défini ⇒ les features IA retournent désactivé, l'app fonctionne toujours) |
| **Live economic-calendar sources** (`tests/IntegrationTests/Calendar/CalendarSourceLiveTests`) | FRED / BLS API keys | `Calendar.FredApiKey`, `Calendar.BlsApiKey` (unset ⇒ that source's live test skips; the keyless central-bank schedule still works) |

## Schéma

Voir `dev-credentials.example.json` à la racine du repo. Sections :

- `OpenApi.App` — `{ ClientId, ClientSecret }` de l'application Open API cTrader.
- `OpenApi.Cids` — logins cTrader ID utilisés par l'onboarding OAuth headless. Chaque entrée porte aussi
  un tableau **`Accounts`** — les numéros de compte de trading cTrader (le login/numéro de compte,
  par ex. `3635817`) sous ce cID que l'infrastructure de test est autorisée à lier dans l'app et
  conduire. `CBotRealRunBacktestTests` lit la première entrée qui a un tableau `Accounts` non-vide,
  ajoute ce cID + compte à l'app, puis exécute réellement et backteste un cBot sur lui. **Mettez seulement
  les numéros de compte démo ici** — jamais un compte en direct ; les tests run/backtest placent de vrais ordres sur
  n'importe quel compte que vous listez. `Accounts` vide/omis ⇒ le test real run/backtest saute proprement.
- `OpenApi.Tokens` — le cache de token multi-cID (une entrée par cID autorisé avec son
  token de rafraîchissement/accès + liste de compte). Écrit automatiquement par l'onboarding et par le
  pas de rafraîchissement de token ; vous le modifiez rarement à la main.
- `Owner` — seed login propriétaire pour l'app sous E2E.
- `Database.ConnectionString` — uniquement quand les tests pointent vers un Postgres externe au lieu
  de Testcontainers.
- `Ai.ApiKey` — clé API Anthropic pour les features IA.
- `Calendar.FredApiKey` — [FRED](https://fredaccount.stlouisfed.org/apikeys) (St. Louis Fed) API key. The primary economic-calendar value source (interest rates, inflation, employment).
- `Calendar.BlsApiKey` — [BLS](https://data.bls.gov/registrationEngine/) (US Bureau of Labor Statistics) v2 registration key (CPI, PPI, employment, JOLTS). Absent ⇒ the low-quota public tier.

  Both feed the exact `FredSource`/`BlsSource` the ingestion worker uses. With a key present, `CalendarSourceLiveTests` hits the real provider and asserts observations come back; absent, that source's test skips cleanly. The app also reads these at runtime via `App:Calendar:FredApiKey` / `App:Calendar:BlsApiKey` (environment variables override — e.g. `FRED_API_KEY`, `BLS_API_KEY`).

## Précédence

1. **Les variables d'environnement** remplacent tout (par ex. `App__OwnerPassword`, `App:Ai:ApiKey`).
2. **`secrets/dev-credentials.local.json`** — le fichier unifié (préféré).
3. **Fichiers divisés hérités** — `openapi-test-app.local.json`, `openapi-cids.local.json`,
   `openapi-tokens.local.json` sont toujours lus quand le fichier unifié est absent, donc les machines existantes continuent de fonctionner. Les nouvelles configurations doivent utiliser le fichier unique.

## Sécurité

- `secrets/` et `*.local.json` sont gitignorés — rien ici n'est jamais commis.
- Les tests en direct de copie refusent de s'exécuter contre les comptes non-démo (les comptes `IsLive` sont filtrés
  par `LiveCopyFixture`). Conservez seulement les comptes démo dans le cache de token.
- Les runs in-cluster (Kubernetes) montent le fichier en Secret en lecture seule ; les rafraîchissements de token sont
  tenus en mémoire et la write-back en lecture seule est une no-op silencieuse.
