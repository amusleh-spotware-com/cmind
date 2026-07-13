---
description: "White-label deploy редко отправляют каждую возможность. Feature toggles позволяют operator отключить main функции продукта on/off — на deploy time через config, или позже на…"
---

# Feature toggles

White-label develop редко отправляют каждую возможность. Feature toggles позволяют operator отключить main функции продукта on/off — на deploy time через config, или позже на runtime, нет redeploy. **Все функции по умолчанию включены**; develop только перечисляет те это меняет.

## Модель

- `Core.Features.FeatureFlag` — enum gateable функций: `Authoring`, `Backtesting`, `Execution`, `CopyTrading`, `Ai`, `PortfolioAgent`, `Alerts`, `PropGuard`, `PropFirm`, `Accounts`, `OpenApi`, `Mcp`, `Compliance`. Core admin поверхности (dashboard, users, nodes, auth) никогда gateable, не здесь.
- `Core.Options.FeaturesOptions` — config baseline, bound из `App:Features`. Каждое свойство по умолчанию `true`.
- `Core.Features.IFeatureGate` — разрешает **effective** состояние: config baseline overlaid с опциональным owner-set runtime override. Реализовано `Infrastructure.Features.FeatureGate`, кеширует overrides briefly (`FeatureSettings.OverrideCacheTtl`), invalidates на изменение.

Runtime overrides хранятся как `AppSetting` rows keyed `feature.<FeatureFlag>` (value `true`/`false`). Нет row = "использовать config baseline".

## Два способа отключить функцию

### 1. Deployment конфигурация (baseline)

Установить flag `false` под `App:Features`. Пример `appsettings.json`:

```json
{
  "App": {
    "Features": {
      "CopyTrading": false,
      "PropGuard": false
    }
  }
}
```

Или через env vars (double underscore):

```
App__Features__CopyTrading=false
```

Baseline gates **startup регистрация** background workers (`Nodes.AddNodes`) и MCP инструментов (`Mcp` сервер), поэтому функция отключена в config никогда не запускает его hosted services ни expose его MCP инструменты.

### 2. Runtime override (owner)

Owner может flip любую функцию live из **Settings → Features** (`/settings/features`) или API:

```
GET  /api/features            -> [{ "flag": "CopyTrading", "enabled": true }, ...]   (Owner)
PUT  /api/features/{flag}      body { "enabled": false }  -> set override             (Owner)
PUT  /api/features/{flag}      body { "enabled": null  }  -> clear override (revert)  (Owner)
```

Runtime изменения вступают в силу немедленно для request-time gates (navigation, API). Background workers и MCP инструменты gated на startup, pick up runtime изменение на next процесс restart.

## Что каждый gate enforces

| Слой | Механизм | Timing |
|-------|-----------|--------|
| HTTP API | `RouteGroupBuilder.RequireFeature(flag)` endpoint filter → `404` когда отключен | Runtime |
| Navigation | `NavMenu` скрывает ссылки через `IFeatureGate.IsEnabled` | Runtime |
| Background workers | conditional `AddHostedService` в `Nodes.AddNodes` | Startup (config) |
| MCP инструменты | conditional `WithTools<>` в MCP сервере | Startup (config) |

Функция достигнута deep link пока отключена рендерит empty страница — его API возвращает `404`; nav больше не surfaces это.

## Flag → поверхность карта

| Flag | API группы | Nav | Workers / MCP |
|------|-----------|-----|----------------|
| Authoring | `/api/cbots`, `/api/paramsets`, `/api/builder` | cBots группа → cBots (param sets per-cBot dialog) | MCP `CBotTools` |
| Backtesting | (shares `/api/instances`) | cBots группа → Backtest | — |
| Execution | `/api/instances` | cBots группа → Run | MCP `InstanceTools` |
| CopyTrading | `/api/copy` | Copy Trading | `CopyEngineSupervisor`, `OpenApiTokenRefreshService`, MCP `CopyTools` |
| Ai | `/api/ai` | AI группа → AI; Settings → AI (key) | `AiRiskGuard`, MCP `AiTools` |
| PortfolioAgent | `/api/agent` | AI группа → Portfolio Agent | `PortfolioAgentService` |
| Alerts | `/api/alerts` | AI группа → Alerts | `AlertEvaluator` |
| PropGuard | `/api/prop` | Prop группа → Prop Guard | `PropGuardService` |
| PropFirm | `/api/prop-firm` | Prop группа → Challenges | — |
| Accounts | `/api/ctids` | Trading Accounts | — |
| OpenApi | `/api/openapi` | Settings → Open API | — |
| Mcp | `/api/mcp-keys` | AI группа → MCP Keys | — |
| Compliance | `/api/compliance` | Settings → Legal & Privacy | — |

## Тесты

- **Unit** — `UnitTests/Features/FeaturesOptionsTests.cs`: baseline по умолчанию, per-flag отображение.
- **Интеграция** — `IntegrationTests/FeatureGateTests.cs`: config baseline, runtime override beats config и persists как `AppSetting`, clearing reverts к baseline (real Postgres).
- **E2E** — `E2ETests/FeatureToggleTests.cs`: отключение `CopyTrading` на runtime скрывает его nav ссылку и `404`s `/api/copy`, re-enabling восстанавливает оба.
