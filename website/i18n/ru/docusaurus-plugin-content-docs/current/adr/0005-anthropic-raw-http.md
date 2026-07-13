---
title: 0005 — Клиент AI использует сырой HTTP, не Anthropic SDK
description: Почему IAiClient вызывает Anthropic API над typed HttpClient вместо официального SDK, и почему AI полностью гейтирован на ключе.
---

# 0005 — Клиент AI использует сырой HTTP, не Anthropic SDK

## Контекст

Каждая AI функция (generation стратегии, self-repair, risk guard, post-mortems) вызывает Anthropic API. Зависимость SDK добавляет переходную поверхность, которую мы не контролируем, связывает наш release cadence к их, и скрывает точной wire контракт, который нам нужен для рассуждения о устойчивости и стоимости.

## Решение

`IAiClient` вызывает Anthropic над **сырым HTTP** через typed `HttpClient` — намеренно **не** SDK. `AiFeatureService` является единственным оркестратором, разделенным Web endpoints, MCP `AiTools` и `AiRiskGuard`. Вся поверхность **гейтирована на `AppOptions.Ai.ApiKey`**: без ключа, каждая функция возвращает `AiResult.Fail` и приложение работает без изменений.

## Последствия

- Нет ключа требуется для построения, тестирования или E2E — CI и локальный dev запускают полное приложение без AI.
- Мы владеем request/response формой, retry/timeout политикой и token accounting явно.
- Новые Anthropic функции должны быть подключены вручную; мы торгуем удобством для контроля и меньшей поверхности зависимости. Смотрите `claude-api` ссылка для текущих model ids и параметров.
