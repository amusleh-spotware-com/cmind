---
description: "Money-manager performance fees on a high-water-mark, the standard copy-trading model (cTrader Copy, Darwinex, ZuluTrade profit-share): a provider charges…"
---

# Copy performance fees (Phase 4)

Money-manager **performance fees on a high-water-mark**, the standard copy-trading model (cTrader Copy,
Darwinex, ZuluTrade profit-share): a provider charges a percentage of *new* profit above each follower's
peak equity — never on the opening balance, and never twice for ground already recovered. **Opt-in** via
`App:Copy:FeesEnabled` (off by default).

## The model (high-water-mark)

Per destination (follower account), each settlement:

1. **First settlement** seeds the high-water-mark (HWM) at the current equity → no charge (a follower is
   never billed on their deposit).
2. **New high** (equity > HWM): `fee = performanceFeePercent × (equity − HWM)`, then `HWM ← equity`.
3. **At or below the peak**: no fee, HWM unchanged — the follower must first recover past the old peak, so
   they are never charged twice for the same gains.

The fee arithmetic is a domain invariant on `CopyDestination.SettleFee(equity)` — the aggregate owns it; the
settlement service only supplies the polled equity and records the returned amount. `PerformanceFee` is a
value object capped at 50% so a misconfiguration can't charge away a follower's whole gain.

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

- `ICopyEquityReader` is a Core abstraction; the live implementation (`OpenApiCopyEquityReader`) is the only
  infra piece — so the settlement + HWM logic is exercised in tests with a fake reader, no live broker.
- `CopyFeeAccrual` is an append-only log (HWM-before, equity, fee %, fee amount, settled-at) — a fact log for
  the fee report and billing, not an aggregate.

## Configuration & API

| `App:Copy` setting | Default | Effect |
|--------------------|---------|--------|
| `FeesEnabled` | `false` | Run the settlement service. |
| `FeeSettlementInterval` | `1h` | How often equity is polled and fees settled. |

Per-destination: `PerformanceFeePercent` (0–50) is set on the destination (add/edit destination request).

- `GET /api/copy/profiles/{id}/fees` — the profile's fee accruals + total charged.

## Tests

- **Unit** (`CopyPerformanceFeeTests`) — the HWM invariant: first settlement seeds + charges nothing; a new
  high charges only the gain above the peak; at/below the peak charges nothing and the peak never retreats;
  after a drawdown only the recovery past the old peak is charged; 0% never charges; the VO rejects
  out-of-range percents.
- **Integration** (`CopyFeeSettlementTests`, real Postgres, fake equity reader) — seed→10k (no charge, mark
  seeded), 12k (charges 400, mark advances), 11k (no charge, mark held); accrual persisted with the right
  owner/amount.

The copy host is untouched by fees (settlement is a separate DB job), so the copy DST stress suite is
unaffected (23/23).
