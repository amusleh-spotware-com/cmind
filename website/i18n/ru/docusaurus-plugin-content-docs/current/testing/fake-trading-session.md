---
description: "tests/UnitTests/CopyTrading/FakeTradingSession.cs = in-memory IOpenApiTradingSession все copy-trading unit тесты запускаются против. Работа: имитировать real cTrader Open…"
---

# FakeTradingSession — cTrader Open API фидилити контракт

`tests/UnitTests/CopyTrading/FakeTradingSession.cs` = in-memory `IOpenApiTradingSession` все copy-trading unit тесты запускаются против. Работа: имитировать **real cTrader Open API сервер** достаточно близко, что unit тесты покрывают поведение только live tier использовалось для ловить. Этот doc = фидилити контракт: что fake моделирует, как верно, и правило сохранения это честным.

> **Binding правило (CLAUDE.md):** fake остается cTrader-faithful. **Extend это, никогда не ослабляй это** пройти тест. Каждое новое real поведение вы полагаетесь на моделирует здесь, pinned by фидилити тест.

## Фидилити матрица (F1–F13)

Tracks план `plans/copy-trading-overhaul.md` §7.6. Легенда: ✅ моделирован · ◑ partial (opt-in / extending) · ⬜ еще не моделирован.

| # | Real Open API поведение | Fake статус | Как это моделировано |
|---|------------------------|-------------|-------------------|
| F1 | Market order может **partial-fill** | ◑ | `PartialFillFractionForCtid[ctid] = f` заполняет только `f×volume`; reconcile затем показывает gap Phase‑1 true‑up (G5) закрывает. Accept→fill event пара еще приходить. |
| F2 | Volume нормализован в **step**, отклонены ниже **min** / выше **max** | ✅ | `VolumeBoundsForCtid[ctid] = (Step, Min, Max)` раундует вниз в step, выбрасывает `CtraderRejectException(VolumeTooLow/High)`. |
| F3 | **Invalid SL/TP** отклонены (side + digits) | ⬜ | Плано Phase 0a/1 (pairs с M6 SL/TP precision нормализация). |
| F4 | Цены **integer-scaled by digits**; `pipPosition` | ◑ | `SymbolDetails` теперь несет `Digits` (и `MaxVolume`), заполнены из real symbol; `PipPosition` управляет market-range толерантностью, `Digits` управляет SL/TP precision нормализацией (M6). Полный integer цена scaling все еще pending. |
| F5 | **Market-range** заполняет только если spot внутри `base ± slippage`, иначе отклоняет | ✅ | `IsMarketRangeRejected` сравнивает live spot (`SetSpot`) к `baseSlippagePrice ± slippageInPoints`. Legacy `RejectMarketRangeForCtid` flag все еще принудительно отклоняет. |
| F6 | **Pending trigger→fill** dual event (Order несет `positionId` + OPEN Position) | ◑ | `PushOpen(..., orderId:)` воспроизводит filled-pending event; FX‑Blue/cMAM double-copy dedupe покрыто в `CopyEngineHostTests.Filled_pending_does_not_double_open`. |
| F7 | **Server-driven закрывает** (SL/TP hit, stop-out) | ⬜ | Сегодня закрывает test-pushed (`PushClose`); price-driven SL/TP-hit + stop-out закрывает плано. |
| F8 | **Per-account** symbol таблицы / детали | ◑ | Symbol имена/ids per-fake; per-account расходящиеся таблицы (cross-broker) pending. |
| F9 | Полный **account состояние** (balance, equity, margin, freeMargin) | ◑ | `Balance` + `LoadPositionValuationsAsync` (entry/swap/commission через `SetPositionValuation`) + `SetSpot` feed real equity в proportional-equity sizing (G2, unit-tested в `CopyEquitySizingTests`). Используемый margin не exposed через reconcile API, поэтому free-margin reported как equity. |
| F10 | Events несут **server timestamps** | ✅ | `ExecutionEvent.ServerTimestamp` (unix ms) — real session читает из deal's `ExecutionTimestamp`; `PushOpen`/`PushPending` accept `serverTimestamp:` поэтому `FakeTimeProvider`-driven тест drives real copy latency (G1). |
| F11 | **Trading режим / расписание** (disabled / close-only / closed) | ⬜ | Плано Phase 2b. |
| F12 | **Typed ошибка taxonomy** (`ProtoOAErrorRes` коды) | ✅ | `RejectReasonForCtid[ctid] = CtraderRejectReason.X` выбрасывает one-shot `CtraderRejectException(reason)` (NotEnoughMoney, MarketClosed, PositionNotFound, …). |
| F13 | **Token инвалидация** — stale token → auth ошибка | ✅ | `InvalidateToken(ctid)` отмечает attached token stale; trading вызывает выбрасывают **real** `OpenApiException` с `OpenApiErrorKind.TokenInvalid` (код `CH_ACCESS_TOKEN_INVALID`), ровно как live сервер, пока `SwapAccessTokenAsync` устанавливает свежий token. Feeds M1 token-robustness тест. |

Фидилити тесты живут в `tests/UnitTests/CopyTrading/FakeTradingSessionFidelityTests.cs`.

## Opt-in, defaults сохраняют legacy поведение

Каждый фидилити knob **off по умолчанию** так fake сохраняет простой always-fill поведение для тестов, которые не заботаются. Тест opt in per учетная запись:

```csharp
session.VolumeBoundsForCtid[slave]        = (Step: 10, Min: 10, Max: 1000); // F2
session.PartialFillFractionForCtid[slave] = 0.6;                            // F1 / G5
session.RejectReasonForCtid[slave]        = CtraderRejectReason.NotEnoughMoney; // F12 (one-shot)
session.InvalidateToken(slave);                                             // F13
```

## Characterization + conformance (плано, сохраняет fake ≡ real)

Два механизма сохраняют fake честным против moving real сервер (tracked, приземление через Phase 0a):

1. **Live characterization** (`LiveApiCharacterization`, demo учетные записи, secrets-gated, `Inconclusive` на closed market): drive real Open API, record точно wire истина (event последовательности, масштабирование, отклонить коды) в golden fixtures checked into тест проект. Нет secrets в fixtures — только observed формы.
2. **Conformance harness**: запускайте *тот же* сценарий suite дважды — один раз против `FakeTradingSession`, один раз против live session (когда secrets present) — assert идентичные observable исходы. Real сервер изменения → live leg fails → update fake. Это делает «unit тесты покрывают все» trustworthy.

Live учетные данные: `secrets/dev-credentials.local.json` (или legacy split файлы) — смотрите `docs/testing/dev-credentials.md`.
