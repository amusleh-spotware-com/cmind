---
description: "tests/UnitTests/CopyTrading/FakeTradingSession.cs = in-memory IOpenApiTradingSession all copy-trading unit tests run against. Job: mimic real cTrader Open…"
---

# FakeTradingSession — cTrader Open API fidelity contract

`tests/UnitTests/CopyTrading/FakeTradingSession.cs` = in-memory `IOpenApiTradingSession` ทั้งหมด copy-trading unit tests run against job: mimic **real cTrader Open API server** close enough ที่ unit tests cover behavior เพียง live tier ใช้ ไป catch doc นี้ = fidelity contract: what fake models วิธี faithfully และ rule keeping มัน honest

> **Binding rule (CLAUDE.md):** fake stays cTrader-faithful **extend มัน ไม่เคยweaken มัน** ไป pass test ทุก new real behavior คุณ rely on gets modeled ที่นี่ pinned โดย fidelity test

## Fidelity matrix (F1–F13)

tracks plan `plans/copy-trading-overhaul.md` §7.6 legend: ✅ modeled · ◑ partial (opt-in / extending) · ⬜ not yet modeled

| # | Real Open API behavior | Fake status | How มันmodeled |
|---|------------------------|-------------|-------------------|
| F1 | Market order can **partial-fill** | ◑ | `PartialFillFractionForCtid[ctid] = f` fills เพียง `f×volume`; reconcile จากนั้น shows gap phase‑1 true‑up (G5) closes accept→fill event pair still ไป come |
| F2 | Volume normalized ไป **step** rejected ด้านล่าง **min** / ข้างบน **max** | ✅ | `VolumeBoundsForCtid[ctid] = (Step, Min, Max)` rounds ลง ไป step throws `CtraderRejectException(VolumeTooLow/High)` |
| F3 | **Invalid SL/TP** rejected (side + digits) | ⬜ | planned phase 0a/1 (pairs ด้วย M6 SL/TP precision normalization) |
| F4 | prices **integer-scaled โดย digits**; `pipPosition` | ◑ | `SymbolDetails` now carries `Digits` (และ `MaxVolume`) populated จาก real symbol; `PipPosition` drives market-range tolerance `Digits` drives SL/TP precision normalization (M6) full integer price scaling still pending |
| F5 | **Market-range** fills เพียง ถ้า spot ใน `base ± slippage` else rejects | ✅ | `IsMarketRangeRejected` compares live spot (`SetSpot`) ไป `baseSlippagePrice ± slippageInPoints` legacy `RejectMarketRangeForCtid` flag still forces reject |
| F6 | **Pending trigger→fill** dual event (order carries `positionId` + open position) | ◑ | `PushOpen(..., orderId:)` reproduces filled-pending event; FX‑Blue/cMAM double-copy dedupe covered ใน `CopyEngineHostTests.Filled_pending_does_not_double_open` |
| F7 | **Server-driven closes** (SL/TP hit stop-out) | ⬜ | today closes test-pushed (`PushClose`); price-driven SL/TP-hit + stop-out closes planned |
| F8 | **Per-account** symbol tables / details | ◑ | symbol names/ids per-fake; per-account divergent tables (cross-broker) pending |
| F9 | full **account state** (balance equity margin freeMargin) | ◑ | `Balance` + `LoadPositionValuationsAsync` (entry/swap/commission ผ่าน `SetPositionValuation`) + `SetSpot` feed real equity ไป proportional-equity sizing (G2 unit-tested ใน `CopyEquitySizingTests`) used margin ไม่exposed โดย reconcile API ดังนั้น free-margin reported เช่น equity |
| F10 | events carry **server timestamps** | ✅ | `ExecutionEvent.ServerTimestamp` (unix ms) — real session reads จาก deal ของ `ExecutionTimestamp`; `PushOpen`/`PushPending` accept `serverTimestamp:` ดังนั้น `FakeTimeProvider`-driven test drives real copy latency (G1) |
| F11 | **Trading mode / schedule** (disabled / close-only / closed) | ⬜ | planned phase 2b |
| F12 | **Typed error taxonomy** (`ProtoOAErrorRes` codes) | ✅ | `RejectReasonForCtid[ctid] = CtraderRejectReason.X` throws one-shot `CtraderRejectException(reason)` (NotEnoughMoney MarketClosed PositionNotFound …) |
| F13 | **Token invalidation** — stale token → auth error | ✅ | `InvalidateToken(ctid)` marks attached token stale; trading calls throw **real** `OpenApiException` ด้วย `OpenApiErrorKind.TokenInvalid` (code `CH_ACCESS_TOKEN_INVALID`) exactly like live server จนกระทั่ง `SwapAccessTokenAsync` installs fresh token feeds M1 token-robustness test |

fidelity tests live ใน `tests/UnitTests/CopyTrading/FakeTradingSessionFidelityTests.cs`

## Opt-in defaults preserve legacy behavior

ทุก fidelity knob **off by default** ดังนั้น fake keeps simple always-fill behavior สำหรับ tests ที่ ไม่มี care test opts ใน per account:

```csharp
session.VolumeBoundsForCtid[slave]        = (Step: 10, Min: 10, Max: 1000); // F2
session.PartialFillFractionForCtid[slave] = 0.6;                            // F1 / G5
session.RejectReasonForCtid[slave]        = CtraderRejectReason.NotEnoughMoney; // F12 (one-shot)
session.InvalidateToken(slave);                                             // F13
```

## Characterization + conformance (planned keeps fake ≡ real)

two mechanisms keep fake honest against moving real server (tracked landing ข้าม phase 0a):

1. **Live characterization** (`LiveApiCharacterization` demo accounts secrets-gated `Inconclusive` on closed market): drive real open API record exact wire truth (event sequences scaling reject codes) ไป golden fixtures checked ไป test project ไม่มี secrets ใน fixtures — เฉพาะ observed shapes
2. **Conformance harness**: run *same* scenario suite twice — once against `FakeTradingSession` once against live session (เมื่อ secrets present) — assert identical observable outcomes real server changes → live leg fails → update fake นี้ makes "unit tests cover everything" trustworthy

live credentials: `secrets/dev-credentials.local.json` (หรือ legacy split files) — ดู `docs/testing/dev-credentials.md`
