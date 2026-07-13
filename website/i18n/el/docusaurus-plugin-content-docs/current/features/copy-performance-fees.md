---
description: "Money-manager performance fees on a high-water-mark, the standard copy-trading model (cTrader Copy, Darwinex, ZuluTrade profit-share): a provider charges…"
---

# Copy performance fees (Phase 4)

Money-manager **performance fees σε high-water-mark**, το standard copy-trading model (cTrader Copy,
Darwinex, ZuluTrade profit-share): ένας provider χρεώνει ένα ποσοστό του *νέου* profit πάνω από το peak equity κάθε follower — ποτέ όχι σε opening balance, και ποτέ δυο φορές για ground που ήδη ανακτήθηκε. **Opt-in** μέσω
`App:Copy:FeesEnabled` (off by default).

## Το model (high-water-mark)

Per destination (follower account), κάθε settlement:

1. **First settlement** seeds το high-water-mark (HWM) στο current equity → χωρίς χρέωση (ένας follower δεν χρεώνεται ποτέ σε deposit).
2. **New high** (equity > HWM): `fee = performanceFeePercent × (equity − HWM)`, τότε `HWM ← equity`.
3. **At or below the peak**: χωρίς χρέωση, HWM unchanged — ο follower πρέπει πρώτα να ανακάμψει πέρα από το παλιό peak, ώστε δεν χρεώνεται δυο φορές για τα ίδια gains.

Η fee arithmetic είναι ένα domain invariant στο `CopyDestination.SettleFee(equity)` — το aggregate κατέχει αυτό; το
settlement service μόνο παραδίδει το polled equity και records το returned amount. `PerformanceFee` είναι ένα
value object capped στο 50% ώστε ένα misconfiguration δεν μπορεί να χρεώσει μακριά ολόκληρο gain ενός follower.

## Πώς κάνει settlement

```
CopyFeeSettlementService (BackgroundService, μόνο όταν FeesEnabled)
   │  κάθε App:Copy:FeeSettlementInterval
   ├─ load running profiles με ένα fee-configured destination
   ├─ ICopyEquityReader.ReadEquityAsync(ctid)   ← OpenApiCopyEquityReader ανοίγει session,
   │                                               υπολογίζει balance + floating P&L (PropFirmEquityCalculator)
   ├─ destination.SettleFee(equity)             ← HWM logic στο aggregate
   └─ persist advanced HWM + append CopyFeeAccrual (μόνο σε νέο high)
```

- `ICopyEquityReader` είναι Core abstraction; η live implementation (`OpenApiCopyEquityReader`) είναι το μόνο
  infra piece — ώστε το settlement + HWM logic ασκείται σε tests με ένα fake reader, χωρίς live broker.
- `CopyFeeAccrual` είναι append-only log (HWM-before, equity, fee %, fee amount, settled-at) — ένα fact log για
  το fee report και billing, όχι aggregate.

## Configuration & API

| `App:Copy` setting | Default | Effect |
|--------------------|---------|--------|
| `FeesEnabled` | `false` | Τρέξε το settlement service. |
| `FeeSettlementInterval` | `1h` | Πόσο συχνά το equity polled και fees settled. |

Per-destination: `PerformanceFeePercent` (0–50) ορίστηκε στο destination (add/edit destination request).

- `GET /api/copy/profiles/{id}/fees` — το profile's fee accruals + total charged.

## Tests

- **Unit** (`CopyPerformanceFeeTests`) — το HWM invariant: first settlement seeds + χρεώνει τίποτα; ένα νέο
  high χρεώνει μόνο το gain πάνω από το peak; at/below το peak χρεώνει τίποτα και το peak ποτέ δεν κάνει retreat;
  μετά από drawdown μόνο η recovery πέρα από το παλιό peak χρεώνεται; 0% ποτέ δεν χρεώνει; το VO απορρίπτει
  out-of-range percents.
- **Integration** (`CopyFeeSettlementTests`, real Postgres, fake equity reader) — seed→10k (χωρίς charge, mark
  seeded), 12k (charges 400, mark advances), 11k (χωρίς charge, mark held); accrual persisted με το σωστό
  owner/amount.

Το copy host δεν αγγίζεται από fees (settlement είναι ένα separate DB job), ώστε το copy DST stress suite δεν
επηρεάζεται (23/23).
