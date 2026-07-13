---
description: "Performance fees управляющего по high-water-mark, стандартная модель copy-trading (cTrader Copy, Darwinex, ZuluTrade profit-share): провайдер берёт процент от новой прибыли сверх пика капитала подписчика."
---

# Copy performance fees (Фаза 4)

Performance fees управляющего по **high-water-mark**, стандартная модель copy-trading (cTrader Copy,
Darwinex, ZuluTrade profit-share): провайдер берёт процент от *новой* прибыли сверх пика капитала
каждого подписчика — никогда с opening balance и никогда дважды за одну и ту же восстановленную прибыль. **Opt-in** через
`App:Copy:FeesEnabled` (выключено по умолчанию).

## Модель (high-water-mark)

Per назначение (фолловер-счёт), при каждом расчёте:

1. **Первый расчёт** засеивает high-water-mark (HWM) на текущий equity → без комиссии (фолловеру
   никогда не выставляется счёт с его депозита).
2. **Новый максимум** (equity > HWM): `fee = performanceFeePercent × (equity − HWM)`, затем `HWM ← equity`.
3. **На уровне или ниже пика**: комиссия не взимается, HWM не меняется — фолловер должен сначала восстановиться выше старого пика, поэтому
   с него никогда не берётся дважды за одни и те же накопленные прибыли.

Арифметика комиссии — это доменный инвариант на `CopyDestination.SettleFee(equity)` — агрегат им владеет; сервис
расчётов только поставляет опрошенный equity и записывает возвращённую сумму. `PerformanceFee` — объект-значение,
ограниченный 50%, поэтому ошибка конфигурации не может списать весь выигрыш фолловера.

## Как рассчитывается

```
CopyFeeSettlementService (BackgroundService, только когда FeesEnabled)
   │  каждые App:Copy:FeeSettlementInterval
   ├─ загрузить работающие профили с настроенным назначением комиссии
   ├─ ICopyEquityReader.ReadEquityAsync(ctid)   ← OpenApiCopyEquityReader открывает сессию,
   │                                               вычисляет balance + floating P&L (PropFirmEquityCalculator)
   ├─ destination.SettleFee(equity)             ← логика HWM на агрегате
   └─ персистим advance'нутый HWM + append CopyFeeAccrual (только при новом максимуме)
```

- `ICopyEquityReader` — Core-абстракция; живая реализация (`OpenApiCopyEquityReader`) — единственный
  инфраструктурный кусок — поэтому расчёт + логика HWM упражняются в тестах с фейковым reader, без живого брокера.
- `CopyFeeAccrual` — append-only лог (HWM-до, equity, fee %, fee amount, settled-at) — лог фактов для
  отчёта о комиссиях, не агрегат.

## Конфигурация & API

| Настройка `App:Copy` | По умолчанию | Эффект |
|--------------------|---------|---------|
| `FeesEnabled` | `false` | Запустить сервис расчётов. |
| `FeeSettlementInterval` | `1h` | Как часто опрашивается equity и рассчитываются комиссии. |

Per-назначение: `PerformanceFeePercent` (0–50) задаётся на назначении (добавить/редактировать назначение).

- `GET /api/copy/profiles/{id}/fees` — начисления комиссий профиля + всего собрано.

## Тесты

- **Unit** (`CopyPerformanceFeeTests`) — HWM-инвариант: первый расчёт засеивает + не берёт; новый
  максимум берёт только прирост выше пика; на/ниже пика не берёт и пик никогда не отступает;
  после просадки только восстановление выше старого пика облагается; 0% никогда не берёт; VO
  отклоняет невалидные проценты.
- **Integration** (`CopyFeeSettlementTests`, реальный Postgres, fake equity reader) — seed→10k (без комиссии, mark
  засеян), 12k (берёт 400, mark продвигается), 11k (без комиссии, mark держится); accrual персистится с правильным
  owner/amount.

Copy-хост не затронут комиссиями (расчёт — отдельное DB-задание), поэтому copy DST stress suite
не затронута (23/23).
