---
description: "All credentials test suites need live in a single gitignored file: secrets/dev-credentials.local.json. Copy committed template và fill in what you"
---

# Dev credentials — one file for every test

All credentials test suites need live in a single gitignored file:
`secrets/dev-credentials.local.json`. Copy committed template và fill in what you
have — every value là optional và tests that need a missing value skip cleanly.

```bash
mkdir -p secrets
cp dev-credentials.example.json secrets/dev-credentials.local.json
# edit secrets/dev-credentials.local.json
```

## What each test tier reads

| Tier | Needs | From |
|------|-------|-------|
| **Unit** (`tests/UnitTests`) | nothing | — deterministic, no secrets, no network |
| **Integration** (`tests/IntegrationTests`) | Postgres | Testcontainers (Docker) — auto |
| **Live copy** (`tests/IntegrationTests/CopyLive`) | OpenAPI app + token cache | `OpenApi.App`, `OpenApi.Tokens` |
| **E2E onboarding** (`tests/E2ETests/CopyLive`) | OpenAPI app + cID logins | `OpenApi.App`, `OpenApi.Cids` |
| **E2E real run/backtest** (`CBotRealRunBacktestTests`) | a cID login + a **demo** account number | `OpenApi.Cids[].{Username,Password,Accounts}` |
| **AI features** | Anthropic key | `Ai.ApiKey` (unset ⇒ AI features return disabled, app still runs) |
| **Live economic-calendar sources** (`tests/IntegrationTests/Calendar/CalendarSourceLiveTests`) | FRED / BLS API keys | `Calendar.FredApiKey`, `Calendar.BlsApiKey` (unset ⇒ that source's live test skips; the keyless central-bank schedule still works) |

## Schema

Xem `dev-credentials.example.json` at repo root. Sections:

- `OpenApi.App` — `{ ClientId, ClientSecret }` của cTrader Open API application.
- `OpenApi.Cids` — cTrader ID logins used by headless OAuth onboarding. Each entry also
  carries an **`Accounts`** array — cTrader trading-account numbers (login/account number,
  ví dụ `3635817`) under that cID mà test infrastructure được phép link into app và
  drive. `CBotRealRunBacktestTests` reads first entry that has non-empty `Accounts` array,
  adds that cID + account to app, rồi really runs và backtests a cBot on it. **Put only
  demo account numbers here** — never a live account; run/backtest tests place real orders on
  whatever account you list. Empty/omitted `Accounts` ⇒ real run/backtest test skips cleanly.
- `OpenApi.Tokens` — multi-cID token cache (one entry per authorized cID với its
  refresh/access token + account list). Written automatically by onboarding và by
  token-refresh step; bạn hiếm khi edit it by hand.
- `Owner` — seed owner login for app under E2E.
- `Database.ConnectionString` — only when pointing tests at external Postgres instead
  of Testcontainers.
- `Ai.ApiKey` — Anthropic API key for AI features.
- `Calendar.FredApiKey` — [FRED](https://fredaccount.stlouisfed.org/apikeys) (St. Louis Fed) API key. The primary economic-calendar value source (interest rates, inflation, employment).
- `Calendar.BlsApiKey` — [BLS](https://data.bls.gov/registrationEngine/) (US Bureau of Labor Statistics) v2 registration key (CPI, PPI, employment, JOLTS). Absent ⇒ the low-quota public tier.

  Both feed the exact `FredSource`/`BlsSource` the ingestion worker uses. With a key present, `CalendarSourceLiveTests` hits the real provider and asserts observations come back; absent, that source's test skips cleanly. The app also reads these at runtime via `App:Calendar:FredApiKey` / `App:Calendar:BlsApiKey` (environment variables override — e.g. `FRED_API_KEY`, `BLS_API_KEY`).

## Precedence

1. **Environment variables** override everything (ví dụ `App__OwnerPassword`, `App:Ai:ApiKey`).
2. **`secrets/dev-credentials.local.json`** — unified file (preferred).
3. **Legacy split files** — `openapi-test-app.local.json`, `openapi-cids.local.json`,
   `openapi-tokens.local.json` still read when unified file absent, vì vậy existing
   machines keep working. New setups nên use single file.

## Safety

- `secrets/` và `*.local.json` are gitignored — nothing here bao giờ committed.
- Live copy tests refuse to run against non-demo accounts (`IsLive` accounts filtered
  out by `LiveCopyFixture`). Keep only demo accounts in token cache.
- In-cluster (Kubernetes) runs mount file as a read-only Secret; token refreshes kept
  in memory và read-only write-back là silent no-op.
