---
title: Fake trading session (in-memory simulator)
description: FakeTradingSession — cTrader-věrný deterministic simulator pro unit/integration/E2E testy bez API klíči nebo live credentials.
---

# Fake trading session

`FakeTradingSession` (`tests/UnitTests/CopyTrading/Fakes/`) = in-memory cTrader Open API simulator. Deterministický, bez network, cTrader-věrný behavior.

## Co to simuluje

- Account state (balance, equity, positions, orders)
- Order placement (validation, rejection, fill)
- Market data (bid/ask ticks)
- Position P&L revaluation
- Partial fills, expiry, slippage

## Jak se používá

```csharp
var session = new FakeTradingSession();
session.SetBalance(10000);
session.Market("EURUSD", bid: 1.0900, ask: 1.0905);
var result = await session.PlaceMarketOrderAsync(...);
Assert.True(result.IsSuccess);
```

Bez pověření, bez API klíče, reproducible seed.

## Veřejnost

Extends `IOpenApiTradingSession` — swap s real session v unit/int testy, stejné kód.

## Stability

cTrader behavior zpětně inženýr z API documentation + live testing. Extend pro nové behavior.

Viz `tests/UnitTests/CopyTrading/` pro příklady.
