---
description: "AI macro currency strength & forward outlook — AI-assisted, math-deterministic макро движок силы валют. Ранжирует валюты по текущей фундаментальной силе и проецирует forward directional outlook для каждой пары."
---

# AI macro currency strength & forward outlook

cMind поставляет **AI-assisted, math-deterministic** макро валютный движок силы. Он ранжирует
настраиваемую вселенную валют — 8 мажоров плюс emerging-market и exotic валюты — по
**текущей** фундаментальной силе и проецирует **forward directional outlook** для каждой пары на
выбранный горизонт (1M / 3M / 6M / 12M). Каждый ранг, каждое pair bias и каждое число вычисляются
чистой детерминированной математикой в доменном ядре; LLM только *собирает* forward-looking inputs,
которые данные не публикуют, и *объясняет* результат на простом английском. Он никогда не изобретает
ранг, направление или число.

> **Честное ограничение.** Фундаменталы хорошо предсказывают среднесрочную-до-долгосрочную стоимость и
> плохо — краткосрочную. Рассматривайте это как фильтр позиционирования / confluence, **не**
> как краткосрочный timing сигнал. Показания вблизи high-impact релизов (NFP/CPI/центральный банк) шумные.
> Не финансовая рекомендация.

## Как это работает

1. **Текущие фундаменталы из Economic Calendar, не из LLM.** Жёсткие числа — policy
   rates, CPI vs target, GDP, employment, trade balance — и их **surprise z-scores** sourcing'ся
   **point-in-time** из [economic calendar](./economic-calendar.md) модуля (FRED/BLS/BEA/ECB и
   central-bank calendars). Исторический snapshot никогда не leaks look-ahead.
2. **LLM собирает только то, что календарь не может опубликовать** — per валюту: **forward** trajectory
   (ожидаемый path ставки в bp, inflation-trend-vs-target, growth momentum) и **geopolitical**
   outlook (risk-on/off, tariffs, fiscal/debt, elections), плюс любые EM/exotic текущие фигуры,
   которых нет в календаре. Strict JSON, tier-aware validation, web search on.
3. **Домен детерминированно вычисляет ранжирование и forward matrix.** Каждый драйвер оценивается
   как **within-tier z-score** (чтобы 50%-inflation exotic никогда не искажал мажоры), winsorized,
   weight-summed в композит и ранжирован strongest→weakest со стабильным ISO tie-break. Forward layer
   несёт каждый композит по его trajectory — и маппит каждый pair's projected differential к **directional bias**
   (▲ appreciate / ▬ neutral / ▼ depreciate) с conviction.
4. **LLM объясняет** ранжирование и top pair calls на простом языке.

## Драйверы

| Драйвер | Влияние на силу | Заметки |
|---|---|---|
| Policy rate & trajectory | Выше / hawkish ⇒ сильнее | Highest weight; central-bank divergence drives biggest gaps. |
| Inflation (CPI vs target) | Above target ⇒ слабее | Scored inversely (purchasing-power drag). |
| GDP growth | Выше относительный рост ⇒ сильнее | Differential vs panel. |
| Employment | Stronger labour ⇒ сильнее | Feeds policy path. |
| Trade balance / current account | Surplus ⇒ сильнее | Structural demand. |
| Policy stance | Hawkish ⇒ сильнее | Primary long-term driver. |
| Surprise momentum | Recent beats ⇒ сильнее | From calendar's surprise z-scores. |
| Geopolitical / risk | Risk-off ⇒ safe havens (USD/JPY/CHF) сильнее | Bounded forward risk delta. |
| Real yield / carry *(EM/exotic)* | Positive real rate ⇒ сильнее | Dominant EM driver in calm regimes. |
| External vulnerability *(EM/exotic)* | Deficits / low reserves / USD debt ⇒ слабее | Structural depreciation pressure. |
| Terms of trade *(commodity exporters)* | Rising export prices ⇒ сильнее | BRL, ZAR, CLP, NOK, AUD, CAD. |
| Political / institutional risk *(EM/exotic)* | Instability ⇒ слабее | Wider dead-band, capped conviction. |

## Tiered universe (мажоры + EM + exotics)

Вселенная **настраивается через deployment config** (`App:CurrencyStrength:Universe`) — добавление валюты это
конфиг, не код. Каждая валюта несёт **tier** (`Major` / `EmergingMarket` / `Exotic`), который tuned
weighting, dead-band width и conviction cap:

- **Мажоры** — USD, EUR, GBP, JPY, AUD, NZD, CAD, CHF (led rate-level).
- **Emerging markets** — CNH, INR, BRL, MXN, ZAR, KRW, SGD, PLN (+ Scandi NOK/SEK); carry + risk +
  external-vulnerability weighted up, medium confidence.
- **Exotics** — TRY, HUF, CZK, плюс USD-pegged HKD/SAR; low confidence, wider dead-band, capped
  conviction. **Pegged / heavily-managed** валюты (HKD, SAR, CNH) flagged, their trajectory
  down-weighted, и their pair outlook зажат toward `Neutral`.

Поскольку official EM/exotic статистика lower-frequency, revised и иногда opaque, AI-gathered
фигуры несут **per-tier confidence** показанный как reliability badge.

## Graceful degradation

| Calendar | AI | Результат |
|---|---|---|
| ✅ | ✅ | Full ranking + forward projection + narrative (`CalendarAndAi`). |
| ✅ | ❌ | Calendar-only current ranking, no forward projection (`CalendarOnly`). |
| ❌ | ✅ | AI-gathered current figures + forward, lower confidence (`AiOnly`). |
| ❌ | ❌ | No snapshot — widget скрыт и страница показывает empty state. |

Приложение работает без изменений. AI gated на AI key; calendar leg уважает свой white-label gate + runtime toggle.

## Использование

- **Включите AI** (Settings → AI) и **включите виджет** из диалога **Customize** своего dashboard
  ("Currency strength" — opt-in, скрыт по умолчанию). Виджет показывает top сильные/слабые валюты и
  top 3M pair call; ссылается на полную страницу.
- **Полная страница** — `/ai/currency-strength`: horizon selector (1M/3M/6M/12M), tier filter
  (All/Majors/EM/Exotics), current ranking, forward forecast, pair-outlook matrix (bias +
  conviction, pegged/low-confidence flagged), и AI narrative. Нажмите **Refresh now** (owner)
  для регенерации. Background worker (`App:CurrencyStrength:RefreshEnabled`, **default `true`**) refreshes
  по schedule so the page populated out of the box.

## Программный доступ

Один shared read model (`ICurrencyStrengthQuery`) достижим тремя путями:

- **In-app AI** — инъектируется напрямую в AI функции.
- **MCP** — `currency_strength` tool (params `horizon`, `tier`) для AI клиентов/агентов.
- **cBot REST** — `GET /api/market/v1/currency-strength/{latest,history,pair/{base}/{quote}}`, secured
  тем же `CalendarJwt` machinery как [calendar cBot API](./calendar-cbot-api.md) с добавленным
  **`market:read`** scope. cBot регистрирует API клиент с `market:read`, обменивает id + secret на
  short-lived JWT at `POST /api/calendar/v1/token`, и вызывает эндпоинты с `Bearer` token.

См. [calendar cBot API](./calendar-cbot-api.md) для token flow и sample.
