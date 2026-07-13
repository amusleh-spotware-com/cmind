---
description: "Per-copy факты исполнения — задержка, реализованное проскальзывание, fill vs failure — захватываются при каждой попытке копирования, отображаются как per-profile transparency report. Off по умолчанию…"
---

# Copy execution transparency (Фаза 3)

Per-copy факты исполнения — задержка, реализованное проскальзывание, fill vs failure — захватываются при каждой попытке копирования,
отображаются как per-profile transparency report. **Off по умолчанию**; включить с
`App:Copy:TransparencyEnabled=true`. Когда выключено, copy-движок байт-в-байт не изменён: хост пишет
в no-op sink, ничего не записывается.

## Как это работает

```
CopyEngineHost ──Record(fact)──▶ ICopyEventSink
                                   │
             (transparency off) NullCopyEventSink   → отбрасывает (по умолчанию; нулевая стоимость hot-path)
             (transparency on)  ChannelCopyEventSink → bounded in-memory channel (DropOldest)
                                   │
                                   ▼
                          CopyExecutionDrainer (BackgroundService)
                                   │  батчит каждые App drain interval
                                   ▼
                          CopyExecution append-only table  ◀── GET /api/copy/profiles/{id}/transparency
```

- **Hot path свободен от I/O.** Хост вызывает `ICopyEventSink.Record(...)` — неблокирующий,
  никогда не выбрасывает, enqueue. Никогда не await'ит, не трогает БД, не блокирует исполнение ордера.
- **Потеря предпочтительна над back-pressure.** Channel ограничен (`CopyExecutionChannelCapacity`) с
  `DropOldest`: если DB drainer тормозит, *старейшие* строки прозрачности дропаются, а не задерживают
  копирование. Прозрачность = best-effort телеметрия, не торговая зависимость.
- **Out-of-band persistence.** `CopyExecutionDrainer` вычищает канал батчами
  (`CopyExecutionDrainBatchSize`) каждые `CopyExecutionDrainInterval`, пишет строки `CopyExecution` через
  scoped `DataContext`. Финальный flush при shutdown.
- **Факты, не команды.** `CopyExecution` = append-only лог (как `InstanceLog`/`AuditLog`), не
  агрегат. Read model читает напрямую (CQRS-lite), агрегаты в памяти.

## Что записывается

Один `CopyExecutionRecord` на попытку копирования на одном назначении:

| Kind | Когда | Несёт |
|------|------|-------|
| `Opened` | copy-ордер размещён | symbol, side, wire volume, master price, realized slippage (points), latency (ms) |
| `Failed` | copy open выбросил/отклонён | symbol, side, master volume/price, latency, failure reason (тип исключения) |

(`Closed`/`Skipped`/`Reconciled` существуют в enum для будущего расширения.)

## Отчёт

`GET /api/copy/profiles/{id}/transparency` (owner-scoped) возвращает по последним 500 фактам:

- **Summary** — всего, opened, failed, **fill rate**, **средняя задержка (ms)**, **среднее проскальзывание (points)**.
- **Recent** — сырые недавние факты (назначение, позиция источника, symbol, side, volume, master price,
  slippage, latency, reason, timestamp).

## Конфигурация (`App:Copy`)

| Настройка | По умолчанию | Эффект |
|---------|---------|---------|
| `TransparencyEnabled` | `false` | Включить per-copy факт захват + drainer для узла. |

Ёмкость канала, размер батча дренажа, интервал дренажа = константы `CopyDefaults`
(`CopyExecutionChannelCapacity` / `CopyExecutionDrainBatchSize` / `CopyExecutionDrainInterval`).

## Тесты

- **Unit** (`CopyTransparencyTests`) — успешное открытие испускает факт `Opened` с правильным
  symbol/side/volume/latency; отклонённое открытие испускает факт `Failed` с причиной. Через capturing sink.
- **Integration** (`CopyExecutionDrainerTests`, реальный Postgres) — drainer персистит буферизованные факты в
  `CopyExecution` лог; пустой sink ничего не пишет.
- **DST** — хост меняет fire-and-forget с no-op default sink, поэтому детерминированный stress
  suite остаётся зелёным (23/23).
