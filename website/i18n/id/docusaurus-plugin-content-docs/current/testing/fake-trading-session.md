---
description: "tests/UnitTests/CopyTrading/FakeTradingSession.cs = in-memory IOpenApiTradingSession semua copy-trading unit test jalankan terhadap. Job: mimic real cTrader Open…"
---

# FakeTradingSession — kontrak fidelity cTrader Open API

`tests/UnitTests/CopyTrading/FakeTradingSession.cs` = in-memory `IOpenApiTradingSession` semua copy-trading unit test jalankan terhadap. Job: mimic **real cTrader Open API server** cukup dekat sehingga unit test cover behavior hanya live tier yang digunakan untuk catch. Doc ini = kontrak fidelity: apa fake model, seberapa faithful, dan rule simpan jujur.

> **Binding rule (CLAUDE.md):** fake tetap cTrader-faithful. **Extend, tidak pernah weaken** untuk pass test. Setiap real behavior baru Anda andalkan get modeled di sini, pinned oleh fidelity test.

## Matrix fidelity (F1–F13)

Tracks plan `plans/copy-trading-overhaul.md` §7.6. Legenda: ✅ modeled · ◑ partial (opt-in / extending) · ⬜ not yet modeled.

| # | Real Open API behavior | Status fake | Bagaimana itu di-model |
|---|------------------------|-------------|-------------------|
| F1 | Market order dapat **partial-fill** | ◑ | `PartialFillFractionForCtid[ctid] = f` fills hanya `f×volume`; reconcile kemudian show gap Phase‑1 true‑up (G5) closes. Accept→fill event pair masih akan datang. |
| F2 | Volume normalized ke **step**, rejected di bawah **min** / di atas **max** | ✅ | `VolumeBoundsForCtid[ctid] = (Step, Min, Max)` round down ke step, throw `CtraderRejectException(VolumeTooLow/High)`. |
| F3 | **Invalid SL/TP** rejected (side + digit) | ⬜ | Rencana Phase 0a/1 (pair dengan M6 SL/TP precision normalization). |
| F4 | Harga **integer-scaled oleh digit**; `pipPosition` | ◑ | `SymbolDetails` sekarang carry `Digits` (dan `MaxVolume`), populated dari real symbol; `PipPosition` drive market-range tolerance, `Digits` drive SL/TP precision normalization (M6). Full integer price scaling masih pending. |
| F5 | **Market-range** fills hanya jika spot di dalam `base ± slippage`, else reject | ✅ | `IsMarketRangeRejected` bandingkan live spot (`SetSpot`) ke `baseSlippagePrice ± slippageInPoints`. Legacy `RejectMarketRangeForCtid` flag masih force reject. |
| F6 | **Pending trigger→fill** dual event (Order carry `positionId` + OPEN Position) | ◑ | `PushOpen(..., orderId:)` reproduce filled-pending event; FX‑Blue/cMAM double-copy dedupe covered dalam `CopyEngineHostTests.Filled_pending_does_not_double_open`. |
| F7 | **Server-driven close** (SL/TP hit, stop-out) | ⬜ | Hari ini close test-pushed (`PushClose`); price-driven SL/TP-hit + stop-out close rencana. |
| F8 | **Per-account** symbol table / detail | ◑ | Nama/id symbol per-fake; per-account divergent table (cross-broker) pending. |
| F9 | Full **account state** (balance, equity, margin, freeMargin) | ◑ | `Balance` + `LoadPositionValuationsAsync` (entry/swap/commission via `SetPositionValuation`) + `SetSpot` feed real equity ke proportional-equity sizing (G2, unit-tested dalam `CopyEquitySizingTests`). Used margin tidak exposed oleh reconcile API, jadi free-margin reported sebagai equity. |
| F10 | Event carry **server timestamp** | ✅ | `ExecutionEvent.ServerTimestamp` (unix ms) — real session baca dari deal's `ExecutionTimestamp`; `PushOpen`/`PushPending` accept `serverTimestamp:` jadi `FakeTimeProvider`-driven test drive real copy latency (G1). |
| F11 | **Trading mode / schedule** (disabled / close-only / closed) | ⬜ | Rencana Phase 2b. |
| F12 | **Typed error taxonomy** (`ProtoOAErrorRes` code) | ✅ | `RejectReasonForCtid[ctid] = CtraderRejectReason.X` throw one-shot `CtraderRejectException(reason)` (NotEnoughMoney, MarketClosed, PositionNotFound, …). |
| F13 | **Token invalidation** — stale token → auth error | ✅ | `InvalidateToken(ctid)` mark attached token stale; trading call throw **real** `OpenApiException` dengan `OpenApiErrorKind.TokenInvalid` (code `CH_ACCESS_TOKEN_INVALID`), exactly seperti live server, sampai `SwapAccessTokenAsync` install fresh token. Feed M1 token-robustness test. |

Fidelity test hidup di `tests/UnitTests/CopyTrading/FakeTradingSessionFidelityTests.cs`.

## Opt-in, default preserve legacy behavior

Setiap fidelity knob **off by default** jadi fake simpan simple always-fill behavior untuk test yang tidak perduli. Test opt in per account:

```csharp
session.VolumeBoundsForCtid[slave]        = (Step: 10, Min: 10, Max: 1000); // F2
session.PartialFillFractionForCtid[slave] = 0.6;                            // F1 / G5
session.RejectReasonForCtid[slave]        = CtraderRejectReason.NotEnoughMoney; // F12 (one-shot)
session.InvalidateToken(slave);                                             // F13
```

## Characterization + conformance (planned, simpan fake ≡ real)

Dua mekanisme simpan fake jujur terhadap moving real server (tracked, landing melintasi Phase 0a):

1. **Live characterization** (`LiveApiCharacterization`, demo account, secret-gated, `Inconclusive` di closed market): drive real Open API, record exact wire truth (event sequence, scaling, reject code) ke golden fixture checked ke test project. Tidak ada secret dalam fixture — hanya observed shape.
2. **Conformance harness**: jalankan *same* scenario suite dua kali — sekali terhadap `FakeTradingSession`, sekali terhadap live session (saat secret present) — assert identical observable outcome. Real server berubah → live leg gagal → update fake. Ini membuat "unit test cover semuanya" trustworthy.
