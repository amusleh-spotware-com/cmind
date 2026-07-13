---
description: "tests/UnitTests/CopyTrading/FakeTradingSession.cs = in-memory IOpenApiTradingSession tất cả copy-trading unit tests chạy chống. Job: mimic real cTrader Open…"
---

# FakeTradingSession — cTrader Open API fidelity contract

`tests/UnitTests/CopyTrading/FakeTradingSession.cs` = in-memory `IOpenApiTradingSession` tất cả copy-trading unit tests chạy chống. Job: mimic **real cTrader Open API server** đủ gần để unit tests cover behavior chỉ live tier dùng để catch. Doc này = fidelity contract: fake models cái gì, bao trung thực như thế nào, và rule giữ nó trung thực.

> **Binding rule (CLAUDE.md):** fake stays cTrader-faithful. **Extend it, không bao giờ weaken it** để pass test. Mỗi real behavior mới bạn rely on được modeled ở đây, pinned bởi fidelity test.

## Fidelity matrix (F1–F13)

Tracks plan `plans/copy-trading-overhaul.md` §7.6. Legend: ✅ modeled · ◑ partial (opt-in / extending) · ⬜ không modeled yet.

| # | Real Open API behavior | Fake status | Cách nó được modeled |
|---|------------------------|-------------|-------------------|
| F1 | Market order có thể **partial-fill** | ◑ | `PartialFillFractionForCtid[ctid] = f` fills chỉ `f×volume`; reconcile sau đó hiển thị gap Phase-1 true-up (G5) closes. Accept→fill event pair vẫn còn đến. |
| F2 | Volume normalized tới **step**, rejected dưới **min** / trên **max** | ✅ | `VolumeBoundsForCtid[ctid] = (Step, Min, Max)` rounds down tới step, throws `CtraderRejectException(VolumeTooLow/High)`. |
| F3 | **Invalid SL/TP** rejected (side + digits) | ⬜ | Planned Phase 0a/1 (pairs với M6 SL/TP precision normalization). |
| F4 | Prices **integer-scaled bởi digits**; `pipPosition` | ◑ | `SymbolDetails` giờ carries `Digits` (và `MaxVolume`), populated từ real symbol; `PipPosition` drives market-range tolerance, `Digits` drives SL/TP precision normalization (M6). Full integer price scaling vẫn pending. |
| F5 | **Market-range** fills chỉ nếu spot trong `base ± slippage`, nếu không rejects | ✅ | `IsMarketRangeRejected` so sánh live spot (`SetSpot`) tới `baseSlippagePrice ± slippageInPoints`. Legacy `RejectMarketRangeForCtid` flag vẫn forces reject. |
| F6 | **Pending trigger→fill** dual event (Order carries `positionId` + OPEN Position) | ◑ | `PushOpen(..., orderId:)` reproduces filled-pending event; FX-Blue/cMAM double-copy dedupe covered trong `CopyEngineHostTests.Filled_pending_does_not_double_open`. |
| F7 | **Server-driven closes** (SL/TP hit, stop-out) | ⬜ | Hôm nay closes test-pushed (`PushClose`); price-driven SL/TP-hit + stop-out closes planned. |
| F8 | **Per-account** symbol tables / details | ◑ | Symbol names/ids per-fake; per-account divergent tables (cross-broker) pending. |
| F9 | Full **account state** (balance, equity, margin, freeMargin) | ◑ | `Balance` + `LoadPositionValuationsAsync` (entry/swap/commission qua `SetPositionValuation`) + `SetSpot` feed real equity vào proportional-equity sizing (G2, unit-tested trong `CopyEquitySizingTests`). Used margin không exposed bởi reconcile API, nên free-margin reported như equity. |
| F10 | Events carry **server timestamps** | ✅ | `ExecutionEvent.ServerTimestamp` (unix ms) — real session reads từ deal's `ExecutionTimestamp`; `PushOpen`/`PushPending` accept `serverTimestamp:` nên `FakeTimeProvider`-driven test drives real copy latency (G1). |
| F11 | **Trading mode / schedule** (disabled / close-only / closed) | ⬜ | Planned Phase 2b. |
| F12 | **Typed error taxonomy** (`ProtoOAErrorRes` codes) | ✅ | `RejectReasonForCtid[ctid] = CtraderRejectReason.X` throws one-shot `CtraderRejectException(reason)` (NotEnoughMoney, MarketClosed, PositionNotFound, …). |
| F13 | **Token invalidation** — stale token → auth error | ✅ | `InvalidateToken(ctid)` marks attached token stale; trading calls throw **real** `OpenApiException` với `OpenApiErrorKind.TokenInvalid` (code `CH_ACCESS_TOKEN_INVALID`), chính xác như live server, cho đến `SwapAccessTokenAsync` installs fresh token. Feeds M1 token-robustness test. |

Fidelity tests nằm trong `tests/UnitTests/CopyTrading/FakeTradingSessionFidelityTests.cs`.

## Opt-in, defaults preserve legacy behavior

Mỗi fidelity knob **off by default** nên fake giữ đơn giản always-fill behavior cho tests không care. Test opts in per account:

```csharp
session.VolumeBoundsForCtid[slave]        = (Step: 10, Min: 10, Max: 1000); // F2
session.PartialFillFractionForCtid[slave] = 0.6;                            // F1 / G5
session.RejectReasonForCtid[slave]        = CtraderRejectReason.NotEnoughMoney; // F12 (one-shot)
session.InvalidateToken(slave);                                             // F13
```

## Characterization + conformance (planned, keeps the fake ≡ real)

Hai mechanisms giữ fake trung thực chống moving real server (tracked, landing across Phase 0a):

1. **Live characterization** (`LiveApiCharacterization`, demo accounts, secrets-gated, `Inconclusive` trên closed market): drive real Open API, record exact wire truth (event sequences, scaling, reject codes) vào golden fixtures checked vào test project. Không secrets trong fixtures — chỉ observed shapes.
2. **Conformance harness**: chạy *same* scenario suite hai lần — một lần chống `FakeTradingSession`, một lần chống live session (khi secrets present) — assert identical observable outcomes. Real server changes → live leg fails → update fake. Cái này làm "unit tests cover everything" trustworthy.

Live credentials: `secrets/dev-credentials.local.json` (hoặc legacy split files) — xem `docs/testing/dev-credentials.md`.
