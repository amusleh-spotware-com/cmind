---
description: "Semua credential yang test suite butuhkan hidup di single gitignored file: secrets/dev-credentials.local.json. Copy committed template dan isi apa yang Anda"
---

# Dev credential — satu file untuk setiap test

Semua credential yang test suite butuhkan hidup di single gitignored file: `secrets/dev-credentials.local.json`. Copy committed template dan isi apa yang Anda punya — setiap value adalah optional dan test yang butuh missing value skip cleanly.

```bash
mkdir -p secrets
cp dev-credentials.example.json secrets/dev-credentials.local.json
# edit secrets/dev-credentials.local.json
```

## Apa setiap test tier baca

| Tier | Butuh | Dari |
|------|-------|------|
| **Unit** (`tests/UnitTests`) | tidak ada | — deterministic, no secret, no network |
| **Integration** (`tests/IntegrationTests`) | Postgres | Testcontainers (Docker) — auto |
| **Live copy** (`tests/IntegrationTests/CopyLive`) | OpenAPI app + token cache | `OpenApi.App`, `OpenApi.Tokens` |
| **E2E onboarding** (`tests/E2ETests/CopyLive`) | OpenAPI app + cID login | `OpenApi.App`, `OpenApi.Cids` |
| **E2E real run/backtest** (`CBotRealRunBacktestTests`) | cID login + **demo** account number | `OpenApi.Cids[].{Username,Password,Accounts}` |
| **Fitur AI** | Anthropic key | `Ai.ApiKey` (unset ⇒ fitur AI return disabled, app masih berjalan) |

## Schema

Lihat `dev-credentials.example.json` di repo root. Section:

- `OpenApi.App` — `{ ClientId, ClientSecret }` dari aplikasi cTrader Open API.
- `OpenApi.Cids` — login cTrader ID digunakan oleh headless OAuth onboarding. Setiap entry juga membawa array **`Accounts`** — nomor trading-account cTrader (login/account number, mis. `3635817`) di bawah cID itu yang infrastruktur test diizinkan untuk link ke app dan drive. `CBotRealRunBacktestTests` membaca first entry yang punya non-empty array `Accounts`, tambahkan cID + account itu ke app, kemudian benar-benar run dan backtest cBot di atasnya. **Hanya put demo account number di sini** — tidak pernah live account; test run/backtest tempatkan real order pada account apa pun Anda list. Empty/omitted `Accounts` ⇒ real run/backtest test skip cleanly.
- `OpenApi.Tokens` — multi-cID token cache (satu entry per authorized cID dengan refresh/access token + account list-nya). Ditulis otomatis oleh onboarding dan oleh token-refresh step; Anda jarang edit dengan tangan.
- `Owner` — seed owner login untuk app di bawah E2E.
- `Database.ConnectionString` — hanya saat arahkan test ke external Postgres daripada Testcontainers.
- `Ai.ApiKey` — Anthropic API key untuk fitur AI.

## Precedence

1. **Environment variable** override semuanya (mis. `App__OwnerPassword`, `App:Ai:ApiKey`).
2. **`secrets/dev-credentials.local.json`** — unified file (preferred).
3. **Legacy split file** — `openapi-test-app.local.json`, `openapi-cids.local.json`, `openapi-tokens.local.json` masih dibaca saat unified file absent, jadi existing machine tetap bekerja. New setup harus gunakan single file.

## Safety

- `secrets/` dan `*.local.json` adalah gitignored — tidak ada di sini yang pernah committed.
- Live copy test refuse untuk jalankan terhadap non-demo account (`IsLive` account difilter oleh `LiveCopyFixture`). Simpan hanya demo account dalam token cache.
