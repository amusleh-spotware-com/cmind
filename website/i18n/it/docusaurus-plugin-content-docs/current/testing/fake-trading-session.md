---
description: "tests/UnitTests/CopyTrading/FakeTradingSession.cs = in-memory IOpenApiTradingSession su cui girano tutti i copy-trading unit test. Job: imitare il server Open API cTrader reale abbastanza fedelmente che i unit test coprano behavior che solo il live tier era solito catturare."
---

# FakeTradingSession — contratto di fedeltà Open API cTrader

`tests/UnitTests/CopyTrading/FakeTradingSession.cs` = `IOpenApiTradingSession` in-memory su cui girano tutti i
copy-trading unit test. Job: imitare il **server Open API cTrader reale** abbastanza fedelmente che i unit test
coprano behavior che solo il live tier era solito catturare. Questo doc = contratto di fedeltà: cosa faka
modella, quanto fedelmente, e regola per mantenerlo honest.

> **Regola binding (CLAUDE.md):** il fake resta cTrader-faithful. **Estenderlo, mai indebolirlo** per passare
> un test. Ogni nuovo comportamento reale su cui fai affidamento viene modellato qui, pinned da fidelity test.

## Matrice di fedeltà (F1–F13)

Traccia il piano `plans/copy-trading-overhaul.md` §7.6. Legenda: ✅ modellato · ◑ parziale (opt-in / extending) · ⬜ non ancora modellato.

| # | Comportamento Open API reale | Fake status | Come è modellato |
|---|------------------------|-------------|-------------------|
| F1 | Market order può **partial-fill** | ◑ | `PartialFillFractionForCtid[ctid] = f` riempie solo `f×volume`; reconcile poi mostra gap Phase-1 true-up (G5) closes. Accept→fill event pair ancora da venire. |
| F2 | Volume normalizzato a **step**, rifiutato sotto **min** / sopra **max** | ✅ | `VolumeBoundsForCtid[ctid] = (Step, Min, Max)` arrotonda down a step, throws `CtraderRejectException(VolumeTooLow/High)`. |
| F3 | **Invalid SL/TP** rifiutato (side + digits) | ⬜ | Pianificato Phase 0a/1 (coppie con M6 SL/TP precision normalization). |
| F4 | Prezzi **integer-scaled by digits**; `pipPosition` | ◑ | `SymbolDetails` ora porta `Digits` (e `MaxVolume`), popolato da simbolo reale; `PipPosition` guida market-range tolerance, `Digits` guida SL/TP precision normalization (M6). Full integer price scaling ancora pending. |
| F5 | **Market-range** fills solo se spot entro `base ± slippage`, altrimenti rifiuta | ✅ | `IsMarketRangeRejected` confronta live spot (`SetSpot`) con `baseSlippagePrice ± slippageInPoints`. Legacy `RejectMarketRangeForCtid` flag forza ancora rifiuto. |
| F6 | **Pending trigger→fill** evento duale (Order porta `positionId` + OPEN Position) | ◑ | `PushOpen(..., orderId:)` riproduce filled-pending event; FX-Blue/cMAM double-copy dedupe coperta in `CopyEngineHostTests.Filled_pending_does_not_double_open`. |
| F7 | **Server-driven closes** (SL/TP hit, stop-out) | ⬜ | Oggi test-pushed (`PushClose`); price-driven SL/TP-hit + stop-out closes pianificati. |
| F8 | **Per-account** symbol tables / details | ◑ | Symbol names/ids per-fake; per-account divergent tables (cross-broker) pending. |
| F9 | Full **account state** (balance, equity, margin, freeMargin) | ◑ | `Balance` + `LoadPositionValuationsAsync` (entry/swap/commission via `SetPositionValuation`) + `SetSpot` feed real equity in proportional-equity sizing (G2, unit-tested in `CopyEquitySizingTests`). Used margin non esposta da reconcile API, quindi free-margin riportato come equity. |
| F10 | Eventi portano **server timestamps** | ✅ | `ExecutionEvent.ServerTimestamp` (unix ms) — real session legge dal `ExecutionTimestamp` del deal; `PushOpen`/`PushPending` accettano `serverTimestamp:` così `FakeTimeProvider`-driven test guida real copy latency (G1). |
| F11 | **Trading mode / schedule** (disabled / close-only / closed) | ⬜ | Pianificato Phase 2b. |
| F12 | **Typed error taxonomy** (`ProtoOAErrorRes` codes) | ✅ | `RejectReasonForCtid[ctid] = CtraderRejectReason.X` throws one-shot `CtraderRejectException(reason)` (NotEnoughMoney, MarketClosed, PositionNotFound, …). |
| F13 | **Token invalidation** — stale token → auth error | ✅ | `InvalidateToken(ctid)` marca attached token stale; trading calls throw **real** `OpenApiException` con `OpenApiErrorKind.TokenInvalid` (code `CH_ACCESS_TOKEN_INVALID`), esattamente come live server, finché `SwapAccessTokenAsync` installa fresh token. Alimenta M1 token-robustness test. |

I fidelity test vivono in `tests/UnitTests/CopyTrading/FakeTradingSessionFidelityTests.cs`.

## Opt-in, defaults preservano legacy behavior

Ogni knob di fedeltà **off by default** così il fake mantiene sempre-fill behavior per test che non se ne curano.
Il test opta in per account:

```csharp
session.VolumeBoundsForCtid[slave]        = (Step: 10, Min: 10, Max: 1000); // F2
session.PartialFillFractionForCtid[slave] = 0.6;                            // F1 / G5
session.RejectReasonForCtid[slave]        = CtraderRejectReason.NotEnoughMoney; // F12 (one-shot)
session.InvalidateToken(slave);                                             // F13
```

## Characterization + conformance (pianificato, mantiene il fake ≡ reale)

Due meccanismi mantengono il fake honest contro il moving real server (tracciato, atterrando attraverso Phase 0a):

1. **Live characterization** (`LiveApiCharacterization`, demo accounts, secrets-gated, `Inconclusive` on closed market): drive real Open API, record exact wire truth (event sequences, scaling, reject codes) into golden fixtures checked into test project. No secrets in fixtures — only observed shapes.
2. **Conformance harness**: run *same* scenario suite twice — once against `FakeTradingSession`, once against live session (when secrets present) — assert identical observable outcomes. Real server changes → live leg fails → update fake. This makes "unit tests cover everything" trustworthy.

Live credentials: `secrets/dev-credentials.local.json` (or legacy split files) — see `docs/testing/dev-credentials.md`.
