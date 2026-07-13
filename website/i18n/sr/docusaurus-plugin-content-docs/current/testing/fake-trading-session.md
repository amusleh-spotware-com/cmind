---
description: "tests/UnitTests/CopyTrading/FakeTradingSession.cs = in-memory IOpenApiTradingSession сви copy-trading unit тестови раде против. Посао: имитирати прави cTrader Open…"
---

# FakeTradingSession — cTrader Open API fidelity уговор

`tests/UnitTests/CopyTrading/FakeTradingSession.cs` = in-memory `IOpenApiTradingSession` сви copy-trading unit тестови раде против. Посао: имитирати **прави cTrader Open API сервер** довољно близу да unit тестови покрију понашање само live нивој кориштен да ухвати. Овај док = fidelity уговор: шта fake моделира, како верно, и правило које га чува честитим.

> **Обавезујуће правило (CLAUDE.md):** fake остаје cTrader-верна. **Проширити је, никада је ослабити** да прође тест. Сваком новом правом понашању на која се ослањаш моделира се овде, прихваћена fidelity тестом.

## Fidelity матрица (F1–F13)

Прати план `plans/copy-trading-overhaul.md` §7.6. Легенда: ✅ моделирано · ◑ делимично (opt-in / проширење) · ⬜ још није моделирано.

| # | Прави Open API понашање | Fake статус | Како је моделирано |
|---|------------------------|-------------|-------------------|
| F1 | Market налог може **partial-fill** | ◑ | `PartialFillFractionForCtid[ctid] = f` пуни само `f×volume`; умирити затим показује јаз Phase‑1 true‑up (G5) затворе. Accept→fill event пара још доћи. |
| F2 | Volume нормализовано на **step**, одбијено испод **min** / изнад **max** | ✅ | `VolumeBoundsForCtid[ctid] = (Step, Min, Max)` округле доле к step, хвата `CtraderRejectException(VolumeTooLow/High)`. |
| F3 | **Invalid SL/TP** одбијено (side + digits) | ⬜ | Планирано Phase 0a/1 (спарено са M6 SL/TP precision нормализација). |
| F4 | Цене **integer-scaled by digits**; `pipPosition` | ◑ | `SymbolDetails` сада носи `Digits` (и `MaxVolume`), унапрвађено од правог симбола; `PipPosition` управља market-range толеранцијом, `Digits` управља SL/TP precision нормализацијом (M6). Пуна integer цена скалирање још чека. |
| F5 | **Market-range** пуни само ако spot унутар `base ± slippage`, иначе одбија | ✅ | `IsMarketRangeRejected` упоређује live spot (`SetSpot`) са `baseSlippagePrice ± slippageInPoints`. Наслеђе `RejectMarketRangeForCtid` флаг још увек силно одбија. |
| F6 | **Pending trigger→fill** dual event (Order носи `positionId` + OPEN Position) | ◑ | `PushOpen(..., orderId:)` репродукује filled-pending event; FX‑Blue/cMAM double-copy dedupe покривено у `CopyEngineHostTests.Filled_pending_does_not_double_open`. |
| F7 | **Server-driven closes** (SL/TP hit, stop-out) | ⬜ | Данас затворе тест-пушу (`PushClose`); price-driven SL/TP-hit + stop-out затворе планирано. |
| F8 | **Per-account** symbol столови / детаљи | ◑ | Symbol имена/ids per-fake; per-account дивергентан столови (cross-broker) чека. |
| F9 | Пуна **account стање** (balance, equity, margin, freeMargin) | ◑ | `Balance` + `LoadPositionValuationsAsync` (entry/swap/commission via `SetPositionValuation`) + `SetSpot` хране правој equity у proportional-equity sizing (G2, unit-tested у `CopyEquitySizingTests`). Коришћена margin није изложена од reconcile API, тако да free-margin пријављена као equity. |
| F10 | События носе **server timestamps** | ✅ | `ExecutionEvent.ServerTimestamp` (unix ms) — права сесија читања од deal-а `ExecutionTimestamp`; `PushOpen`/`PushPending` прихватити `serverTimestamp:` тако да `FakeTimeProvider`-driven тест вози прави copy кашњење (G1). |
| F11 | **Trading режим / распоред** (disabled / close-only / closed) | ⬜ | Планирано Phase 2b. |
| F12 | **Типизирана error таксономија** (`ProtoOAErrorRes` кодови) | ✅ | `RejectReasonForCtid[ctid] = CtraderRejectReason.X` хвата one-shot `CtraderRejectException(reason)` (NotEnoughMoney, MarketClosed, PositionNotFound, …). |
| F13 | **Token invalidation** — stale token → auth грешка | ✅ | `InvalidateToken(ctid)` означи прилози token stale; трговање позиви хвате **прави** `OpenApiException` са `OpenApiErrorKind.TokenInvalid` (код `CH_ACCESS_TOKEN_INVALID`), баш као live сервер, све док `SwapAccessTokenAsync` инсталира fresh token. Храна M1 token-robustness тест. |

Fidelity тестови живе у `tests/UnitTests/CopyTrading/FakeTradingSessionFidelityTests.cs`.

## Opt-in, подразумева чува наслеђе понашање

Сваки fidelity дугме **off подразумевано** тако fake задржи просто увек-пуни понашање за тестове који јој не брину. Тест opt-in per налог:

```csharp
session.VolumeBoundsForCtid[slave]        = (Step: 10, Min: 10, Max: 1000); // F2
session.PartialFillFractionForCtid[slave] = 0.6;                            // F1 / G5
session.RejectReasonForCtid[slave]        = CtraderRejectReason.NotEnoughMoney; // F12 (one-shot)
session.InvalidateToken(slave);                                             // F13
```

## Карактеризација + conformance (планирано, чува fake ≡ прави)

Два механизма чувају fake честитим против покретног правог сервера (пратило, слетању кроз Phase 0a):

1. **Live карактеризација** (`LiveApiCharacterization`, демо налози, secrets-gated, `Inconclusive` на затворено тржишту): вози прави Open API, записи тачне жице истине (event секвенце, скалирање, одбити кодови) у golden фиксетуре проверене у тест пројекат. Нема тајни у фиксетурама — само посматране облике.
2. **Conformance упряжи**: покренути *исту* сценарио пакет два пута — једном против `FakeTradingSession`, једном против live сесије (када су тајне присутне) — потврди идентична посматрана исходи. Прави сервер промене → live ноге неуспевају → ажурирај fake. Ово прави "unit тестови покрију све" веродостојно.

Live акредитиви: `secrets/dev-credentials.local.json` (или наслеђе подељене датотеке) — видите `docs/testing/dev-credentials.md`.
