using System.Runtime.CompilerServices;
using CTraderOpenApi.Messages;

namespace CTraderOpenApi.Client;

public sealed record ExecutionEvent(
    long CtidTraderAccountId,
    string ExecutionType,
    long PositionId,
    long SymbolId,
    bool IsBuy,
    long Volume,
    double Price,
    double? StopLoss,
    double? TakeProfit,
    bool IsOpen);

public sealed record OpenPositionSnapshot(long PositionId, long SymbolId, bool IsBuy, long Volume, string Label);

public sealed record SymbolDetails(long SymbolId, long LotSize, long StepVolume, long MinVolume, int PipPosition);

public interface IOpenApiTradingSession : IAsyncDisposable
{
    ConnectionState State { get; }
    Func<CancellationToken, Task>? OnReconnected { get; set; }

    void AttachAccount(long ctidTraderAccountId, string accessToken);
    Task StartAsync(CancellationToken ct);
    Task<double> LoadBalanceAsync(long ctidTraderAccountId, CancellationToken ct);
    IAsyncEnumerable<ExecutionEvent> SourceExecutionsAsync(long ctidTraderAccountId, CancellationToken ct);
    Task<IReadOnlyDictionary<string, long>> LoadSymbolIdsAsync(long ctidTraderAccountId, CancellationToken ct);
    Task<IReadOnlyDictionary<long, string>> LoadSymbolNamesAsync(long ctidTraderAccountId, CancellationToken ct);
    Task SendMarketOrderAsync(long ctidTraderAccountId, long symbolId, bool isBuy, long volume, string label, CancellationToken ct);
    Task ClosePositionAsync(long ctidTraderAccountId, long positionId, long volume, CancellationToken ct);
    Task<IReadOnlyList<OpenPositionSnapshot>> ReconcileAsync(long ctidTraderAccountId, CancellationToken ct);
    Task<IReadOnlyList<SymbolDetails>> LoadSymbolDetailsAsync(
        long ctidTraderAccountId, IReadOnlyCollection<long> symbolIds, CancellationToken ct);
}

public sealed class OpenApiTradingSession(OpenApiConnection connection) : IOpenApiTradingSession
{
    public ConnectionState State => connection.State;

    public Func<CancellationToken, Task>? OnReconnected
    {
        get => connection.OnReconnected;
        set => connection.OnReconnected = value;
    }

    public void AttachAccount(long ctidTraderAccountId, string accessToken)
        => connection.AttachAccount(ctidTraderAccountId, accessToken);

    public Task StartAsync(CancellationToken ct) => connection.StartAsync(ct);

    public async IAsyncEnumerable<ExecutionEvent> SourceExecutionsAsync(
        long ctidTraderAccountId, [EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var message in connection.Events.WithCancellation(ct))
        {
            if (message.PayloadType != (uint)ProtoOAPayloadType.ProtoOaExecutionEvent) continue;

            var executionEvent = ProtoOAExecutionEvent.Parser.ParseFrom(message.Payload);
            if (executionEvent.CtidTraderAccountId != ctidTraderAccountId || executionEvent.Position is null) continue;

            var position = executionEvent.Position;
            var tradeData = position.TradeData;
            yield return new ExecutionEvent(
                executionEvent.CtidTraderAccountId,
                executionEvent.ExecutionType.ToString(),
                position.PositionId,
                tradeData.SymbolId,
                tradeData.TradeSide == ProtoOATradeSide.Buy,
                tradeData.Volume,
                position.Price,
                position.HasStopLoss ? position.StopLoss : null,
                position.HasTakeProfit ? position.TakeProfit : null,
                position.PositionStatus == ProtoOAPositionStatus.PositionStatusOpen);
        }
    }

    public async Task<IReadOnlyDictionary<string, long>> LoadSymbolIdsAsync(long ctidTraderAccountId, CancellationToken ct)
    {
        var symbols = await LoadSymbolsAsync(ctidTraderAccountId, ct);
        var map = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (var symbol in symbols)
            if (!string.IsNullOrEmpty(symbol.SymbolName))
                map[Normalize(symbol.SymbolName)] = symbol.SymbolId;
        return map;
    }

    public async Task<IReadOnlyDictionary<long, string>> LoadSymbolNamesAsync(long ctidTraderAccountId, CancellationToken ct)
    {
        var symbols = await LoadSymbolsAsync(ctidTraderAccountId, ct);
        var map = new Dictionary<long, string>();
        foreach (var symbol in symbols)
            if (!string.IsNullOrEmpty(symbol.SymbolName))
                map[symbol.SymbolId] = Normalize(symbol.SymbolName);
        return map;
    }

    public async Task SendMarketOrderAsync(
        long ctidTraderAccountId, long symbolId, bool isBuy, long volume, string label, CancellationToken ct)
    {
        var request = new ProtoOANewOrderReq
        {
            CtidTraderAccountId = ctidTraderAccountId,
            SymbolId = symbolId,
            OrderType = ProtoOAOrderType.Market,
            TradeSide = isBuy ? ProtoOATradeSide.Buy : ProtoOATradeSide.Sell,
            Volume = volume,
            Label = label
        };
        await connection.SendAsync(request, (int)ProtoOAPayloadType.ProtoOaNewOrderReq, ct);
    }

    public async Task<double> LoadBalanceAsync(long ctidTraderAccountId, CancellationToken ct)
    {
        var response = await connection.SendAsync(
            new ProtoOATraderReq { CtidTraderAccountId = ctidTraderAccountId },
            (int)ProtoOAPayloadType.ProtoOaTraderReq, ct);
        var trader = ProtoOATraderRes.Parser.ParseFrom(response.Payload).Trader;
        var scale = Math.Pow(10, trader.HasMoneyDigits ? trader.MoneyDigits : 2);
        return trader.Balance / scale;
    }

    public async Task ClosePositionAsync(long ctidTraderAccountId, long positionId, long volume, CancellationToken ct)
    {
        var request = new ProtoOAClosePositionReq
        {
            CtidTraderAccountId = ctidTraderAccountId,
            PositionId = positionId,
            Volume = volume
        };
        await connection.SendAsync(request, (int)ProtoOAPayloadType.ProtoOaClosePositionReq, ct);
    }

    public async Task<IReadOnlyList<OpenPositionSnapshot>> ReconcileAsync(long ctidTraderAccountId, CancellationToken ct)
    {
        var response = await connection.SendAsync(
            new ProtoOAReconcileReq { CtidTraderAccountId = ctidTraderAccountId },
            (int)ProtoOAPayloadType.ProtoOaReconcileReq, ct);

        return ProtoOAReconcileRes.Parser.ParseFrom(response.Payload).Position
            .Select(p => new OpenPositionSnapshot(
                p.PositionId, p.TradeData.SymbolId, p.TradeData.TradeSide == ProtoOATradeSide.Buy,
                p.TradeData.Volume, p.TradeData.Label ?? string.Empty))
            .ToList();
    }

    public async Task<IReadOnlyList<SymbolDetails>> LoadSymbolDetailsAsync(
        long ctidTraderAccountId, IReadOnlyCollection<long> symbolIds, CancellationToken ct)
    {
        var request = new ProtoOASymbolByIdReq { CtidTraderAccountId = ctidTraderAccountId };
        request.SymbolId.AddRange(symbolIds);

        var response = await connection.SendAsync(
            request, (int)ProtoOAPayloadType.ProtoOaSymbolByIdReq, ct);

        return ProtoOASymbolByIdRes.Parser.ParseFrom(response.Payload).Symbol
            .Select(s => new SymbolDetails(s.SymbolId, s.LotSize, s.StepVolume, s.MinVolume, s.PipPosition))
            .ToList();
    }

    private async Task<IReadOnlyList<ProtoOALightSymbol>> LoadSymbolsAsync(long ctidTraderAccountId, CancellationToken ct)
    {
        var response = await connection.SendAsync(
            new ProtoOASymbolsListReq { CtidTraderAccountId = ctidTraderAccountId },
            (int)ProtoOAPayloadType.ProtoOaSymbolsListReq, ct);
        return ProtoOASymbolsListRes.Parser.ParseFrom(response.Payload).Symbol;
    }

    private static string Normalize(string symbolName)
        => new string(symbolName.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

    public ValueTask DisposeAsync() => connection.DisposeAsync();
}
