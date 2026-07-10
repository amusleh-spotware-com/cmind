using System.Threading.Channels;
using CTraderOpenApi;
using CTraderOpenApi.Client;
using Microsoft.Extensions.Logging;

namespace UnitTests.CopyTrading;

// Captures log records so tests can assert the copy audit trail fires.
internal sealed class CapturingLogger : ILogger
{
    public List<(int EventId, string Message)> Records { get; } = [];

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
        => Records.Add((eventId.Id, formatter(state, exception)));

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose() { }
    }
}

// Deterministic in-memory IOpenApiTradingSession for CopyEngineHost tests. Models symbols, balances,
// movable bid/ask spot, per-account open positions and pending orders (with expiry + market-range /
// stop-limit slippage), records every order/close/amend/cancel/token-swap the host issues, and lets a
// test push source execution events, drop/restore the socket to force a resync, and expire resting
// orders on a virtual clock. No network, fully synchronous state — a faithful cTrader stand-in.
internal sealed class FakeTradingSession : IOpenApiTradingSession
{
    public sealed record OrderCall(long Ctid, long SymbolId, bool IsBuy, long Volume, string Label,
        int? SlippageInPoints = null, double? BaseSlippagePrice = null);
    public sealed record PendingCall(long Ctid, long SymbolId, bool IsBuy, long Volume, CopyOrderKind Kind, double Price,
        string Label, long? ExpirationTimestamp = null, int? SlippageInPoints = null);
    public sealed record AmendPendingCall(long Ctid, long OrderId, CopyOrderKind Kind, long Volume, double Price,
        long? ExpirationTimestamp, int? SlippageInPoints);
    public sealed record CancelCall(long Ctid, long OrderId);
    public sealed record CloseCall(long Ctid, long PositionId, long Volume);
    public sealed record AmendCall(long Ctid, long PositionId, double? StopLoss, double? TakeProfit, bool Trailing);
    public sealed record SwapCall(long Ctid, string Token);

    private readonly Channel<ExecutionEvent> _source =
        Channel.CreateUnbounded<ExecutionEvent>(new UnboundedChannelOptions { SingleReader = true });
    private readonly Dictionary<long, List<OpenPositionSnapshot>> _positions = new();
    private readonly Dictionary<long, List<PendingOrderSnapshot>> _pendingOrders = new();
    private readonly Dictionary<long, string> _accountTokens = new();
    private readonly Dictionary<long, (double Bid, double Ask)> _spots = new();
    private readonly Dictionary<long, long?> _sourceOrderExpiry = new();
    private readonly Dictionary<long, string> _symbolNames;
    private readonly Dictionary<string, long> _symbolIds;
    private readonly SymbolDetails _details;
    private long _positionSeq = 5000;
    private long _orderSeq = 8000;

    public List<OrderCall> Orders { get; } = [];
    public List<PendingCall> Pendings { get; } = [];
    public List<AmendPendingCall> AmendedPendings { get; } = [];
    public List<CancelCall> Cancels { get; } = [];
    public List<CloseCall> Closes { get; } = [];
    public List<AmendCall> Amends { get; } = [];
    public List<SwapCall> Swaps { get; } = [];
    public HashSet<long> FailOrdersForCtid { get; } = [];
    public HashSet<long> RejectMarketRangeForCtid { get; } = [];

    public FakeTradingSession(
        IReadOnlyDictionary<long, string> symbolNames,
        IReadOnlyDictionary<string, long> symbolIds,
        SymbolDetails details)
    {
        _symbolNames = new Dictionary<long, string>(symbolNames);
        _symbolIds = new Dictionary<string, long>(symbolIds, StringComparer.OrdinalIgnoreCase);
        _details = details;
    }

    public ConnectionState State { get; private set; } = ConnectionState.Connected;
    public Func<CancellationToken, Task>? OnReconnected { get; set; }

    public void AttachAccount(long ctidTraderAccountId, string accessToken) => _accountTokens[ctidTraderAccountId] = accessToken;

    public Task SwapAccessTokenAsync(long ctidTraderAccountId, string accessToken, CancellationToken ct)
    {
        Swaps.Add(new SwapCall(ctidTraderAccountId, accessToken));
        _accountTokens[ctidTraderAccountId] = accessToken;
        return Task.CompletedTask;
    }

    public string? CurrentToken(long ctidTraderAccountId)
        => _accountTokens.TryGetValue(ctidTraderAccountId, out var token) ? token : null;

    public Task StartAsync(CancellationToken ct) => Task.CompletedTask;

    // ---- test control surface -------------------------------------------------------------------

    public void SetSpot(long symbolId, double bid, double ask) => _spots[symbolId] = (bid, ask);

    public void Disconnect() => State = ConnectionState.Disconnected;

    public async Task ReconnectAsync(CancellationToken ct = default)
    {
        State = ConnectionState.Connected;
        if (OnReconnected is not null) await OnReconnected(ct);
    }

    public void ExpireDuePendingOrders(long nowUnixMs)
    {
        foreach (var (orderId, expiry) in _sourceOrderExpiry.Where(e => e.Value is { } v && v <= nowUnixMs).ToArray())
        {
            _sourceOrderExpiry.Remove(orderId);
            _source.Writer.TryWrite(new ExecutionEvent(0, "ORDER_EXPIRED", 0, 0, false, 0, 0, null, null,
                IsOpen: false, OrderId: orderId, IsPendingOrder: true, IsOrderCancelled: true));
        }
    }

    public void PushOpen(long ctid, long positionId, long symbolId, bool isBuy, long volume,
        double? stopLoss = null, double? takeProfit = null, long orderId = 0, bool trailing = false,
        CopyOrderKind orderKind = CopyOrderKind.Market, int? slippageInPoints = null, double? baseSlippagePrice = null)
        => _source.Writer.TryWrite(new ExecutionEvent(ctid, "ORDER_FILLED", positionId, symbolId,
            isBuy, volume, 1.10, stopLoss, takeProfit, IsOpen: true, OrderId: orderId, TrailingStopLoss: trailing,
            OrderKind: orderKind, SlippageInPoints: slippageInPoints, BaseSlippagePrice: baseSlippagePrice));

    public void PushClose(long ctid, long positionId, long symbolId, bool isBuy, long volume)
        => _source.Writer.TryWrite(new ExecutionEvent(ctid, "ORDER_ACCEPTED", positionId, symbolId,
            isBuy, volume, 1.10, null, null, IsOpen: false));

    public void PushPending(long ctid, long orderId, long symbolId, bool isBuy, long volume,
        CopyOrderKind kind, double price, long? expirationTimestamp = null, int? slippageInPoints = null)
    {
        _sourceOrderExpiry[orderId] = expirationTimestamp;
        _source.Writer.TryWrite(new ExecutionEvent(ctid, "ORDER_ACCEPTED", 0, symbolId, isBuy, volume,
            price, null, null, IsOpen: false, OrderId: orderId, IsPendingOrder: true, OrderKind: kind,
            LimitPrice: kind is CopyOrderKind.Limit ? price : null,
            StopPrice: kind is CopyOrderKind.Stop or CopyOrderKind.StopLimit ? price : null,
            ExpirationTimestamp: expirationTimestamp, SlippageInPoints: slippageInPoints));
    }

    public void PushPendingReplaced(long ctid, long orderId, long symbolId, bool isBuy, long volume,
        CopyOrderKind kind, double price, long? expirationTimestamp = null, int? slippageInPoints = null)
        => _source.Writer.TryWrite(new ExecutionEvent(ctid, "OrderReplaced", 0, symbolId, isBuy, volume,
            price, null, null, IsOpen: false, OrderId: orderId, IsPendingOrder: true, OrderKind: kind,
            LimitPrice: kind is CopyOrderKind.Limit ? price : null,
            StopPrice: kind is CopyOrderKind.Stop or CopyOrderKind.StopLimit ? price : null,
            ExpirationTimestamp: expirationTimestamp, SlippageInPoints: slippageInPoints));

    public void PushPendingCancel(long ctid, long orderId, long symbolId, bool isBuy, long volume, CopyOrderKind kind)
    {
        _sourceOrderExpiry.Remove(orderId);
        _source.Writer.TryWrite(new ExecutionEvent(ctid, "ORDER_CANCELLED", 0, symbolId, isBuy, volume,
            1.10, null, null, IsOpen: false, OrderId: orderId, IsPendingOrder: true, IsOrderCancelled: true, OrderKind: kind));
    }

    public void SeedPosition(long ctid, long positionId, long symbolId, bool isBuy, long volume, string label,
        double? stopLoss = null, bool trailing = false)
        => PositionStore(ctid).Add(new OpenPositionSnapshot(positionId, symbolId, isBuy, volume, label, stopLoss, trailing));

    public void SeedPending(long ctid, long orderId, long symbolId, bool isBuy, long volume, CopyOrderKind kind, double price, string label)
        => PendingStore(ctid).Add(new PendingOrderSnapshot(orderId, symbolId, isBuy, volume, kind, price, label));

    public void CompleteSource() => _source.Writer.TryComplete();

    // ---- IOpenApiTradingSession ----------------------------------------------------------------

    public async IAsyncEnumerable<ExecutionEvent> SourceExecutionsAsync(
        long ctidTraderAccountId, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var e in _source.Reader.ReadAllAsync(ct))
            if (e.CtidTraderAccountId == ctidTraderAccountId || e.CtidTraderAccountId == 0)
                yield return e;
    }

    public Task<double> LoadBalanceAsync(long ctidTraderAccountId, CancellationToken ct) => Task.FromResult(10000.0);

    public Task<IReadOnlyDictionary<string, long>> LoadSymbolIdsAsync(long ctidTraderAccountId, CancellationToken ct)
        => Task.FromResult<IReadOnlyDictionary<string, long>>(_symbolIds);

    public Task<IReadOnlyDictionary<long, string>> LoadSymbolNamesAsync(long ctidTraderAccountId, CancellationToken ct)
        => Task.FromResult<IReadOnlyDictionary<long, string>>(_symbolNames);

    public Task<(double Bid, double Ask)> LoadSpotPriceAsync(long ctidTraderAccountId, long symbolId, CancellationToken ct)
        => Task.FromResult(_spots.TryGetValue(symbolId, out var spot) ? spot : (1.10, 1.10));

    public Task SendMarketOrderAsync(long ctidTraderAccountId, long symbolId, bool isBuy, long volume, string label,
        CancellationToken ct, int? slippageInPoints = null, double? baseSlippagePrice = null)
    {
        if (FailOrdersForCtid.Contains(ctidTraderAccountId))
            throw new InvalidOperationException("simulated order rejection");
        if (slippageInPoints.HasValue && RejectMarketRangeForCtid.Contains(ctidTraderAccountId))
            throw new InvalidOperationException("simulated market-range slippage rejection");
        Orders.Add(new OrderCall(ctidTraderAccountId, symbolId, isBuy, volume, label, slippageInPoints, baseSlippagePrice));
        PositionStore(ctidTraderAccountId).Add(new OpenPositionSnapshot(++_positionSeq, symbolId, isBuy, volume, label));
        return Task.CompletedTask;
    }

    public Task SendPendingOrderAsync(long ctidTraderAccountId, long symbolId, bool isBuy, long volume,
        CopyOrderKind kind, double price, string label, CancellationToken ct,
        long? expirationTimestamp = null, int? slippageInPoints = null)
    {
        if (FailOrdersForCtid.Contains(ctidTraderAccountId))
            throw new InvalidOperationException("simulated order rejection");
        Pendings.Add(new PendingCall(ctidTraderAccountId, symbolId, isBuy, volume, kind, price, label,
            expirationTimestamp, slippageInPoints));
        PendingStore(ctidTraderAccountId).Add(new PendingOrderSnapshot(++_orderSeq, symbolId, isBuy, volume, kind, price, label));
        return Task.CompletedTask;
    }

    public Task AmendPendingOrderAsync(long ctidTraderAccountId, long orderId, CopyOrderKind kind, long volume,
        double price, long? expirationTimestamp, int? slippageInPoints, CancellationToken ct)
    {
        AmendedPendings.Add(new AmendPendingCall(ctidTraderAccountId, orderId, kind, volume, price, expirationTimestamp, slippageInPoints));
        var store = PendingStore(ctidTraderAccountId);
        var index = store.FindIndex(o => o.OrderId == orderId);
        if (index >= 0) store[index] = store[index] with { Price = price, Volume = volume };
        return Task.CompletedTask;
    }

    public Task CancelOrderAsync(long ctidTraderAccountId, long orderId, CancellationToken ct)
    {
        Cancels.Add(new CancelCall(ctidTraderAccountId, orderId));
        PendingStore(ctidTraderAccountId).RemoveAll(o => o.OrderId == orderId);
        return Task.CompletedTask;
    }

    public Task ClosePositionAsync(long ctidTraderAccountId, long positionId, long volume, CancellationToken ct)
    {
        Closes.Add(new CloseCall(ctidTraderAccountId, positionId, volume));
        var store = PositionStore(ctidTraderAccountId);
        var index = store.FindIndex(p => p.PositionId == positionId);
        if (index >= 0)
        {
            var existing = store[index];
            if (volume >= existing.Volume) store.RemoveAt(index);
            else store[index] = existing with { Volume = existing.Volume - volume };
        }
        return Task.CompletedTask;
    }

    public Task AmendPositionSltpAsync(long ctidTraderAccountId, long positionId, double? stopLoss,
        double? takeProfit, bool trailingStopLoss, CancellationToken ct)
    {
        Amends.Add(new AmendCall(ctidTraderAccountId, positionId, stopLoss, takeProfit, trailingStopLoss));
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<OpenPositionSnapshot>> ReconcileAsync(long ctidTraderAccountId, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<OpenPositionSnapshot>>(PositionStore(ctidTraderAccountId).ToList());

    public Task<IReadOnlyList<PendingOrderSnapshot>> ReconcilePendingOrdersAsync(long ctidTraderAccountId, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<PendingOrderSnapshot>>(PendingStore(ctidTraderAccountId).ToList());

    public Task<IReadOnlyList<SymbolDetails>> LoadSymbolDetailsAsync(
        long ctidTraderAccountId, IReadOnlyCollection<long> symbolIds, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<SymbolDetails>>(
            symbolIds.Select(id => _details with { SymbolId = id }).ToList());

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private List<OpenPositionSnapshot> PositionStore(long ctid)
        => _positions.TryGetValue(ctid, out var list) ? list : _positions[ctid] = [];

    private List<PendingOrderSnapshot> PendingStore(long ctid)
        => _pendingOrders.TryGetValue(ctid, out var list) ? list : _pendingOrders[ctid] = [];
}

internal sealed class FakeTradingSessionFactory(FakeTradingSession session) : IOpenApiTradingSessionFactory
{
    public IOpenApiTradingSession Create(bool live, string clientId, string clientSecret) => session;
}
