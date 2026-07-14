---
description: "Сви акредитиви које пакети тестова требају живе у једној gitignored датотеци: secrets/dev-credentials.local.json. Копирајте посвећени шаблон и пуните оно што имате"
---

# Dev акредитиви — једна датотека за сваки тест

Сви акредитиви које пакети тестова требају живе у једној gitignored датотеци: `secrets/dev-credentials.local.json`. Копирајте посвећени шаблон и пуните оно што имате — свака вредност је опциона и тестови који требају недостајућу вредност прескачу чисто.

```bash
mkdir -p secrets
cp dev-credentials.example.json secrets/dev-credentials.local.json
# уредите secrets/dev-credentials.local.json
```

## Шта сваки нивој тестирања чита

| Нивој | Требује | Из |
|------|-------|------|
| **Unit** (`tests/UnitTests`) | ништа | — детерминистички, без тајни, без мреже |
| **Интеграција** (`tests/IntegrationTests`) | Postgres | Testcontainers (Docker) — аутоматски |
| **Live копија** (`tests/IntegrationTests/CopyLive`) | OpenAPI апликација + токен кеш | `OpenApi.App`, `OpenApi.Tokens` |
| **E2E onboarding** (`tests/E2ETests/CopyLive`) | OpenAPI апликација + cID пријаве | `OpenApi.App`, `OpenApi.Cids` |
| **E2E прави тек/backtest** (`CBotRealRunBacktestTests`) | cID пријава + **демо** број налога | `OpenApi.Cids[].{Username,Password,Accounts}` |
| **AI карактеристике** | Anthropic кључ | `Ai.ApiKey` (неподешено ⇒ AI карактеристике враће оневозмогућено, апликација јако ради) |
| **Live economic-calendar sources** (`tests/IntegrationTests/Calendar/CalendarSourceLiveTests`) | FRED / BLS API keys | `Calendar.FredApiKey`, `Calendar.BlsApiKey` (unset ⇒ that source's live test skips; the keyless central-bank schedule still works) |

## Схема

Видите `dev-credentials.example.json` на корену репо. Делови:

- `OpenApi.App` — `{ ClientId, ClientSecret }` cTrader Open API апликације.
- `OpenApi.Cids` — cTrader ID пријаве коришћене бездомнаком OAuth onboarding-у. Сваки унос такође носи **`Accounts`** низ — cTrader броје трговачког налога (пријава/број налога, нпр. `3635817`) под том cID-ом коју инфраструктура тестирања сме да повеже у апликацију и да вози. `CBotRealRunBacktestTests` чита први унос који има непразан `Accounts` низ, додаје ту cID + налог апликацији, затим заиста тече и backtest-uje cBot на њему. **Наведи само демо број налога овде** — никад налаз живог налога; покренућ/backtest тестови постављају реалне налоге на било који налог који наведеш. Празно/изостављено `Accounts` ⇒ реалан покренућ/backtest тест прескаче чисто.
- `OpenApi.Tokens` — multi-cID токен кеш (један унос по овлашћеној cID-и са њеним рефреш/приступ токеном + листом налога). Написано аутоматски од стране onboarding-а и од стране корака refresh-а токена; ретко га уредиш ручно.
- `Owner` — seed власник пријаву апликације под E2E.
- `Database.ConnectionString` — само када упућујете тестове на спољашњи Postgres уместо Testcontainers.
- `Ai.ApiKey` — Anthropic API кључ за AI карактеристике.
- `Calendar.FredApiKey` — [FRED](https://fredaccount.stlouisfed.org/apikeys) (St. Louis Fed) API key. The primary economic-calendar value source (interest rates, inflation, employment).
- `Calendar.BlsApiKey` — [BLS](https://data.bls.gov/registrationEngine/) (US Bureau of Labor Statistics) v2 registration key (CPI, PPI, employment, JOLTS). Absent ⇒ the low-quota public tier.

  Both feed the exact `FredSource`/`BlsSource` the ingestion worker uses. With a key present, `CalendarSourceLiveTests` hits the real provider and asserts observations come back; absent, that source's test skips cleanly. The app also reads these at runtime via `App:Calendar:FredApiKey` / `App:Calendar:BlsApiKey` (environment variables override — e.g. `FRED_API_KEY`, `BLS_API_KEY`).

## Редослед приоритета

1. **Варијабле окружења** надјачају све (нпр. `App__OwnerPassword`, `App:Ai:ApiKey`).
2. **`secrets/dev-credentials.local.json`** — уједињена датотека (преферирана).
3. **Наслеђене подељене датотеке** — `openapi-test-app.local.json`, `openapi-cids.local.json`, `openapi-tokens.local.json` су јако читане када недостаје уједињена датотека, тако да постојеће машине настављају да раде. Нова подешавања би требала да користе једну датотеку.

## Безбедност

- `secrets/` и `*.local.json` су gitignored — ништа овде никад није посвећено.
- Live копирајте тестове одбијају да се покрену против налога који нису демо (`IsLive` налози су филтрирани од `LiveCopyFixture`). Задржите само демо налоге у токен кешу.
- In-cluster (Kubernetes) извршавања монтирају датотеку као читљиву-само Secret; refresh-и токена се чувају у меморији и read-only write-back је тиха no-op.
