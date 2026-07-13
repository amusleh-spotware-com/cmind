---
description: "tests/UnitTests/CopyTrading/FakeTradingSession.cs = in-memory IOpenApiTradingSession
wszystkie copy-trading unit tests run przeciwko. Job: mimic real cTrader Open…"
---

# FakeTradingSession — cTrader Open API fidelity contract

`tests/UnitTests/CopyTrading/FakeTradingSession.cs` = in-memory `IOpenApiTradingSession` wszystkie
copy-trading unit tests run przeciwko. Job: mimic **real cTrader Open API server** close enough
że unit tests cover behavior tylko live tier używane aby catch. Ta doc = fidelity contract:
co fake models, jak faithfully, i rule keeping to honest.

> **Binding rule (CLAUDE.md):** fake stays cTrader-faithful. **Extend to, nigdy nie weaken to**
> aby pass test. Każdy nowy real behavior ty rely na gets modeled tutaj, pinned przez fidelity test.

## Fidelity matrix (F1–F13)

Tracks plan `plans/copy-trading-overhaul.md` §7.6. Legend: ✅ modeled · ◑ partial (opt-in / extending) · ⬜ not yet modeled.

| # | Real Open API behavior | Fake status | Jak to jest modeled |
|---|------------------------|-------------|-------------------|
| F1 | Market order może **partial-fill** | ◑ | `PartialFillFractionForCtid[ctid] = f` fills tylko `f×volume`; reconcile potem shows gap Phase‑1 true‑up (G5) closes. Accept→fill event pair ciągle do przyjścia. |
| F2 | Volume normalized do **step**, rejected poniżej **min** / powyżej **max** | ✅ | `VolumeBoundsForCtid[ctid] = (Step, Min, Max)` rounds down do step, throws `CtraderRejectException(VolumeTooLow/High)`. |
| F3 | **Invalid SL/TP** rejected (side + digits) | ⬜ | Planned Phase 0a/1 (pairs z M6 SL/TP precision normalization). |
| F4 | Prices **integer-scaled przez digits**; `pipPosition` | ◑ | `SymbolDetails` teraz niesie `Digits` (i `MaxVolume`), populated z real symbol; `PipPosition` drives market-range tolerance, `Digits` drives SL/TP precision normalization (M6). Pełny integer price scaling ciągle pending. |
| F5 | **Market-range** fills tylko jeśli spot w `base ± slippage`, else rejects | ✅ | `IsMarketRangeRejected` compares live spot (`SetSpot`) do `baseSlippagePrice ± slippageInPoints`. Legacy `RejectMarketRangeForCtid` flag ciągle forces reject. |
| F6 | **Pending trigger→fill** dual event (Order niesie `positionId` + OPEN Position) | ◑ | `PushOpen(..., orderId:)` reproduces filled-pending event; FX‑Blue/cMAM double-copy dedupe covered w `CopyEngineHostTests.Filled_pending_does_not_double_open`. |
| F7 | **Server-driven closes** (SL/TP hit, stop-out) | ⬜ | Dzisiaj closes test-pushed (`PushClose`); price-driven SL/TP-hit + stop-out closes planned. |
| F8 | **Per-account** symbol tables / details | ◑ | Symbol names/ids per-fake; per-account divergent tables (cross-broker) pending. |
| F9 | Pełny **account state** (balance, equity, margin, freeMargin) | ◑ | `Balance` + `LoadPositionValuationsAsync` (entry/swap/commission poprzez `SetPositionValuation`) + `SetSpot` feed real equity do proportional-equity sizing (G2, unit-tested w `CopyEquitySizingTests`). Used margin nie exposed przez reconcile API, więc free-margin reported jako equity. |
| F10 | Events niosą **server timestamps** | ✅ | `ExecutionEvent.ServerTimestamp` (unix ms) — real session czyta z deal'a `ExecutionTimestamp`; `PushOpen`/`PushPending` accept `serverTimestamp:` więc `FakeTimeProvider`-driven test drives real copy latency (G1). |
| F11 | **Trading mode / schedule** (disabled / close-only / closed) | ⬜ | Planned Phase 2b. |
| F12 | **Typed error taxonomy** (`ProtoOAErrorRes` codes) | ✅ | `RejectReasonForCtid[ctid] = CtraderRejectReason.X` throws one-shot `CtraderRejectException(reason)` (NotEnoughMoney, MarketClosed, PositionNotFound, …). |
| F13 | **Token invalidation** — stale token → auth error | ✅ | `InvalidateToken(ctid)` marks attached token stale; trading calls throw **real** `OpenApiException` z `OpenApiErrorKind.TokenInvalid` (code `CH_ACCESS_TOKEN_INVALID`), dokładnie jak live server, aż `SwapAccessTokenAsync` installs fresh token. Feeds M1 token-robustness test. |

Fidelity tests żyją w `tests/UnitTests/CopyTrading/FakeTradingSessionFidelityTests.cs`.

## Opt-in, defaults preserve legacy behavior

Każdy fidelity knob **off domyślnie** więc fake keeps simple always-fill behavior dla tests że
nie care. Test opts w per account:

```csharp
session.VolumeBoundsForCtid[slave]        = (Step: 10, Min: 10, Max: 1000); // F2
session.PartialFillFractionForCtid[slave] = 0.6;                            // F1 / G5
session.RejectReasonForCtid[slave]        = CtraderRejectReason.NotEnoughMoney; // F12 (one-shot)
session.InvalidateToken(slave);                                             // F13
```

## Characterization + conformance (planned, keeps fake ≡ real)

Dwa mechanisms keep fake honest przeciwko moving real server (tracked, landing across Phase 0a):

1. **Live characterization** (`LiveApiCharacterization`, demo accounts, secrets-gated, `Inconclusive`
   na closed market): drive real Open API, record exact wire truth (event sequences, scaling, reject
   codes) do golden fixtures checked do test project. Brak sekrety w fixtures — tylko observed
   shapes.
2. **Conformance harness**: run *ten sam* scenario suite dwa razy — raz przeciwko `FakeTradingSession`,
   raz przeciwko live session (gdy sekrety present) — assert identical observable outcomes. Real
   server zmienia → live leg fails → update fake. To makes "unit tests cover wszystko" trustworthy.

Live credentials: `secrets/dev-credentials.local.json` (albo legacy split files) — zobacz
`docs/testing/dev-credentials.md`.
