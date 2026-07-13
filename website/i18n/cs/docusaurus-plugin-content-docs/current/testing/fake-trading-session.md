---
description: "tests/UnitTests/CopyTrading/FakeTradingSession.cs = in-memory IOpenApiTradingSession, proti které běží všechny unit testy kopírovacího obchodování. Úkol: napodobit skutečný cTrader Open API server dostatečně věrně, aby unit testy pokryly chování, které dříve zachycoval pouze live tier."
---

# FakeTradingSession — smlouva věrnosti cTrader Open API

`tests/UnitTests/CopyTrading/FakeTradingSession.cs` = in-memory `IOpenApiTradingSession`, proti které běží všechny unit testy kopírovacího obchodování. Úkol: napodobit **skutečný cTrader Open API server** dostatečně věrně, aby unit testy pokryly chování, které dříve zachycoval pouze live tier. Tento dokument = smlouva věrnosti: co fake modeluje, jak věrně, a pravidla pro udržení faky čestné.

> **Závazné pravidlo (CLAUDE.md):** fake zůstává cTrader-věrný. **Rozšiřujte jej, nikdy jej neoslabujte** pro průchod testu. Každé nové reálné chování, na které se spoléháte, se modeluje zde, připnuto fidelity testem.

## Matice věrnosti (F1–F13)

Sleduje plán `plans/copy-trading-overhaul.md` §7.6. Legenda: ✅ modelováno · ◑ částečně (opt-in / rozšiřující) · ⬜ zatím nemodelováno.

| # | Reálné chování Open API | Status faky | Jak je to modelováno |
|---|------------------------|-------------|-------------------|
| F1 | Market příkaz může **částečně vyplnit** | ◑ | `PartialFillFractionForCtid[ctid] = f` vyplní pouze `f×volume`; následná rekonciliace ukazuje mezeru Fáze‑1 true‑up (G5) uzavře. Dvojice Accept→fill událost stále chybí. |
| F2 | Volume normalizováno na **krok**, odmítnuto pod **min** / nad **max** | ✅ | `VolumeBoundsForCtid[ctid] = (Step, Min, Max)` zaokrouhlí dolů na krok, vyhodí `CtraderRejectException(VolumeTooLow/High)`. |
| F3 | **Neplatný SL/TP** odmítnut (směr + cifry) | ⬜ | Plánováno Fáze 0a/1 (spárováno s M6 normalizací přesnosti SL/TP). |
| F4 | Ceny **celočíselně škálované podle cifer**; `pipPosition` | ◑ | `SymbolDetails` nyní nese `Digits` (a `MaxVolume`), naplněno z reálného symbolu; `PipPosition` řídí toleranci tržního rozsahu, `Digits` řídí normalizaci přesnosti SL/TP (M6). Úplné celočíselné škálování cen stále čeká. |
| F5 | **Tržní rozsah** vyplní pouze pokud je spot v rámci `base ± slippage`, jinak odmítne | ✅ | `IsMarketRangeRejected` porovnává live spot (`SetSpot`) s `baseSlippagePrice ± slippageInPoints`. Legacy příznak `RejectMarketRangeForCtid` stále vynucuje odmítnutí. |
| F6 | **Pending trigger→fill** dvojitá událost (Order nese `positionId` + OPEN Position) | ◑ | `PushOpen(..., orderId:)` reprodukuje událost vyplněného pending; FX‑Blue/cMAM deduplikace dvojitého kopírování pokryta v `CopyEngineHostTests.Filled_pending_does_not_double_open`. |
| F7 | **Serverem řízená uzavření** (SL/TP zasaženo, stop-out) | ⬜ | Dnes uzavření test-push (`PushClose`); cena řízená SL/TP-zasaženo + stop-out uzavření plánována. |
| F8 | **Na účet** symbolové tabulky / detaily | ◑ | Symbolová jména/ids per-fake; per-account divergentní tabulky (cross-broker) čekají. |
| F9 | Plný **stav účtu** (balance, equity, margin, freeMargin) | ◑ | `Balance` + `LoadPositionValuationsAsync` (entry/swap/commission přes `SetPositionValuation`) + `SetSpot` feed reálné equity do proporčního equity sizingu (G2, unit-testováno v `CopyEquitySizingTests`). Použitá margin není vystavena přes reconcile API, takže free-margin hlášena jako equity. |
| F10 | Události nesou **serverová časová razítka** | ✅ | `ExecutionEvent.ServerTimestamp` (unix ms) — reálná session čte z deal's `ExecutionTimestamp`; `PushOpen`/`PushPending` přijímají `serverTimestamp:`, takže `FakeTimeProvider`-řizený test řídí reálnou copy latenci (G1). |
| F11 | **Obchodní režim / rozvrh** (disabled / close-only / closed) | ⬜ | Plánováno Fáze 2b. |
| F12 | **Typovaná taxonomie chyb** (`ProtoOAErrorRes` kódy) | ✅ | `RejectReasonForCtid[ctid] = CtraderRejectReason.X` vyhodí one-shot `CtraderRejectException(reason)` (NotEnoughMoney, MarketClosed, PositionNotFound, …). |
| F13 | **Zrušení tokenu** — neplatný token → chyba auth | ✅ | `InvalidateToken(ctid)` označí připojený token jako neplatný; obchodní volání vyhodí **skutečnou** `OpenApiException` s `OpenApiErrorKind.TokenInvalid` (kód `CH_ACCESS_TOKEN_INVALID`), přesně jako live server, dokud `SwapAccessTokenAsync` neinstaluje čerstvý token. Napájí M1 token-robustness test. |

Fidelity testy žijí v `tests/UnitTests/CopyTrading/FakeTradingSessionFidelityTests.cs`.

## Opt-in, výchozí hodnoty zachovávají legacy chování

Každý fidelity knoflík **vypnutý ve výchozím stavu**, takže fake zachovává jednoduché always-fill chování pro testy, které to nepotřebují. Test opt-in per účet:

```csharp
session.VolumeBoundsForCtid[slave]        = (Step: 10, Min: 10, Max: 1000); // F2
session.PartialFillFractionForCtid[slave] = 0.6;                            // F1 / G5
session.RejectReasonForCtid[slave]        = CtraderRejectReason.NotEnoughMoney; // F12 (one-shot)
session.InvalidateToken(slave);                                             // F13
```

## Charakterizace + conformance (plánováno, udržuje fake ≡ reálný)

Dva mechanismy udržují fake čestný proti se měnícímu reálnému serveru (sledováno, přistává napříč Fáze 0a):

1. **Live characterization** (`LiveApiCharacterization`, demo účty, secrets-gated, `Inconclusive` při uzavřeném trhu): řídí reálné Open API, zaznamenává přesnou wire pravdu (sekvence událostí, škálování, reject kódy) do golden fixtures checknutých do testovacího projektu. Žádná tajemství ve fixtures — pouze pozorované tvary.
2. **Conformance harness**: spouští *stejnou* sadu scénářů dvakrát — jednou proti `FakeTradingSession`, jednou proti live session (když jsou tajemství přítomna) — Assertuje identické pozorovatelné výstupy. Změna reálného serveru → live noha selže → aktualizovat fake. To činí "unit testy pokrývají vše" důvěryhodným.

Live přihlašovací údaje: `secrets/dev-credentials.local.json` (nebo legacy split soubory) — viz `docs/testing/dev-credentials.md`.
