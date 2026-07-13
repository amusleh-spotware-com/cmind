---
title: FakeTradingSession
description: "A cTrader szolgáltatásait szimulálja determinisztikusan - számla, pozíciók, orderek, tick-ek - a tesztek reprodukálhatók és a CI-ben zöldek."
---

# FakeTradingSession

A `FakeTradingSession` a cTrader szolgáltatásait szimulálja determinisztikusan - számla, pozíciók, orderek, tick-ek - a tesztek reprodukálhatók és a CI-ben zöldek.

## Mit szimulál

- **`IOpenApiClient`** - teljes Open API híváskészlet: `GetAccountInfo`, `GetOpenPositions`, `GetPendingOrders`, `GetOrdersHistory`, `PlaceOrder`, `CancelOrder`, `AmendOrder`, `AmendPosition`.
- **`ISubscribeClient`** - SignalR subscribe: `PositionOpened`, `PositionClosed`, `OrderPlaced`, `OrderFilled`, `OrderCancelled`, `BalanceChanged`, `SubscribePositions`, `SubscribeOrders`.
- **`ITradingInfoProvider`** - `GetSymbols`, `GetAsset`, `GetMarginInfo`.

## Seedelés

```csharp
var session = new FakeTradingSession(seed: 42);
// vagy
var session = new FakeTradingSession(trades: new List<Trade>(), nextOrderId: 1);
```

Fix seed → fix ID-sorozat, determinisztikus tick-generálás, determinisztikus fill-idők. A `CopyEngineHost` ugyanezt az `IClock`-t használja, szóval az idempotens-true-up és a lejárati ellenőrzések pontosan kiszámíthatók.

## Pozíciók szimulálása

```csharp
session.AddPosition(new Position { Symbol = "EURUSD", Quantity = 100_000, Side = Side.Buy, ... });
session.AddOrder(new PendingOrder { Symbol = "EURUSD", OrderType = OrderType.StopLimit, ... });
```

Minden szimulált pozícióhoz/orderhez a `FakeSubscribeClient` SignalR-eseményt bocsát ki a valódi `SubscribeClient` mintájára, így a `CopyEngineHost` nem tudja megkülönböztetni a hamis és a valódi munkamenetet.

## Fill-ek szimulálása

```csharp
session.SimulateFill(orderId: 1, price: 1.1200, quantity: 50_000);
```

Egy részleges vagy teljes fill-t szimulál, és mindkét oldalon SignalR-eseményt bocsát ki: `OrderFilled` + `PositionUpdated`.

## Ellenőrzés

```csharp
session.VerifyOrderPlaced(order => order.Symbol == "EURUSD" && order.Quantity == 100_000);
session.VerifyOrderCancelled(orderId: 1);
session.VerifyNoRemainingOrders();
```

A `Verify` metódusok a szokásos Moq/NSubstitute Assertions. Minden elvárt hívásnak meg kell történnie a `Dispose`-ig.

## cTrader-faithful

A `FakeTradingSession` **nem gyengíti a szimulátort** és a teszteket nem gyengíti, hogy a CI-t átengedjék. Ha a teszt red, a `FakeTradingSession` kerül javításra - a teszt nem. A hamis munkamenet az `IFakeTradingSession` interfészen keresztül van beadva, szóval a valódi `IOpenApiClient`/`ISubscribeClient` is beadható az integrációs tesztekhez.

## Korlátozások

- **Nincs piaci szimuláció** - a fill-eket manuálisan kell szimulálni.
- **Nincs margin ellenőrzés** - a teszt felelőssége a megfelelő margin-ellenőrzés a `FakeMarginInfoProvider`-rel.
- **Nincs multithread szimuláció** - a munkamenet nem szálbiztos; egy tesztben egyidejűleg csak egy szál használja.
