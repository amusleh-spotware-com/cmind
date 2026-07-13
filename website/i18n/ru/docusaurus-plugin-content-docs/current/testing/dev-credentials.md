---
description: "Все учетные данные которые test suites требуют жить в единственном gitignored файле: secrets/dev-credentials.local.json. Скопируйте committed template и заполните что вы"
---

# Dev учетные данные — один файл для каждого теста

Все учетные данные которые test suites требуют жить в единственном gitignored файле:
`secrets/dev-credentials.local.json`. Скопируйте committed template и заполните что вы имеете — каждое значение опциональное и тесты которые требуют отсутствующего значения skip cleanly.

```bash
mkdir -p secrets
cp dev-credentials.example.json secrets/dev-credentials.local.json
# edit secrets/dev-credentials.local.json
```

## Что каждый test tier читает

| Tier | Требует | Из |
|------|-------|------|
| **Unit** (`tests/UnitTests`) | ничего | — детерминированный, нет secrets, нет network |
| **Интеграция** (`tests/IntegrationTests`) | Postgres | Testcontainers (Docker) — auto |
| **Live copy** (`tests/IntegrationTests/CopyLive`) | OpenAPI app + token cache | `OpenApi.App`, `OpenApi.Tokens` |
| **E2E onboarding** (`tests/E2ETests/CopyLive`) | OpenAPI app + cID logins | `OpenApi.App`, `OpenApi.Cids` |
| **E2E реальный run/backtest** (`CBotRealRunBacktestTests`) | cID login + **demo** account номер | `OpenApi.Cids[].{Username,Password,Accounts}` |
| **AI функции** | Anthropic key | `Ai.ApiKey` (unset ⇒ AI функции return disabled, приложение все еще запускается) |

## Schema

Смотрите `dev-credentials.example.json` на repo root. Секции:

- `OpenApi.App` — `{ ClientId, ClientSecret }` из cTrader Open API приложения.
- `OpenApi.Cids` — cTrader ID logins используемые headless OAuth onboarding. Каждый entry также несет **`Accounts`** array — cTrader trading-account номеры (login/account номер, например `3635817`) под тем cID это test инфраструктура разрешена link в приложение и drive. `CBotRealRunBacktestTests` читает первый entry что имеет non-empty `Accounts` array, добавляет то cID + account в приложение, затем реально запускает и бэктестирует cBot на это. **Ставьте только demo account номеры здесь** — никогда live account; run/backtest тесты place реальные orders на что бы account вы список. Empty/omitted `Accounts` ⇒ реальный run/backtest тест skips cleanly.
- `OpenApi.Tokens` — multi-cID token cache (один entry per authorized cID с его refresh/access token + account list). Написано автоматически onboarding и token-refresh шагом; вы редко edit это вручную.
- `Owner` — seed owner login для приложения под E2E.
- `Database.ConnectionString` — только когда pointing тесты на external Postgres вместо Testcontainers.
- `Ai.ApiKey` — Anthropic API key для AI функций.

## Precedence

1. **Environment переменные** override все (например `App__OwnerPassword`, `App:Ai:ApiKey`).
2. **`secrets/dev-credentials.local.json`** — unified файл (preferred).
3. **Legacy split файлы** — `openapi-test-app.local.json`, `openapi-cids.local.json`, `openapi-tokens.local.json` все еще читаются когда unified файл absent, поэтому существующие машины сохраняют работу. Новые setups должны использовать single файл.

## Safety

- `secrets/` и `*.local.json` gitignored — ничего здесь никогда не committed.
- Live copy тесты refuse запустить против non-demo accounts (`IsLive` accounts отфильтрованы `LiveCopyFixture`). Сохраняйте только demo accounts в token cache.
- In-cluster (Kubernetes) запускает mount файл как read-only Secret; token refreshes сохраняются в памяти и read-only write-back это silent no-op.
