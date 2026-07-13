---
description: "Test every failure mode — network loss, order rejection, token rotation, node death, market closure."
---

# Failure paths

Test mỗi failure mode — network loss, order rejection, token rotation, node death, market closure.

## Coverage

**Unit** (FakeTradingSession):
- Order rejection (NotEnoughMoney, VolumeTooHigh, etc.)
- Volume normalization.
- Market-range rejection.
- Partial fills.
- Token invalidation.

**Integration**:
- DB connection drop + retry.
- Postgres advisory lock (migration + seeding).
- Node lease expiry + reclaim.

**E2E / Live**:
- Network flap (socket reconnect).
- cTrader server 5xx → retry.
- Node death → failover.
- Market close → order rejection.

## Testing discipline

Never skip failure paths just because "looks unlikely" — live traders experience them.

DST (Deterministic Simulation Testing) injects every failure under controlled seed, asserts convergence.

Xem [stress-testing.md](./stress-testing.md) cho DST suite.
