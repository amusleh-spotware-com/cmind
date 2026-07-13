---
description: "Money-manager poplatky za výkon na high-water-mark, standardní copy-trading model (cTrader Copy, Darwinex, ZuluTrade profit-share): provider účtuje…"
---

# Copy poplatky za výkon (Fáze 4)

Money-manager **poplatky za výkon na high-water-mark**, standardní copy-trading model (cTrader Copy,
Darwinex, ZuluTrade profit-share): provider účtuje procento *nového* zisku nad peak equity každého followera
— nikdy z opening balance, a nikdy dvakrát za stejnou půdu. **Opt-in** přes
`App:Copy:FeesEnabled` (off by default).

## Model (high-water-mark)

Per destinace (follower účet), každé settlement:

1. **First settlement** seeduje high-water-mark (HWM) na current equity → žádný poplatek (follower
   nikdy není fakturován ze svého vkladu).
2. **New high** (equity > HWM): `fee = performanceFeePercent × (equity − HWM)`, pak `HWM ← equity`.
3. **At or below the peak**: žádný poplatek, HWM nezměněna — follower musí nejprve recover past old peak, takže
   mu nikdy není účtováno dvakrát za stejné zisky.

Fee arithmetic je domain invariant na `CopyDestination.SettleFee(equity)` — aggregate to vlastní; settlement service pouze dodává polled equity a zaznamenává vrácenou částku. `PerformanceFee` je value object capped at 50% takže misconfiguration nemůže účtovat away celý zisk followera.

## Jak se settlement provádí

```
CopyFeeSettlementService (BackgroundService, pouze když FeesEnabled)
   │  každý App:Copy:FeeSettlementInterval
   ├─ load running profiles s fee-konfigurovanou destinací
   ├─ ICopyEquityReader.ReadEquityAsync(ctid)   ← OpenApiCopyEquityReader otevře session,
   │                                               spočítá balance + floating P&L (PropFirmEquityCalculator)
   ├─ destination.SettleFee(equity)             ← HWM logika na aggregate
   └─ persist advanced HWM + append CopyFeeAccrual (pouze na new high)
```

- `ICopyEquityReader` je Core abstrakce; live implementation (`OpenApiCopyEquityReader`) je jediný
  infra piece — takže settlement + HWM logika je exercisována v testech s fake reader, žádný live broker.
- `CopyFeeAccrual` je append-only log (HWM-before, equity, fee %, fee amount, settled-at) — fact log for
  fee report and billing, not aggregate.

## Konfigurace & API

| `App:Copy` nastavení | Default | Efekt |
|--------------------|---------|--------|
| `FeesEnabled` | `false` | Spustit settlement service. |
| `FeeSettlementInterval` | `1h` | Jak často je equity polled a poplatky settled. |

Per-destination: `PerformanceFeePercent` (0–50) je nastaveno na destinaci (add/edit destination request).

- `GET /api/copy/profiles/{id}/fees` — fee accruals + total charged pro profil.

## Testy

- **Unit** (`CopyPerformanceFeeTests`) — HWM invariant: first settlement seeds + charges nothing; new
  high účtuje pouze zisk nad peak; at/below peak účtuje nothing a peak se nikdy neustoupí;
  after drawdown pouze recovery past old peak je účtována; 0% nikdy neúčtuje; VO rejects
  out-of-range percents.
- **Integration** (`CopyFeeSettlementTests`, real Postgres, fake equity reader) — seed→10k (no charge, mark
  seeded), 12k (účtuje 400, mark advances), 11k (no charge, mark held); accrual persisted s right
  owner/amount.

Copy host zůstává nedotčen poplatky (settlement je samostatný DB job), takže copy DST stress suite je
unaffected (23/23).
