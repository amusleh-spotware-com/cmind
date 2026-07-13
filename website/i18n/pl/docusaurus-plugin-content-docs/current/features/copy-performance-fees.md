---
description: "Money-manager performance fees na high-water-mark, standard copy-trading model (cTrader Copy, Darwinex, ZuluTrade profit-share): provider charges…"
---

# Copy performance fees (Phase 4)

Money-manager **performance fees na high-water-mark**, standard copy-trading model (cTrader Copy,
Darwinex, ZuluTrade profit-share): provider charges procent *new* profit ponad każdy follower's
peak equity — nigdy na opening balance, i nigdy twice dla ground już recovered. **Opt-in** via
`App:Copy:FeesEnabled` (off domyślnie).

## Model (high-water-mark)

Per destination (follower account), każdy settlement:

1. **First settlement** seeds high-water-mark (HWM) na current equity → no charge (follower jest
   nigdy billed na swoim deposit).
2. **New high** (equity > HWM): `fee = performanceFeePercent × (equity − HWM)`, wtedy `HWM ← equity`.
3. **At lub below peak**: no fee, HWM unchanged — follower musi first recover past old peak, więc
   nigdy nie są charged twice dla same gains.

Fee arithmetic to domain invariant na `CopyDestination.SettleFee(equity)` — aggregate owns to; settlement
service tylko supplies polled equity i records returned amount. `PerformanceFee` to
value object capped na 50% więc misconfiguration nie może charge away follower's whole gain.

## Jak settles

```
CopyFeeSettlementService (BackgroundService, tylko gdy FeesEnabled)
   │  każdy App:Copy:FeeSettlementInterval
   ├─ load running profiles z fee-configured destination
   ├─ ICopyEquityReader.ReadEquityAsync(ctid)   ← OpenApiCopyEquityReader otwiera session,
   │                                               computes balance + floating P&L (PropFirmEquityCalculator)
   ├─ destination.SettleFee(equity)             ← HWM logic na aggregate
   └─ persist advanced HWM + append CopyFeeAccrual (tylko na new high)
```

- `ICopyEquityReader` to Core abstraction; live implementation (`OpenApiCopyEquityReader`) to jedyny
  infra piece — więc settlement + HWM logic jest exercised w tests z fake reader, no live broker.
- `CopyFeeAccrual` to append-only log (HWM-before, equity, fee %, fee amount, settled-at) — fact log dla
  fee report i billing, nie aggregate.

## Konfiguracja & API

| `App:Copy` ustawienie | Domyślnie | Effect |
|--------------------|---------|--------|
| `FeesEnabled` | `false` | Run settlement service. |
| `FeeSettlementInterval` | `1h` | How often equity polled i fees settled. |

Per-destination: `PerformanceFeePercent` (0–50) set na destination (add/edit destination request).

- `GET /api/copy/profiles/{id}/fees` — profile's fee accruals + total charged.

## Testy

- **Unit** (`CopyPerformanceFeeTests`) — HWM invariant: first settlement seeds + charges nic; new
  high charges tylko gain above peak; at/below peak charges nic i peak nigdy nie retreats;
  after drawdown tylko recovery past old peak jest charged; 0% nigdy charges; VO rejects
  out-of-range percents.
- **Integracja** (`CopyFeeSettlementTests`, real Postgres, fake equity reader) — seed→10k (no charge, mark
  seeded), 12k (charges 400, mark advances), 11k (no charge, mark held); accrual persisted z right
  owner/amount.

Copy host jest untouched przez fees (settlement to separate DB job), więc copy DST stress suite jest
unaffected (23/23).
