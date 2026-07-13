---
description: "Браузный каталог copy-стратегий. Провайдер публикует копи-профиль как объявление с бейджем verified-live (стратегия торгует реальные деньги, не демо) плюс комиссия за результат."
---

# Copy provider marketplace (Фаза 4)

Браузный каталог copy-стратегий. Провайдер **публикует** копи-профиль как объявление с **бейджем verified-live** (стратегия торгует реальные деньги, не демо) плюс комиссия за результат. Подписчики просматривают marketplace, ранжированные по оценке производительности, рассчитанной из данных execution-transparency.

## Модель

- `CopyProviderListing` = агрегат: `UserId`, `ProfileId`, display name, description, комиссия, `VerifiedLive`, `Published` + `PublishedAt`. Одно объявление на профиль (уникальный индекс).
- **Verified-live** определяется при публикации из `TradingAccount.IsLive` исходного счёта — провайдер не может сам себя аттестовать.
- Статистика производительности **не хранится на объявлении** — проекция поверх лога `CopyExecution` transparency (fill rate, avg latency, avg realized slippage), поэтому marketplace всегда отражает живое качество исполнения.

## Ранжирование

`CopyEndpoints.MarketplaceScore(fillRate, avgLatencyMs, avgSlippagePoints, verifiedLive)` → оценка 0–100: fill rate доминирует (×60), низкая задержка + низкое проскальзывание добавляют (×20 каждая), verified-live бейдж добавляет небольшой trust bonus. Детерминированное + монотонное, поэтому порядок стабилен.

## API

- `POST /api/copy/profiles/{id}/publish` — опубликовать/обновить объявление (`DisplayName`, `Description`, `PerformanceFeePercent`); verified-live устанавливается из исходного счёта.
- `DELETE /api/copy/profiles/{id}/publish` — снять с публикации.
- `GET /api/copy/marketplace` — все опубликованные объявления, ранжированные, каждое с итогом производительности (executions, fill rate, avg latency, avg slippage, score) + бейджем verified-live.

## Тесты

- **Unit** (`CopyProviderListingTests`) — инварианты агрегата: display name обязателен; publish ставит timestamp; unpublish скрывает; update заменяет поля + fee + badge.
- **Integration** (`CopyMarketplaceTests`, реальный Postgres) — опубликованное объявление персистится с badge; одно объявление на профиль (уникальный индекс); ranking score предпочитает verified/high-fill провайдеров.

Copy-хост не затронут (листинги + read model только), поэтому copy DST stress suite не затронута.
