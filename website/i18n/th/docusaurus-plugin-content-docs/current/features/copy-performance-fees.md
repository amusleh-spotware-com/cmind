---
description: "Money-manager performance fees on a high-water-mark, the standard copy-trading model (cTrader Copy, Darwinex, ZuluTrade profit-share): a provider charges…"
---

# Copy performance fees (Phase 4)

Money-manager **performance fees on high-water-mark** standard copy-trading model (cTrader Copy
Darwinex ZuluTrade profit-share): provider charges percentage ของ *new* profit above ทุก follower's
peak equity — ไม่เคยบน opening balance และ never twice สำหรับ ground already recovered **Opt-in** via
`App:Copy:FeesEnabled` (off by default)

## The model (high-water-mark)

Per destination (follower account) ทุก settlement:

1. **First settlement** seeds high-water-mark (HWM) ที่ current equity → ไม่มี charge (follower ไม่เคย
   billed on deposit ของพวกเขา)
2. **New high** (equity > HWM): `fee = performanceFeePercent × (equity − HWM)` จากนั้น `HWM ← equity`
3. **At หรือ below peak**: ไม่มี fee HWM unchanged — follower must first recover past old peak ดังนั้น
   พวกเขา ไม่เคย charged twice สำหรับ gains เดียวกัน

fee arithmetic เป็น domain invariant on `CopyDestination.SettleFee(equity)` — aggregate owns มัน; settlement
service เพียงsupplies polled equity และ records returned amount `PerformanceFee` value object
capped ที่ 50% ดังนั้น misconfiguration can't charge away follower ของ whole gain

## How มันsettle

```
CopyFeeSettlementService (BackgroundService only when FeesEnabled)
   │  ทุก App:Copy:FeeSettlementInterval
   ├─ load running profiles ด้วย fee-configured destination
   ├─ ICopyEquityReader.ReadEquityAsync(ctid)   ← OpenApiCopyEquityReader opens session
   │                                               computes balance + floating P&L (PropFirmEquityCalculator)
   ├─ destination.SettleFee(equity)             ← HWM logic on aggregate
   └─ persist advanced HWM + append CopyFeeAccrual (only on new high)
```

- `ICopyEquityReader` Core abstraction; live implementation (`OpenApiCopyEquityReader`) only
  infra piece — ดังนั้น settlement + HWM logic exercised ใน tests ด้วย fake reader no live broker
- `CopyFeeAccrual` append-only log (HWM-before equity fee % fee amount settled-at) — fact log สำหรับ
  fee report และ billing ไม่ใช่ aggregate

## Configuration & API

| `App:Copy` setting | Default | Effect |
|--------------------|---------|--------|
| `FeesEnabled` | `false` | Run settlement service |
| `FeeSettlementInterval` | `1h` | How often equity polled และ fees settled |

Per-destination: `PerformanceFeePercent` (0–50) set บน destination (add/edit destination request)

- `GET /api/copy/profiles/{id}/fees` — profile ของ fee accruals + total charged

## Tests

- **Unit** (`CopyPerformanceFeeTests`) — HWM invariant: first settlement seeds + charges nothing; new
  high charges เฉพาะ gain above peak; at/below peak charges nothing และ peak never retreats;
  หลัง drawdown only recovery past old peak charged; 0% never charges; VO rejects
  out-of-range percents
- **Integration** (`CopyFeeSettlementTests` real Postgres fake equity reader) — seed→10k (no charge mark
  seeded) 12k (charges 400 mark advances) 11k (no charge mark held); accrual persisted ด้วย right
  owner/amount

copy host untouched โดย fees (settlement separate DB job) ดังนั้น copy DST stress suite
unaffected (23/23)
