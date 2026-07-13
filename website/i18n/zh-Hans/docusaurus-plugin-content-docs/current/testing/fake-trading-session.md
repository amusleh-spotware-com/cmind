---
description: "tests/UnitTests/CopyTrading/FakeTradingSession.cs = in-memory IOpenApiTradingSession all copy-trading unit tests run against. Job: mimic real cTrader Open…"
---

# FakeTradingSession — cTrader Open API fidelity contract

`tests/UnitTests/CopyTrading/FakeTradingSession.cs` = in-memory `IOpenApiTradingSession` all copy-trading unit tests run against. Job: mimic **real cTrader Open API server** close enough that unit tests cover behavior only live tier used to catch. This doc = fidelity contract: what fake models, how faithfully, and rule keeping it honest.

> **Binding rule (CLAUDE.md):** fake stays cTrader-faithful. **Extend it, never weaken it** to pass test. Every new real behavior you rely on gets modeled here, pinned by fidelity test.

## Fidelity matrix (F1–F13)

Tracks plan `plans/copy-trading-overhaul.md` §7.6. Legend: ✅ modeled · ◑ partial (opt-in / extending) · ⬜ not yet modeled.

| # | Real Open API behavior | Fake status | How it is modeled |
|---|------------------------|-------------|-------------------|
| F1 | Market order can **partial-fill** | ◑ | `PartialFillFractionForCtid[ctid] = f` fills only `f×volume`; reconcile then shows gap Phase‑1 true‑up (G5) closes. Accept→fill event pair still to come. |
| F2 | Volume normalized to **step**, rejected below **min** / above **max** | ✅ | `VolumeBoundsForCtid[ctid] = (Step, Min, Max)` rounds down to step, throws `CtraderRejectException(VolumeTooLow/High)`. |
| F3 | **Invalid SL/TP** rejected (side + digits) | ⬜ | Planned Phase 0a/1 (pairs with M6 SL/TP precision normalization). |
| F4 | Prices **integer-scaled by digits**; `pipPosition` | ◑ | `SymbolDetails` now carries `Digits` (and `MaxVolume`), populated from real symbol; `PipPosition` drives market-range tolerance, `Digits` drives SL/TP precision normalization (M6). Full integer price scaling still pending. |
| F5 | **Market-range** fills only if spot within `base ± slippage`, else rejects | ✅ | `IsMarketRangeRejected` compares live spot (`SetSpot`) to `baseSlippagePrice ± slippageInPoints`. Legacy `RejectMarketRangeForCtid` flag still forces reject. |
| F6 | **Pending trigger→fill** dual event (Order carries `positionId` + OPEN Position) | ◑ | `PushOpen(..., orderId:)` reproduces filled-pending event; FX‑Blue/cMAM double-copy dedupe covered in `CopyEngineHostTests.Filled_pending_does_not_double_open`. |
| F7 | **Server-driven closes** (SL/TP hit, stop-out) | ⬜ | Today closes test-pushed (`PushClose`); price-driven SL/TP-hit + stop-out closes planned. |
| F8 | **Per-account** symbol tables / details | ◑ | Symbol names/ids per-fake; per-account divergent tables (cross-broker) pending. |
| F9 | Full **account state** (balance, equity, margin, freeMargin) | ◑ | `Balance` + `LoadPositionValuationsAsync` (entry/swap/commission via `SetPositionValuation`) + `SetSpot` feed real equity into proportional-equity sizing (G2, unit-tested in `CopyEquitySizingTests`). Used margin not exposed by reconcile API, so free-margin reported as equity. |
| F10 | Events carry **server timestamps** | ✅ | `ExecutionEvent.ServerTimestamp` (unix ms) — real session reads from deal's `ExecutionTimestamp`; `PushOpen`/`PushPending` accept `serverTimestamp:` so `FakeTimeProvider`-driven test drives real copy latency (G1). |
| F11 | **Trading mode / schedule** (disabled / close-only / closed) | ⬜ | Planned Phase 2b. |
| F12 | **Typed error taxonomy** (`ProtoOAErrorRes` codes) | ✅ | `RejectReasonForCtid[ctid] = CtraderRejectReason.X` throws one-shot `CtraderRejectException(reason)` (NotEnoughMoney, MarketClosed, PositionNotFound, …). |
| F13 | **Token invalidation** — stale token → auth error | ✅ | `InvalidateToken(ctid)` marks attached token stale; trading calls throw **real** `OpenApiException` with `OpenApiErrorKind.TokenInvalid` (code `CH_ACCESS_TOKEN_INVALID`), exactly like live server, until `SwapAccessTokenAsync` installs fresh token. Feeds M1 token-robustness test. |

Fidelity tests live in `tests/UnitTests/CopyTrading/FakeTradingSessionFidelityTests.cs`.

## Opt-in, defaults preserve legacy behavior

Every fidelity knob **off by default** so fake keeps simple always-fill behavior for tests that don't care. Test opts in per account:

```csharp
session.VolumeBoundsForCtid[slave]        = (Step: 10, Min: 10, Max: 1000); // F2
session.PartialFillFractionForCtid[slave] = 0.6;                            // F1 / G5
session.RejectReasonForCtid[slave]        = CtraderRejectReason.NotEnoughMoney; // F12 (one-shot)
session.InvalidateToken(slave);                                             // F13
```

## Characterization + conformance (planned, keeps the fake ≡ real)

Two mechanisms keep fake honest against moving real server (tracked, landing across Phase 0a):

1. **Live characterization** (`LiveApiCharacterization`, demo accounts, secrets-gated, `Inconclusive` on closed market): drive real Open API, record exact wire truth (event sequences, scaling, reject codes) into golden fixtures checked into test project. No secrets in fixtures — only observed shapes.
2. **Conformance harness**: run *same* scenario suite twice — once against `FakeTradingSession`, once against live session (when secrets present) — assert identical observable outcomes. Real server changes → live leg fails → update fake. This makes "unit tests cover everything" trustworthy.

Live credentials: `secrets/dev-credentials.local.json` (or legacy split files) — see `docs/testing/dev-credentials.md`.
<!-- [ZH-HANS] Translation needed -->
