---
description: "Spustit přesně co byl zkopírován, v jaké pořadí, jaké velikosti, jaké slippage, a proč skipped. Auditovatelný na pix."
---

# Execution transparency

Kopírování je důvěrný. Každý trader musí vidět co se stalo na jejich účtech.

## Executor report

`GET /api/copy/profiles/{id}/activity` — ordered log recent copies:

```json
{
  "entries": [
    {
      "timestamp": "2025-07-13T10:30:42Z",
      "destinationId": "...",
      "action": "OrderPlaced",
      "masterOrderId": "12345",
      "slaveOrderId": "67890",
      "symbol": "EURUSD",
      "side": "BUY",
      "lots": 1.5,
      "price": 1.0902,
      "slippage": 0.3,
      "reason": "MarketOrderPlaced"
    },
    {
      "action": "OrderSkipped",
      "reason": "TradingHours",
      "timestamp": "..."
    }
  ]
}
```

Pole:
- `action` — OrderPlaced, PartialClosed, Protected, Skipped, Failed
- `reason` — why (ManageOnly, TradingHours, lot_sanity, rejection_breaker, slippage_filter, atd.)
- `slippage` — actual pip moved

## Audit trail

Všechny akce zaznamenány v `LogMessages` (structured logs). Owner/trader vidí:

- Co bylo kopírováno (symbol, lot, price)
- Kdy (UTC timestamp s resolution ms)
- Proč skipped (trading-hour window, symbol-filter, slippage guard, atd.)

Spolu s copy logging, engine emituje **OpenTelemetry metrics** — latency, slippage, placed vs skipped rate. Live dashboards / alerty.

## Compliance

Veřejný audit trail + email notifications na action = regulátor happy.

Viz [features/copy-trading.md](copy-trading.md) § Auditovatelnost.
