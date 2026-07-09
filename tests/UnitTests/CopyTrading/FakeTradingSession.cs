using System.Threading.Channels;
using CTraderOpenApi;
using CTraderOpenApi.Client;

namespace UnitTests.CopyTrading;

// Deterministic in-memory IOpenApiTradingSession for CopyEngineHost tests. Models symbols, balances
// and per-account open positions; records the orders/closes/amends the host issues; and lets a test
// push source execution events. No network, fully synchronous state.
internal sealed class FakeTradingSession : IOpenApiTradingSession
{
    public sealed record OrderCall(long Ctid, long SymbolId, bool IsBuy, long Volume, string Label);
    public sealed record CloseCall(long Ctid, long PositionId, long Volume);
    public sealed record AmendCall(long Ctid, long PositionId, double? StopLoss, double? TakeProfit);

    private readonly Channel<ExecutionEvent> _source =
        Channel.CreateUnbounded<ExecutionEvent>(new UnboundedChannelOptions { SingleReader = true });
    private readonly Dictionary<long, List<OpenPositionSnapshot>> _positions = new();
    private readonly Dictionary<long, string> _symbolNames;
    private readonly Dictionary<string, long> _symbolIds;
    private readonly SymbolDetails _details;
    private long _positionSeq = 5000;

    public List<OrderCall> Orders { get; } = [];
    public List<CloseCall> Closes { get; } = [];
    public List<AmendCall> Amends { get; } = [];
    public HashSet<long> FailOrdersForCtid { get; } = [];

    public FakeTradingSession(
        IReadOnlyDictionary<long, string> symbolNames,
        IReadOnlyDictionary<string, long> symbolIds,
        SymbolDetails details)
    {
        _symbolNames = new Dictionary<long, string>(symbolNames);
        _symbolIds = new Dictionary<string, long>(symbolIds, StringComparer.OrdinalIgnoreCase);
        _details = details;
    }

    public ConnectionState State => ConnectionState.Connected;
    public Func<CancellationToken, Task>? OnReconnected { get; set; }

    public void AttachAccount(long ctidTraderAccountId, string accessToken) { }
    public Task StartAsync(CancellationToken ct) => Task.CompletedTask;

    public void PushOpen(long ctid, long positionId, long symbolId, bool isBuy, long volume,
        double? stopLoss = null, double? takeProfit = null)
        => _source.Writer.TryWrite(new ExecutionEvent(ctid, "ORDER_ACCEPTED", positionId, symbolId,
            isBuy, volume, 1.10, stopLoss, takeProfit, IsOpen: true));

    public void PushClose(long ctid, long positionId, long symbolId, bool isBuy, long volume)
        => _source.Writer.TryWrite(new ExecutionEvent(ctid, "ORDER_ACCEPTED", positionId, symbolId,
            isBuy, volume, 1.10, null, null, IsOpen: false));

    public void SeedPosition(long ctid, long positionId, long symbolId, bool isBuy, long volume, string label)
        => Store(ctid).Add(new OpenPositionSnapshot(positionId, symbolId, isBuy, volume, label));

    public async IAsyncEnumerable<ExecutionEvent> SourceExecutionsAsync(
        long ctidTraderAccountId, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var e in _source.Reader.ReadAllAsync(ct))
            if (e.CtidTraderAccountId == ctidTraderAccountId)
                yield return e;
    }

    public Task<double> LoadBalanceAsync(long ctidTraderAccountId, CancellationToken ct) => Task.FromResult(10000.0);

    public Task<IReadOnlyDictionary<string, long>> LoadSymbolIdsAsync(long ctidTraderAccountId, CancellationToken ct)
        => Task.FromResult<IReadOnlyDictionary<string, long>>(_symbolIds);

    public Task<IReadOnlyDictionary<long, string>> LoadSymbolNamesAsync(long ctidTraderAccountId, CancellationToken ct)
        => Task.FromResult<IReadOnlyDictionary<long, string>>(_symbolNames);

    public Task SendMarketOrderAsync(long ctidTraderAccountId, long symbolId, bool isBuy, long volume, string label, CancellationToken ct)
    {
        if (FailOrdersForCtid.Contains(ctidTraderAccountId))
            throw new InvalidOperationException("simulated order rejection");
        Orders.Add(new OrderCall(ctidTraderAccountId, symbolId, isBuy, volume, label));
        Store(ctidTraderAccountId).Add(new OpenPositionSnapshot(++_positionSeq, symbolId, isBuy, volume, label));
        return Task.CompletedTask;
    }

    public Task ClosePositionAsync(long ctidTraderAccountId, long positionId, long volume, CancellationToken ct)
    {
        Closes.Add(new CloseCall(ctidTraderAccountId, positionId, volume));
        Store(ctidTraderAccountId).RemoveAll(p => p.PositionId == positionId);
        return Task.CompletedTask;
    }

    public Task AmendPositionSltpAsync(long ctidTraderAccountId, long positionId, double? stopLoss, double? takeProfit, CancellationToken ct)
    {
        Amends.Add(new AmendCall(ctidTraderAccountId, positionId, stopLoss, takeProfit));
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<OpenPositionSnapshot>> ReconcileAsync(long ctidTraderAccountId, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<OpenPositionSnapshot>>(Store(ctidTraderAccountId).ToList());

    public Task<IReadOnlyList<SymbolDetails>> LoadSymbolDetailsAsync(
        long ctidTraderAccountId, IReadOnlyCollection<long> symbolIds, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<SymbolDetails>>(
            symbolIds.Select(id => _details with { SymbolId = id }).ToList());

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private List<OpenPositionSnapshot> Store(long ctid)
        => _positions.TryGetValue(ctid, out var list) ? list : _positions[ctid] = [];
}

internal sealed class FakeTradingSessionFactory(FakeTradingSession session) : IOpenApiTradingSessionFactory
{
    public IOpenApiTradingSession Create(bool live, string clientId, string clientSecret) => session;
}
