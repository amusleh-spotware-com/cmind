---
description: "Poplatky za výkonnosť money-managera na high-water-mark, štandardný copy-trading model (cTrader Copy, Darwinex, ZuluTrade profit-share): poskytovateľ účtuje…"
---

# Copy performance fees (Fáza 4)

**Poplatky za výkonnosť money-managera na high-water-mark**, štandardný copy-trading model (cTrader Copy,
Darwinex, ZuluTrade profit-share): poskytovateľ účtuje percento z *nového* zisku nad peak equity každého sledovateľa
— nikdy na opening balance a nikdy dvakrát za tú istú získanú pôdu. **Opt-in** cez
`App:Copy:FeesEnabled` (vypnuté predvolene).

## Model (high-water-mark)

Per destinácia (sledovateľský účet), každé vyrovnanie:

1. **Prvé vyrovnanie** seedingne HWM na aktuálnu equity → žiadny poplatok (sledovateľovi sa
   nikdy neúčtuje jeho vklad).
2. **Nový peak** (equity > HWM): `fee = performanceFeePercent × (equity − HWM)`, potom `HWM ← equity`.
3. **Na alebo pod peak**: žiadny poplatok, HWM nezmenená — sledovateľ musí najprv zotaviť nad starý peak,
   takže mu nikdy nie je účtované dvakrát za rovnaké zisky.

Poplatková aritmetika je doménový invariant na `CopyDestination.SettleFee(equity)` — aggregate ho vlastní;
vyrovnávacia služba len dodáva polled equity a zaznamenáva vrátenú sumu. `PerformanceFee` je value object
capped na 50%, takže miskonfigurácia nemôže účtovať celý zisk sledovateľa.

## Ako sa vyrovnáva

```
CopyFeeSettlementService (BackgroundService, iba keď FeesEnabled)
   │  každý App:Copy:FeeSettlementInterval
   ├─ načíta bežiace profily s fee-nakonfigurovanou destináciou
   ├─ ICopyEquityReader.ReadEquityAsync(ctid)   ← OpenApiCopyEquityReader otvorí session,
   │                                               vypočíta balance + floating P&L (PropFirmEquityCalculator)
   ├─ destination.SettleFee(equity)             ← HWM logika na aggregate
   └─ perzistuje advanced HWM + append CopyFeeAccrual (iba na novom peak)
```

- `ICopyEquityReader` je Core abstrakcia; live implementácia (`OpenApiCopyEquityReader`) je jediný
  infra kúsok — takže settlement + HWM logika je cvičená v testoch s fake readerom, bez live brokera.
- `CopyFeeAccrual` je append-only log (HWM-before, equity, fee %, fee amount, settled-at) — fact log pre
  fee report a fakturáciu, nie aggregate.

## Konfigurácia & API

| `App:Copy` nastavenie | Predvolené | Efekt |
|--------------------|---------|--------|
| `FeesEnabled` | `false` | Spustiť settlement service. |
| `FeeSettlementInterval` | `1h` | Ako často sa equity polling a fee settlement robí. |

Per-destinácia: `PerformanceFeePercent` (0–50) sa nastavuje na destinácii (add/edit destination request).

- `GET /api/copy/profiles/{id}/fees` — fee accruals profilu + total charged.

## Testy

- **Jednotka** (`CopyPerformanceFeeTests`) — HWM invariant: prvé settlement seedingne + neúčtuje nič; nový
  peak účtuje iba zisk nad peak; na/ pod peak neúčtuje nič a peak sa nikdy nevráti;
  po drawdown iba zotavenie nad starý peak je účtované; 0% nikdy neúčtuje; VO odmieta
  out-of-range percentá.
- **Integrácia** (`CopyFeeSettlementTests`, reálny Postgres, fake equity reader) — seed→10k (žiadny poplatok,
  mark posunutý), 12k (účtuje 400, mark pokročil), 11k (žiadny poplatok, mark držaný); accrual
  perzistovaný so správnym owner/amount.

Copy host je nedotknutý poplatkami (settlement je samostatná DB job), takže copy DST stress suite je
nezávislá (23/23).
