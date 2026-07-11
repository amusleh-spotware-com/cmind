# FakeTradingSession — cTrader Open API fidelity contract

`tests/UnitTests/CopyTrading/FakeTradingSession.cs` is the in-memory `IOpenApiTradingSession` that every
copy-trading unit test runs against. Its job is to mimic the **real cTrader Open API server** closely
enough that unit tests cover behavior the live tier used to be the only place to catch. This doc is the
fidelity contract: what the fake models, how faithfully, and the rule that keeps it honest.

> **Binding rule (CLAUDE.md):** the fake stays cTrader-faithful. **Extend it, never weaken it** to make a
> test pass. Every new real behavior you rely on must be modeled here, with a fidelity test that pins it.

## Fidelity matrix (F1–F13)

Tracks the plan's `plans/copy-trading-overhaul.md` §7.6. Legend: ✅ modeled · ◑ partial (opt-in / being
extended) · ⬜ not yet modeled.

| # | Real Open API behavior | Fake status | How it is modeled |
|---|------------------------|-------------|-------------------|
| F1 | Market order can **partial-fill** | ◑ | `PartialFillFractionForCtid[ctid] = f` fills only `f×volume`; reconcile then shows the gap the Phase‑1 true‑up (G5) closes. Accept→fill event pair still to come. |
| F2 | Volume normalized to **step**, rejected below **min** / above **max** | ✅ | `VolumeBoundsForCtid[ctid] = (Step, Min, Max)` rounds down to step, throws `CtraderRejectException(VolumeTooLow/High)`. |
| F3 | **Invalid SL/TP** rejected (side + digits) | ⬜ | Planned Phase 0a/1 (pairs with M6 SL/TP precision normalization). |
| F4 | Prices **integer-scaled by digits**; `pipPosition` | ◑ | `PipPosition` on `SymbolDetails` drives market-range tolerance; full per-symbol digit scaling pending (needs `SymbolDetails` enrichment in Phase 1). |
| F5 | **Market-range** fills only if spot within `base ± slippage`, else rejects | ✅ | `IsMarketRangeRejected` compares live spot (`SetSpot`) to `baseSlippagePrice ± slippageInPoints`. Legacy `RejectMarketRangeForCtid` flag still forces a reject. |
| F6 | **Pending trigger→fill** dual event (Order carries `positionId` + OPEN Position) | ◑ | `PushOpen(..., orderId:)` reproduces the filled-pending event; the FX‑Blue/cMAM double-copy dedupe is covered in `CopyEngineHostTests.Filled_pending_does_not_double_open`. |
| F7 | **Server-driven closes** (SL/TP hit, stop-out) | ⬜ | Today closes are test-pushed (`PushClose`); price-driven SL/TP-hit + stop-out closes planned. |
| F8 | **Per-account** symbol tables / details | ◑ | Symbol names/ids are per-fake; per-account divergent tables (cross-broker) pending. |
| F9 | Full **account state** (balance, equity, margin, freeMargin) | ◑ | `Balance` + `LoadPositionValuationsAsync` (entry/swap/commission via `SetPositionValuation`) + `SetSpot` feed real equity into proportional-equity sizing (G2, unit-tested in `CopyEquitySizingTests`). Used margin isn't exposed by the reconcile API, so free-margin is reported as equity. |
| F10 | Events carry **server timestamps** | ✅ | `ExecutionEvent.ServerTimestamp` (unix ms) — real session reads it from the deal's `ExecutionTimestamp`; `PushOpen`/`PushPending` accept `serverTimestamp:` so a `FakeTimeProvider`-driven test drives real copy latency (G1). |
| F11 | **Trading mode / schedule** (disabled / close-only / closed) | ⬜ | Planned Phase 2b. |
| F12 | **Typed error taxonomy** (`ProtoOAErrorRes` codes) | ✅ | `RejectReasonForCtid[ctid] = CtraderRejectReason.X` throws one-shot `CtraderRejectException(reason)` (NotEnoughMoney, MarketClosed, PositionNotFound, …). |
| F13 | **Token invalidation** — stale token → auth error | ✅ | `InvalidateToken(ctid)` marks the attached token stale; trading calls throw `CtraderTokenInvalidException` until `SwapAccessTokenAsync` installs a fresh token. |

Fidelity tests live in `tests/UnitTests/CopyTrading/FakeTradingSessionFidelityTests.cs`.

## Opt-in, defaults preserve legacy behavior

Every fidelity knob is **off by default** so the fake keeps its simple always-fill behavior for tests that
don't care. A test opts in per account:

```csharp
session.VolumeBoundsForCtid[slave]        = (Step: 10, Min: 10, Max: 1000); // F2
session.PartialFillFractionForCtid[slave] = 0.6;                            // F1 / G5
session.RejectReasonForCtid[slave]        = CtraderRejectReason.NotEnoughMoney; // F12 (one-shot)
session.InvalidateToken(slave);                                             // F13
```

## Characterization + conformance (planned, keeps the fake ≡ real)

Two mechanisms keep the fake honest against the moving real server (tracked, landing across Phase 0a):

1. **Live characterization** (`LiveApiCharacterization`, demo accounts, secrets-gated, `Inconclusive` on a
   closed market): drive the real Open API, record the exact wire truth (event sequences, scaling, reject
   codes) into golden fixtures checked into the test project. No secrets in the fixtures — only observed
   shapes.
2. **Conformance harness**: run the *same* scenario suite twice — once against `FakeTradingSession`, once
   against the live session (when secrets present) — and assert identical observable outcomes. If the real
   server changes, the live leg fails and we update the fake. This is what makes "unit tests cover
   everything" trustworthy.

Live credentials: `secrets/dev-credentials.local.json` (or the legacy split files) — see
`docs/testing/dev-credentials.md`.
