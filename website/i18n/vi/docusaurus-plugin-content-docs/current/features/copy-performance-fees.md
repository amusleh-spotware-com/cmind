---
description: "Money-manager performance fees on a high-water-mark, the standard copy-trading model (cTrader Copy, Darwinex, ZuluTrade profit-share): a provider charges…"
---

# Copy performance fees (Phase 4)

Money-manager **performance fees on a high-water-mark**, the standard copy-trading model (cTrader Copy,
Darwinex, ZuluTrade profit-share): a provider charges một phần trăm của *new* profit above each follower's
peak equity — không bao giờ trên opening balance, và không bao giờ hai lần cho cùng một ground đã recovered. **Opt-in** via
`App:Copy:FeesEnabled` (off by default).

## The model (high-water-mark)

Per destination (follower account), each settlement:

1. **First settlement** seeds the high-water-mark (HWM) at current equity → no charge (a follower is
   never billed on their deposit).
2. **New high** (equity > HWM): `fee = performanceFeePercent × (equity − HWM)`, rồi `HWM ← equity`.
3. **At or below the peak**: no fee, HWM unchanged — follower phải recover past the old peak trước, vì vậy
   they are never charged twice for the same gains.

Fee arithmetic là một domain invariant trên `CopyDestination.SettleFee(equity)` — aggregate owns it; settlement service only supplies polled equity và records returned amount. `PerformanceFee` là một
value object capped at 50% nên một misconfiguration không thể charge away a follower's whole gain.

## How it settles

```
CopyFeeSettlementService (BackgroundService, only when FeesEnabled)
   │  every App:Copy:FeeSettlementInterval
   ├─ load running profiles with a fee-configured destination
   ├─ ICopyEquityReader.ReadEquityAsync(ctid)   ← OpenApiCopyEquityReader opens a session,
   │                                               computes balance + floating P&L (PropFirmEquityCalculator)
   ├─ destination.SettleFee(equity)             ← HWM logic on the aggregate
   └─ persist advanced HWM + append CopyFeeAccrual (only on a new high)
```

- `ICopyEquityReader` là một Core abstraction; live implementation (`OpenApiCopyEquityReader`) là only
  infra piece — vì vậy settlement + HWM logic được exercise in tests với a fake reader, no live broker.
- `CopyFeeAccrual` là một append-only log (HWM-before, equity, fee %, fee amount, settled-at) — một fact log cho
  fee report và billing, không phải aggregate.

## Configuration & API

| `App:Copy` setting | Default | Effect |
|--------------------|---------|--------|
| `FeesEnabled` | `false` | Run the settlement service. |
| `FeeSettlementInterval` | `1h` | How often equity is polled và fees settled. |

Per-destination: `PerformanceFeePercent` (0–50) được set on destination (add/edit destination request).

- `GET /api/copy/profiles/{id}/fees` — profile's fee accruals + total charged.

## Tests

- **Unit** (`CopyPerformanceFeeTests`) — HWM invariant: first settlement seeds + charges nothing; a new
  high charges only the gain above the peak; at/below the peak charges nothing và peak never retreats;
  after a drawdown only recovery past old peak is charged; 0% never charges; VO rejects
  out-of-range percents.
- **Integration** (`CopyFeeSettlementTests`, real Postgres, fake equity reader) — seed→10k (no charge, mark
  seeded), 12k (charges 400, mark advances), 11k (no charge, mark held); accrual persisted với right
  owner/amount.

Copy host untouched by fees (settlement là một separate DB job), vì vậy copy DST stress suite unaffected (23/23).
