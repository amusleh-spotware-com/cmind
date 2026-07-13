---
description: "Wszystkie credentials test suites potrzebują żyją w single gitignored file: secrets/dev-credentials.local.json. Copy committed template i fill w co ty"
---

# Dev credentials — jeden file dla każdego test

Wszystkie credentials test suites potrzebują żyją w single gitignored file:
`secrets/dev-credentials.local.json`. Copy committed template i fill w co ty
masz — każda wartość jest optional i testy że potrzebują brakującej wartości skip cleanly.

```bash
mkdir -p secrets
cp dev-credentials.example.json secrets/dev-credentials.local.json
# edit secrets/dev-credentials.local.json
```

## Co każdy test tier czyta

| Tier | Potrzeby | Z |
|------|---------|---|
| **Unit** (`tests/UnitTests`) | nic | — deterministic, brak sekretów, brak network |
| **Integration** (`tests/IntegrationTests`) | Postgres | Testcontainers (Docker) — auto |
| **Live copy** (`tests/IntegrationTests/CopyLive`) | OpenAPI app + token cache | `OpenApi.App`, `OpenApi.Tokens` |
| **E2E onboarding** (`tests/E2ETests/CopyLive`) | OpenAPI app + cID logins | `OpenApi.App`, `OpenApi.Cids` |
| **E2E real run/backtest** (`CBotRealRunBacktestTests`) | cID login + **demo** account number | `OpenApi.Cids[].{Username,Password,Accounts}` |
| **AI features** | Anthropic key | `Ai.ApiKey` (unset ⇒ AI features zwracają disabled, app ciągle runs) |

## Schema

Zobacz `dev-credentials.example.json` na repo root. Sekcje:

- `OpenApi.App` — `{ ClientId, ClientSecret }` z cTrader Open API application.
- `OpenApi.Cids` — cTrader ID logins używane przez headless OAuth onboarding. Każdy entry także
  niesie **`Accounts`** array — cTrader trading-account numbers (login/account number,
  np. `3635817`) pod tym cID że test infrastructure jest allowed do link do app i
  drive. `CBotRealRunBacktestTests` czyta pierwszy entry że ma non-empty `Accounts` array,
  adds że cID + account do app, potem really runs i backtests cBot na to. **Put tylko
  demo account numbers tutaj** — nigdy live account; run/backtest testy place real orders
  na whatever account ty list. Empty/omitted `Accounts` ⇒ real run/backtest test skips cleanly.
- `OpenApi.Tokens` — multi-cID token cache (jeden entry per authorized cID z jego
  refresh/access token + account list). Written automatycznie przez onboarding i przez
  token-refresh step; ty rarely edit to ręcznie.
- `Owner` — seed owner login dla app pod E2E.
- `Database.ConnectionString` — tylko gdy wskazując tests na external Postgres zamiast
  Testcontainers.
- `Ai.ApiKey` — Anthropic API key dla AI features.

## Precedence

1. **Environment variables** override wszystko (np. `App__OwnerPassword`, `App:Ai:ApiKey`).
2. **`secrets/dev-credentials.local.json`** — unified file (preferred).
3. **Legacy split files** — `openapi-test-app.local.json`, `openapi-cids.local.json`,
   `openapi-tokens.local.json` są ciągle czytane gdy unified file jest absent, więc existing
   machines keep working. Nowe setups powinny używać single file.

## Safety

- `secrets/` i `*.local.json` są gitignored — nic tutaj nigdy nie jest committed.
- Live copy tests refuse do run przeciwko non-demo accounts (`IsLive` accounts są filtered
  out przez `LiveCopyFixture`). Keep tylko demo accounts w token cache.
- In-cluster (Kubernetes) runs mount file jako read-only Secret; token refreshes są
  kept w memory i read-only write-back jest silent no-op.
