---
description: "DST (Deterministic Simulation Testing) — seeded randomized workloads + fault injection drive CopyEngineHost to convergence."
---

# Stress testing — DST

**DST (Deterministic Simulation Testing)** — seeded randomized workloads + fault injection drive `CopyEngineHost` tới convergence.

## Tại sao DST?

Unit tests check logic. Integration tests hit real DB. **DST checks emergent behavior** dưới fault storms:

- Socket flap (reconnect)
- Order rejection
- Market-range rejection
- Token rotation
- Node death

Everything deterministic (seeded) + reproducible (no real network/time).

## Chạy

```bash
dotnet test tests/StressTests --filter "NameOfTest"
```

Tests nằm trong `tests/StressTests/CopyTrading/`.

## Invariants

Mỗi DST scenario drives tới quiescence, sau đó asserts:

- No duplicate orders.
- No orphaned positions.
- Slave state ≡ master state (after resync).

## Instances found by DST

DST suite discovered + helped fix:

1. **Startup race** — `OnReconnected` wired trước initial load.
2. **Partial-fill true-up (G5)** — needed reconcile.
3. **Cross-cID token swap** — single valid token per cID.

Xem memory file: [../../MEMORY.md](../../MEMORY.md) — "Stress suite (DST) shipped".
