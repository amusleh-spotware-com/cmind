using CTraderOpenApi;
using CTraderOpenApi.Client;
using UnitTests.CopyTrading;

namespace StressTests.CopyTrading;

// Thread-safe wrapper over the (single-threaded) FakeTradingSession. The stress workload mutates the
// fake book from the test thread while the CopyEngineHost reads and writes it from its own loop; a real
// Open API session tolerates concurrent calls, the fake does not. Every data operation is made atomic on
// one gate so there are no torn dictionary reads. Disconnect/Reconnect are deliberately NOT gated: the
// reconnect callback re-enters the host's own state gate to run a resync, and holding this gate across it
// would invert lock order against the host and deadlock. Per-operation atomicity is enough — the host's
// state gate already serialises resync against event handling.
internal sealed class SyncTradingSession(FakeTradingSession inner) : IOpenApiTradingSession
{
    private readonly object _gate = new();

    public ConnectionState State => inner.State;

    public Func<CancellationToken, Task>? OnReconnected
    {
        get => inner.OnReconnected;
        set => inner.OnReconnected = value;
    }

    // ---- test control surface ----------------------------------------------------------------------

    public void SetSpot(long symbolId, double bid, double ask) { lock (_gate) inner.SetSpot(symbolId, bid, ask); }

    public void SeedPosition(long ctid, long positionId, long symbolId, bool isBuy, long volume, string label)
    { lock (_gate) inner.SeedPosition(ctid, positionId, symbolId, isBuy, volume, label); }

    public void PushOpen(long ctid, long positionId, long symbolId, bool isBuy, long volume)
    { lock (_gate) inner.PushOpen(ctid, positionId, symbolId, isBuy, volume); }

    public void PushClose(long ctid, long positionId, long symbolId, bool isBuy, long volume)
    { lock (_gate) inner.PushClose(ctid, positionId, symbolId, isBuy, volume); }

    public void SetFailOrders(long ctid, bool fail)
    {
        lock (_gate)
        {
            if (fail) inner.FailOrdersForCtid.Add(ctid);
            else inner.FailOrdersForCtid.Remove(ctid);
        }
    }

    public void SetRejectMarketRange(long ctid) { lock (_gate) inner.RejectMarketRangeForCtid.Add(ctid); }

    public bool IsFailing(long ctid) { lock (_gate) return inner.FailOrdersForCtid.Contains(ctid); }

    public int SwapCount { get { lock (_gate) return inner.Swaps.Count; } }

    public string? CurrentToken(long ctid) { lock (_gate) return inner.CurrentToken(ctid); }

    public void Disconnect() => inner.Disconnect();

    public Task ReconnectAsync(CancellationToken ct) => inner.ReconnectAsync(ct);

    // ---- IOpenApiTradingSession (all gated) --------------------------------------------------------

    public void AttachAccount(long ctid, string accessToken) { lock (_gate) inner.AttachAccount(ctid, accessToken); }

    public Task SwapAccessTokenAsync(long ctid, string accessToken, CancellationToken ct)
    { lock (_gate) return inner.SwapAccessTokenAsync(ctid, accessToken, ct); }

    public Task StartAsync(CancellationToken ct) { lock (_gate) return inner.StartAsync(ct); }

    public Task<double> LoadBalanceAsync(long ctid, CancellationToken ct)
    { lock (_gate) return inner.LoadBalanceAsync(ctid, ct); }

    public Task<IReadOnlyList<PositionValuation>> LoadPositionValuationsAsync(long ctid, CancellationToken ct)
    { lock (_gate) return inner.LoadPositionValuationsAsync(ctid, ct); }

    // Channel-backed and independently thread-safe; no gate, no awaits held.
    public IAsyncEnumerable<ExecutionEvent> SourceExecutionsAsync(long ctid, CancellationToken ct)
        => inner.SourceExecutionsAsync(ctid, ct);

    public Task<IReadOnlyDictionary<string, long>> LoadSymbolIdsAsync(long ctid, CancellationToken ct)
    { lock (_gate) return inner.LoadSymbolIdsAsync(ctid, ct); }

    public Task<IReadOnlyDictionary<long, string>> LoadSymbolNamesAsync(long ctid, CancellationToken ct)
    { lock (_gate) return inner.LoadSymbolNamesAsync(ctid, ct); }

    public Task SendMarketOrderAsync(long ctid, long symbolId, bool isBuy, long volume, string label,
        CancellationToken ct, int? slippageInPoints = null, double? baseSlippagePrice = null)
    { lock (_gate) return inner.SendMarketOrderAsync(ctid, symbolId, isBuy, volume, label, ct, slippageInPoints, baseSlippagePrice); }

    public Task SendPendingOrderAsync(long ctid, long symbolId, bool isBuy, long volume,
        CopyOrderKind kind, double price, string label, CancellationToken ct,
        long? expirationTimestamp = null, int? slippageInPoints = null,
        double? stopLoss = null, double? takeProfit = null,
        long? relativeStopLoss = null, long? relativeTakeProfit = null, bool trailingStopLoss = false)
    { lock (_gate) return inner.SendPendingOrderAsync(ctid, symbolId, isBuy, volume, kind, price, label, ct, expirationTimestamp, slippageInPoints, stopLoss, takeProfit, relativeStopLoss, relativeTakeProfit, trailingStopLoss); }

    public Task AmendPendingOrderAsync(long ctid, long orderId, CopyOrderKind kind, long volume, double price,
        long? expirationTimestamp, int? slippageInPoints, CancellationToken ct,
        double? stopLoss = null, double? takeProfit = null,
        long? relativeStopLoss = null, long? relativeTakeProfit = null, bool trailingStopLoss = false)
    { lock (_gate) return inner.AmendPendingOrderAsync(ctid, orderId, kind, volume, price, expirationTimestamp, slippageInPoints, ct, stopLoss, takeProfit, relativeStopLoss, relativeTakeProfit, trailingStopLoss); }

    public Task<(double Bid, double Ask)> LoadSpotPriceAsync(long ctid, long symbolId, CancellationToken ct)
    { lock (_gate) return inner.LoadSpotPriceAsync(ctid, symbolId, ct); }

    public Task CancelOrderAsync(long ctid, long orderId, CancellationToken ct)
    { lock (_gate) return inner.CancelOrderAsync(ctid, orderId, ct); }

    public Task ClosePositionAsync(long ctid, long positionId, long volume, CancellationToken ct)
    { lock (_gate) return inner.ClosePositionAsync(ctid, positionId, volume, ct); }

    public Task AmendPositionSltpAsync(long ctid, long positionId, double? stopLoss, double? takeProfit,
        bool trailingStopLoss, CancellationToken ct)
    { lock (_gate) return inner.AmendPositionSltpAsync(ctid, positionId, stopLoss, takeProfit, trailingStopLoss, ct); }

    public Task<IReadOnlyList<OpenPositionSnapshot>> ReconcileAsync(long ctid, CancellationToken ct)
    { lock (_gate) return inner.ReconcileAsync(ctid, ct); }

    public Task<IReadOnlyList<PendingOrderSnapshot>> ReconcilePendingOrdersAsync(long ctid, CancellationToken ct)
    { lock (_gate) return inner.ReconcilePendingOrdersAsync(ctid, ct); }

    public Task<IReadOnlyList<SymbolDetails>> LoadSymbolDetailsAsync(
        long ctid, IReadOnlyCollection<long> symbolIds, CancellationToken ct)
    { lock (_gate) return inner.LoadSymbolDetailsAsync(ctid, symbolIds, ct); }

    public ValueTask DisposeAsync() => inner.DisposeAsync();
}

internal sealed class SyncTradingSessionFactory(SyncTradingSession session) : IOpenApiTradingSessionFactory
{
    public IOpenApiTradingSession Create(bool live, string clientId, string clientSecret) => session;
}
