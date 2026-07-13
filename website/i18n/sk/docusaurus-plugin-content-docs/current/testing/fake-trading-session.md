---
description: "tests/UnitTests/CopyTrading/FakeTradingSession.cs = in-memory IOpenApiTradingSession, proti ktorému bežia všetky copy-trading jednotkové testy. Úloha: napodobniť reálny cTrader Open…"
---

# FakeTradingSession — cTrader Open API fidelity contract

`tests/UnitTests/CopyTrading/FakeTradingSession.cs` = in-memory `IOpenApiTradingSession`, proti ktorému
bežia všetky copy-trading jednotkové testy. Úloha: napodobniť **reálny cTrader Open API server**
dosť blízko na to, aby jednotkové testy pokrývali správanie, ktoré predtým zachytával len live tier. Tento doc =
fidelity contract: čo fake modeluje, ako presne, a pravidlo udržiavajúce ho čestným.

> **Záväzné pravidlo (CLAUDE.md):** fake zostáva cTrader-faithful. **Rozširujte ho, nikdy ho neoslabujte**
> pre passing testu. Každé nové reálne správanie, na ktoré sa spoliehate, sem modelujte, pripnuté fidelity testom.

## Fidelity matrix (F1–F13)

Trackuje plán `plans/copy-trading-overhaul.md` §7.6. Legenda: ✅ modelované · ◑ čiastočné (opt-in / rozširujúce) · ⬜ ešte nemodelované.

| # | Reálne Open API správanie | Fake status | Ako je to modelované |
|---|------------------------|-------------|-------------------|
| F1 | Market order môže **partial-fill** | ◑ | `PartialFillFractionForCtid[ctid] = f` fills len `f×volume`; reconcile potom ukazuje medzeru Phase‑1 true‑up (G5) zatvára. Accept→fill event pair stále prísť. |
| F2 | Volume normalizované na **step**, odmietnuté pod **min** / nad **max** | ✅ | `VolumeBoundsForCtid[ctid] = (Step, Min, Max)` rounds down to step, throws `CtraderRejectException(VolumeTooLow/High)`. |
| F3 | **Invalid SL/TP** odmietnuté (side + digits) | ⬜ | Plánované Phase 0a/1 (páry s M6 SL/TP precision normalization). |
| F4 | Ceny **integer-scaled by digits**; `pipPosition` | ◑ | `SymbolDetails` teraz nesie `Digits` (a `MaxVolume`), populated z reálneho symbolu; `PipPosition` poháňa market-range tolerance, `Digits` poháňa SL/TP precision normalization (M6). Full integer price scaling stále čaká. |
| F5 | **Market-range** fills len ak spot je v `base ± slippage`, inak odmietne | ✅ | `IsMarketRangeRejected` porovnáva live spot (`SetSpot`) s `baseSlippagePrice ± slippageInPoints`. Legacy `RejectMarketRangeForCtid` flag stále forces reject. |
| F6 | **Pending trigger→fill** dual event (Order nesie `positionId` + OPEN Position) | ◑ | `PushOpen(..., orderId:)` reprodukuje filled-pending event; FX‑Blue/cMAM double-copy dedupe pokryté v `CopyEngineHostTests.Filled_pending_does_not_double_open`. |
| F7 | **Server-driven closes** (SL/TP hit, stop-out) | ⬜ | Dnes test-pushed (`PushClose`); price-driven SL/TP-hit + stop-out closes plánované. |
| F8 | **Per-account** symbol tables / details | ◑ | Symbol names/ids per-fake; per-account divergent tables (cross-broker) čaká. |
| F9 | Full **account state** (balance, equity, margin, freeMargin) | ◑ | `Balance` + `LoadPositionValuationsAsync` (entry/swap/commission cez `SetPositionValuation`) + `SetSpot` feed real equity do proportional-equity sizing (G2, jednotka-testované v `CopyEquitySizingTests`). Used margin not exposed by reconcile API, takže free-margin reported ako equity. |
| F10 | Události nesú **server timestamps** | ✅ | `ExecutionEvent.ServerTimestamp` (unix ms) — reálna session číta z deal's `ExecutionTimestamp`; `PushOpen`/`PushPending` akceptujú `serverTimestamp:` takže `FakeTimeProvider`-driven test poháňa reálnu copy latency (G1). |
| F11 | **Trading mode / schedule** (disabled / close-only / closed) | ⬜ | Plánované Phase 2b. |
| F12 | **Typed error taxonomy** (`ProtoOAErrorRes` kódy) | ✅ | `RejectReasonForCtid[ctid] = CtraderRejectReason.X` throws one-shot `CtraderRejectException(reason)` (NotEnoughMoney, MarketClosed, PositionNotFound, …). |
| F13 | **Token invalidation** — stale token → auth error | ✅ | `InvalidateToken(ctid)` označí priložený token ako stale; trading calls hodia **reálnu** `OpenApiException` s `OpenApiErrorKind.TokenInvalid` (kód `CH_ACCESS_TOKEN_INVALID`), presne ako live server, kým `SwapAccessTokenAsync` nenainštaluje čerstvý token. Feed do M1 token-robustness testu. |

Fidelity testy žijú v `tests/UnitTests/CopyTrading/FakeTradingSessionFidelityTests.cs`.

## Opt-in, predvolené zachovávajú legacy správanie

Každý fidelity gombík **vypnutý predvolene** takže fake zachováva simple always-fill správanie pre testy,
ktorým na tom nezáleží. Test opt-in per account:

```csharp
session.VolumeBoundsForCtid[slave]        = (Step: 10, Min: 10, Max: 1000); // F2
session.PartialFillFractionForCtid[slave] = 0.6;                            // F1 / G5
session.RejectReasonForCtid[slave]        = CtraderRejectReason.NotEnoughMoney; // F12 (one-shot)
session.InvalidateToken(slave);                                             // F13
```

## Characterization + conformance (plánované, udržuje fake ≡ reálny)

Dva mechanizmy udržiavajú fake čestným voči pohyblivému reálnemu serveru (tracked, landing across Phase 0a):

1. **Live characterization** (`LiveApiCharacterization`, demo účty, secrets-gated, `Inconclusive` na closed market): hnaj reálne Open API, record exact wire truth (event sequences, scaling, reject codes) do golden fixtures checked into test project. Žiadne secrets vo fixtures — len pozorované shapes.
2. **Conformance harness**: spustí *rovnakú* scenár suitu dvakrát — raz proti `FakeTradingSession`, raz proti live session (keď sú secrets prítomné) — assertuje identické observable outcomes. Reálny server sa zmení → live leg zlyhá → update fake. Toto robí "jednotkové testy pokrývajú všetko" dôveryhodným.

Live creds: `secrets/dev-credentials.local.json` (alebo legacy split files) — pozrite `docs/testing/dev-credentials.md`.
