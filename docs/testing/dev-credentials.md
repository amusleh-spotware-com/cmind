# Dev credentials — one file for every test

All credentials the test suites need live in a single gitignored file:
`secrets/dev-credentials.local.json`. Copy the committed template and fill in what you
have — every value is optional and the tests that need a missing value skip cleanly.

```bash
mkdir -p secrets
cp dev-credentials.example.json secrets/dev-credentials.local.json
# edit secrets/dev-credentials.local.json
```

## What each test tier reads

| Tier | Needs | From |
|------|-------|------|
| **Unit** (`tests/UnitTests`) | nothing | — deterministic, no secrets, no network |
| **Integration** (`tests/IntegrationTests`) | Postgres | Testcontainers (Docker) — auto |
| **Live copy** (`tests/IntegrationTests/CopyLive`) | OpenAPI app + token cache | `OpenApi.App`, `OpenApi.Tokens` |
| **E2E onboarding** (`tests/E2ETests/CopyLive`) | OpenAPI app + cID logins | `OpenApi.App`, `OpenApi.Cids` |
| **AI features** | Anthropic key | `Ai.ApiKey` (unset ⇒ AI features return disabled, app still runs) |

## Schema

See `dev-credentials.example.json` at the repo root. Sections:

- `OpenApi.App` — `{ ClientId, ClientSecret }` of the cTrader Open API application.
- `OpenApi.Cids` — cTrader ID logins used only by the headless OAuth onboarding.
- `OpenApi.Tokens` — the multi-cID token cache (one entry per authorized cID with its
  refresh/access token + account list). Written automatically by onboarding and by the
  token-refresh step; you rarely edit it by hand.
- `Owner` — seed owner login for the app under E2E.
- `Database.ConnectionString` — only when pointing tests at an external Postgres instead
  of Testcontainers.
- `Ai.ApiKey` — Anthropic API key for the AI features.

## Precedence

1. **Environment variables** override everything (e.g. `App__OwnerPassword`, `App:Ai:ApiKey`).
2. **`secrets/dev-credentials.local.json`** — the unified file (preferred).
3. **Legacy split files** — `openapi-test-app.local.json`, `openapi-cids.local.json`,
   `openapi-tokens.local.json` are still read when the unified file is absent, so existing
   machines keep working. New setups should use the single file.

## Safety

- `secrets/` and `*.local.json` are gitignored — nothing here is ever committed.
- Live copy tests refuse to run against non-demo accounts (`IsLive` accounts are filtered
  out by `LiveCopyFixture`). Keep only demo accounts in the token cache.
- In-cluster (Kubernetes) runs mount the file as a read-only Secret; token refreshes are
  kept in memory and the read-only write-back is a silent no-op.
