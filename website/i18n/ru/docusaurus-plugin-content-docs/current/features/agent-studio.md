---
description: "Agent Studio — создание persona-driven, no-code trading агентов с характером и архетипом, управляющих счетами к вашим целям под Autonomy & Safety Kernel (risk envelope, circuit breaker, kill switch, versioned disclaimer consent)."
---

# Agent Studio

Agent Studio позволяет создать **trading-агента с характером** — без кода — и дать ему управление
вашими счетами к измеримым целям. Агент похож на personality-driven cBot: вы выбираете архетип
и аттитюд, устанавливаете гарды, и он работает под **Autonomy & Safety Kernel**.

Открыть **AI → Agent Studio** (`/agent-studio`).

## Создание агента

Диалог **New agent** собирает, без кода:

- **Name** и **archetype** — Scalper, Day Trader, Swing Trader, Position Trader, News Trader,
  Contrarian, Mean Reversion или Breakout/Momentum. Каждый пресет фиксирует разумный cadence и posture.
- **Attitude** — слайдеры aggressiveness, patience и trend-following.
- **Managed account(s)** — **требуется хотя бы один для создания агента** (агент без счёта никогда не сможет запуститься, поэтому *Create* остаётся отключён пока вы не выберете хотя бы один). Если вы ещё не привязали торговый счёт, диалог скажет об этом и подскажет сначала привязать его.
- **Уровень автономии** — **Advisory** (только предлагает) или **Approval-gated** (действует только после вашего
  per-action подтверждения). **Full Auto** (без per-trade подтверждения) дополнительно требует **risk envelope**
  и принятия disclaimer'а перед тем как armed.

Персона компилируется **детерминированно** в system prompt агента (не LLM его пишет), поэтому одна
и та же конфигурация всегда производит одинаковые инструкции — воспроизводимо и аудируемо.

## Ростер

Каждый агент показывается в таблице control-room: **какой агент, его тип, сколько счетов управляет,
его цели, статус запуска и последнее действие**, с контролами **Start / Stop / Kill**. Kill switch
немедленно останавливает работающего агента.

## Safety — доменный инвариант, не настройка

Всё, касающееся денег, маршрутизируется через **Autonomy & Safety Kernel**:

- **Risk envelope** — hard per-order лимиты (max daily loss, open exposure, position size, leverage,
  consecutive losses, orders/hour, allowed symbols). Каждый ордер валидируется против него перед dispatch;
  breach отклоняется, не зажимается. Требуется перед тем как агент может достичь Full Auto.
- **Circuit breaker** — детерминированно останавливает новый риск при серии убытков, breach daily-loss,
  **hard performance-goal breach** или **недоступности AI-провайдера** (модель down или галлюцинирует — не открывает новых позиций).
- **Versioned disclaimer consent** — одноразовое, версионированное принятие требуется для arm Full Auto
  (юридически обязательное согласие, не per-trade approval); bump disclaimer'а заставляет re-consent.
- **Kill switch** — идемпотентный emergency halt на каждом работающем агенте.

## Цели

Дайте агенту **измеримые цели** — например *держать max drawdown ниже 4%*, *profit factor хотя бы
1.5*, *win rate ≥ 55%*. Каждая цель **Hard** (гарда — breach trips circuit breaker) или
**Soft** (влияет на reasoning только), оценивается как On-track / At-risk / Breached.

## Pipeline принятия решений

После запуска агент работает в **24/7 supervised loop** (`AgentRuntimeService`). Каждый тик, для каждого
управляемого счёта: читает **детерминированное состояние счёта** (ground truth, никогда память модели);
запрашивает у движка решений ход; пропускает через **safety gate** (`AgentDecisionProcessor`) —
уровень автономии → circuit breaker → risk envelope; пишет append-only **`AgentDecisionRecord`**; и
останавливается или исполняет как gate направляет. Цикл **fault-isolated** (сбой одного агента никогда
не касается другого или хоста) и **safe by default**: он инертен если AI не настроен *и*
`App:Ai:AgentRuntimeEnabled` не установлен, и никогда не открывает новый риск пока AI провайдер недоступен.

- **Approval gate** — предложенный ордер approval-gated агента записывается как **Pending** и ничего
  не делает пока владелец не одобрит (`POST /api/agent-studio/{id}/decisions/{seq}/approve` или
  `/reject`); **Full Auto** проходит через envelope без per-trade approval; **Advisory** только предлагает.
- **Audit ledger** — каждое решение воспроизводимо: reasoning (XAI), доказательства которые цитировались,
  вердикт gate, order intent и исполнился ли, на `GET /api/agent-studio/{id}/decisions`.
- **Research desk** — по запросу multi-agent debate: Alpha/Sentiment/Technical/Risk аналитики дают
  каждый своё view и Reviewer синтезирует proposal (`POST /api/agent-studio/{id}/debate`).
- **Memory** — агент запоминает каждое решение и вспоминает recent memory в следующем prompt для
  continuity (`GET /api/agent-studio/{id}/memory`).

Каждая строка ростера **Details** открывает ленту решений агента (с Approve/Reject на pending orders),
его memory и вкладку Run-debate.

## Managed accounts and editing

При создании агента вы выбираете торговые счета, которыми он управляет — **требуется хотя бы один при создании** (кнопка *Create* отключена пока не выбран хотя бы один, и эндпоинт создания отклоняет пустой выбор). Каждого агента можно **редактировать** впоследствии (имя, темперамент, автономия и управляемые счета) из значка карандаша на строке ростера. Элементы управления жизненным циклом (детали, редактирование, запуск, остановка, kill) — это кнопки со значками, каждая отключена в состояниях, когда действие не применимо.

## Scope

Доставлено: полный lifecycle агента, детерминированный safety gate, 24/7 runtime,
human-in-the-loop approval gate, audit ledger и **живая интеграция cTrader Open API** — стор для
account-state (читает реальный balance, позиции и open exposure в lots) и order executor (размещает
реальные market orders, lots→volume через lot size символа), оба резолвят OAuth credentials каждого
управляемого счёта и gracefully деградируют когда счёт не привязан. **Требует Anthropic API key**
чтобы модель генерировала ордера (до тех пор движок держит); ещё предстоит: multi-agent debate roles
и layered memory/reflection. Runtime выключен если `App:Ai:AgentRuntimeEnabled` не установлен, поэтому
живая торговля происходит только при явном, полностью согласованном opt-in.
