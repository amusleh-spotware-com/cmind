---
description: "AI helper. Рекомендует безопасные настройки copy-trading назначения из профиля риска подписчика и описания исходного (мастер) счёта. Доступен через REST API, MCP…"
---

# AI copy-profile recommender

AI helper. Рекомендует безопасные настройки copy-trading назначения из профиля риска подписчика и описания исходного (мастер) счёта. Доступен через REST API, MCP tool, страницу Copy Trading. Только рекомендация — никогда не создаёт/не изменяет профиль; человек (или последующий MCP-вызов) применяет настройки.

## Модель

- `IAiFeatureService.RecommendCopyProfileAsync(riskProfile, sourceDescription, ct)` — строит запрос из
  промпта `AiPrompts.CopyProfileSystem`, возвращает `AiResult`, текст которого = JSON-объект рекомендуемых
  настроек: `riskMode` (название `MoneyManagementMode`), `riskParameter`, `maxDrawdownPercent`, `dailyLossLimit`,
  `direction`, `copyStopLoss`, `copyTakeProfit`, `slippagePips`, короткое `rationale`.
- Как и любая AI-функция, управляется `App:Ai:ApiKey`: без ключа → вызов возвращает
  `AiResult.Fail(disabled)`, приложение не затронуто.

## Поверхности

| Поверхность | Вход |
|---------|-------|
| REST | `POST /api/ai/recommend-copy-profile` `{ riskProfile, sourceDescription }` → `AiResult` (фича `Ai`, роль User+) |
| MCP | `CopyTools.RecommendCopyProfile(riskProfile, sourceDescription)` (фича `CopyTrading`, делегирует в AI-сервис) |
| UI | Страница Copy Trading → кнопка **AI suggest**; рекомендация отображается в inline-алерте |

Рекомендация не применяется автоматически намеренно: подписчик просматривает, затем создаёт профиль /
назначение через обычный диалог Copy Trading (или MCP-клиент парсит JSON + вызывает эндпоинты создания).

## Тесты

- **Unit** — `UnitTests/Ai/AiFeatureServiceRecommendTests.cs`: профиль риска + описание источника
  передаются AI-клиенту под промптом системы копирования (NSubstitute).
- **Integration** — `IntegrationTests/AiRecommendDisabledTests.cs`: без API-ключа → реальный
  `AnthropicAiClient` + `AiFeatureService` деградируют до результата с ошибкой (приложение работает без ключа).
- **E2E** — `E2ETests/AiCopyRecommendTests.cs`: кнопка **AI suggest** вызывает эндпоинт + рендерит
  результат (graceful "not configured" в тестовом окружении), доказывая путь UI → эндпоинт → AI.
