---
description: "tests/UnitTests/CopyTrading/FakeTradingSession.cs = in-memory IOpenApiTradingSession na katerega tečejo vsi copy-trading unit testi. Naloga: posnemati realni cTrader Open API strežnik dovolj natančno, da unit testi pokrivajo vedenje, ki bi ga live tier komaj zaznal. Ta doc = fidelity pogodba: kaj fake modelira, kako zvesto, in pravilo ki jo ohranja pošteno."
---

# FakeTradingSession — fidelity pogodba cTrader Open API

`tests/UnitTests/CopyTrading/FakeTradingSession.cs` = in-memory `IOpenApiTradingSession` na katerega tečejo vsi copy-trading unit testi. Naloga: posnemati **realni cTrader Open API strežnik** dovolj natančno, da unit testi pokrivajo vedenje, ki bi ga live tier komaj zaznal. Ta doc = fidelity pogodba: kaj fake modelira, kako zvesto, in pravilo ki jo ohranja pošteno.

> **Vezavno pravilo (CLAUDE.md):** fake ostane cTrader-veren. **Razširite ga, ga nikoli ne oslabite** da bi testi potekli. Vsako novo realno vedenje na katerega se zanašate je tukaj modelirano, pripeto s fidelity testom.

## Fidelity matrika (F1–F13)

Sledi načrtu `plans/copy-trading-overhaul.md` §7.6. Legenda: ✅ modelirano · ◑ delno (opt-in / razširjajoče) · ⬜ še ne modelirano.

| # | Realno Open API vedenje | Fake status | Kako je modelirano |
|---|------------------------|-------------|-------------------|
| F1 | Market naročilo lahko **delno-napolni** | ◑ | `PartialFillFractionForCtid[ctid] = f` napolni samo `f×volume`; uskladitev nato kaže vrzel Phase-1 true-up (G5) zapre. Accept→fill par dogodkov še prihaja. |
| F2 | Volumen normaliziran na **korak**, zavrnjen pod **min** / nad **max** | ✅ | `VolumeBoundsForCtid[ctid] = (Step, Min, Max)` zaokroži navzdol na korak, vrže `CtraderRejectException(VolumeTooLow/High)`. |
| F3 | **Neveljaven SL/TP** zavrnjen (stran + števke) | ⬜ | Načrtovano Phase 0a/1 (skupaj z M6 SL/TP normalizacijo natančnosti). |
| F4 | Cene **celo-lestvicno narazen po števkah**; `pipPosition` | ◑ | `SymbolDetails` zdaj nosi `Digits` (in `MaxVolume`), napolnjeno iz pravega simbola; `PipPosition` poganja toleranco market-range, `Digits` poganja SL/TP normalizacijo natančnosti (M6). Popolna celo-lestvicna cena še čaka. |
| F5 | **Market-range** se napolni samo če je spot znotraj `base ± slippage`, sicer zavrni | ✅ | `IsMarketRangeRejected` primerja živi spot (`SetSpot`) s `baseSlippagePrice ± slippageInPoints`. Staro `RejectMarketRangeForCtid` zastavico še vedno sili zavrnitev. |
| F6 | **Čakajoč trigger→fill** dvojni dogodek (Naročilo nosi `positionId` + OPEN Pozicija) | ◑ | `PushOpen(..., orderId:)` reproducira napolnjeno-čakajoč dogodek; FX‑Blue/cMAM dvojno-copy dedupe pokrit v `CopyEngineHostTests.Filled_pending_does_not_double_open`. |
| F7 | **Strežnik-pogonjena zaprtja** (SL/TP zadeto, stop-out) | ⬜ | Danes zaprtje test-potrjeno (`PushClose`); cena-poganjjena SL/TP-zadeto + stop-out zaprtja načrtovana. |
| F8 | **Na račun** tabele / podrobnosti simbolov | ◑ | Imena/id-ji simbolov na fake; per-račun divergentne tabele (cross-broker) čakajo. |
| F9 | Polno **stanje računa** (balance, equity, margin, freeMargin) | ◑ | `Balance` + `LoadPositionValuationsAsync` (vstop/swap/provizija prek `SetPositionValuation`) + `SetSpot` vir real equity v sorazmerno-equity dimenzioniranje (G2, unit-testirano v `CopyEquitySizingTests`). Uporabljena marža ni razkrita od usklajevalnega API, torej prosta-marža poročana kot equity. |
| F10 | Dogodki nosijo **strežnikove časovne žige** | ✅ | `ExecutionEvent.ServerTimestamp` (unix ms) — real seja bere iz deal `ExecutionTimestamp`; `PushOpen`/`PushPending` sprejmeta `serverTimestamp:` tako da `FakeTimeProvider`-gnani test poganja real copy latency (G1). |
| F11 | **Trgovalni način / urnik** (onemogočen / samo-zapri / zaprto) | ⬜ | Načrtovano Phase 2b. |
| F12 | **Tipizirana taksonomija napak** (`ProtoOAErrorRes` kode) | ✅ | `RejectReasonForCtid[ctid] = CtraderRejectReason.X` vrže enkraten `CtraderRejectException(reason)` (NotEnoughMoney, MarketClosed, PositionNotFound, …). |
| F13 | **Razveljavitev žetona** — zastarelm žeton → napaka avtikacije | ✅ | `InvalidateToken(ctid)` označi priložen žeton zastarelm; trgovalni klici vržejo **resnično** `OpenApiException` z `OpenApiErrorKind.TokenInvalid` (koda `CH_ACCESS_TOKEN_INVALID`), natančno kot live strežnik, dokler `SwapAccessTokenAsync` ne namesti svežega žetona. Hrani M1 test robustnosti žetona. |

Fidelity testi živijo v `tests/UnitTests/CopyTrading/FakeTradingSessionFidelityTests.cs`.

## Opt-in, privzetki ohranjajo legacy vedenje

Vsak fidelity gumb **izključen privzeto** tako fake ohranja preprosto vedno-napolni vedenje za teste ki se ne zmenijo. Test opt-in per račun:

```csharp
session.VolumeBoundsForCtid[slave]        = (Step: 10, Min: 10, Max: 1000); // F2
session.PartialFillFractionForCtid[slave] = 0.6;                            // F1 / G5
session.RejectReasonForCtid[slave]        = CtraderRejectReason.NotEnoughMoney; // F12 (enkratnik)
session.InvalidateToken(slave);                                             // F13
```

## Karakterizacija + skladnost (načrtovano, ohranja fake ≡ real)

Dva mehanizma ohranjata fake poštenega proti premikajočemu se realnemu strežniku (sledeno, prihaja čez Phase 0a):

1. **Live karakterizacija** (`LiveApiCharacterization`, demo računi, skrivnost-varovano, `Inconclusive` na zaprtem trgu): poganja real Open API, snema natančno žično resnico (zaporedja dogodkov, lestvicenje, zavrnitvene kode) v goldene fiksture vtestni projekt. Brez skrivnosti v fiksturah — samo opazovane oblike.
2. **Skladnostni harness**: zažene *isto* scenarijsko zbirko dvakrat — enkrat proti `FakeTradingSession`, enkrat proti live seji (ko so skrivnosti prisotne) — trdi identične opazovane izide. Sprememba realnega strežnika → live noga propade → posodobi fake. To naredi "unit testi pokrivajo vse" verodostojno.

Live poverilnice: `secrets/dev-credentials.local.json` (ali stare razdeljene datoteke) — glej `docs/testing/dev-credentials.md`.
