using System.Runtime.CompilerServices;
using CTraderOpenApi.Messages;

namespace CTraderOpenApi.Client;

public enum CopyOrderKind
{
    Market = 0,
    Limit = 1,
    Stop = 2,
    MarketRange = 3,
    StopLimit = 4
}

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
    bool IsOpen,
    long OrderId = 0,
    bool IsPendingOrder = false,
    bool IsOrderCancelled = false,
    CopyOrderKind OrderKind = CopyOrderKind.Market,
    double? LimitPrice = null,
    double? StopPrice = null,
    bool TrailingStopLoss = false,
    long? ExpirationTimestamp = null,
    int? SlippageInPoints = null,
    double? BaseSlippagePrice = null,
    long? ServerTimestamp = null,
    string? SourceLabel = null);

public sealed record OpenPositionSnapshot(
    long PositionId, long SymbolId, bool IsBuy, long Volume, string Label,
    double? StopLoss = null, bool TrailingStopLoss = false);

public sealed record PendingOrderSnapshot(
    long OrderId, long SymbolId, bool IsBuy, long Volume, CopyOrderKind Kind, double Price, string Label);

public sealed record SymbolDetails(long SymbolId, long LotSize, long StepVolume, long MinVolume, int PipPosition,
    long MaxVolume = 0, int Digits = 0);

public interface IOpenApiTradingSession : IAsyncDisposable
{
    ConnectionState State { get; }
    Func<CancellationToken, Task>? OnReconnected { get; set; }

    void AttachAccount(long ctidTraderAccountId, string accessToken);
    Task SwapAccessTokenAsync(long ctidTraderAccountId, string accessToken, CancellationToken ct);
    Task StartAsync(CancellationToken ct);
    Task<double> LoadBalanceAsync(long ctidTraderAccountId, CancellationToken ct);
    Task<IReadOnlyList<PositionValuation>> LoadPositionValuationsAsync(long ctidTraderAccountId, CancellationToken ct);
    IAsyncEnumerable<ExecutionEvent> SourceExecutionsAsync(long ctidTraderAccountId, CancellationToken ct);
    Task<IReadOnlyDictionary<string, long>> LoadSymbolIdsAsync(long ctidTraderAccountId, CancellationToken ct);
    Task<IReadOnlyDictionary<long, string>> LoadSymbolNamesAsync(long ctidTraderAccountId, CancellationToken ct);
    Task SendMarketOrderAsync(long ctidTraderAccountId, long symbolId, bool isBuy, long volume, string label,
        CancellationToken ct, int? slippageInPoints = null, double? baseSlippagePrice = null);
    Task SendPendingOrderAsync(long ctidTraderAccountId, long symbolId, bool isBuy, long volume,
        CopyOrderKind kind, double price, string label, CancellationToken ct,
        long? expirationTimestamp = null, int? slippageInPoints = null);
    Task AmendPendingOrderAsync(long ctidTraderAccountId, long orderId, CopyOrderKind kind, long volume, double price,
        long? expirationTimestamp, int? slippageInPoints, CancellationToken ct);
    Task<(double Bid, double Ask)> LoadSpotPriceAsync(long ctidTraderAccountId, long symbolId, CancellationToken ct);
    Task CancelOrderAsync(long ctidTraderAccountId, long orderId, CancellationToken ct);
    Task ClosePositionAsync(long ctidTraderAccountId, long positionId, long volume, CancellationToken ct);
    Task AmendPositionSltpAsync(long ctidTraderAccountId, long positionId, double? stopLoss, double? takeProfit,
        bool trailingStopLoss, CancellationToken ct);
    Task<IReadOnlyList<OpenPositionSnapshot>> ReconcileAsync(long ctidTraderAccountId, CancellationToken ct);
    Task<IReadOnlyList<PendingOrderSnapshot>> ReconcilePendingOrdersAsync(long ctidTraderAccountId, CancellationToken ct);
    Task<IReadOnlyList<SymbolDetails>> LoadSymbolDetailsAsync(
        long ctidTraderAccountId, IReadOnlyCollection<long> symbolIds, CancellationToken ct);
}

public interface IOpenApiTradingSessionFactory
{
    IOpenApiTradingSession Create(bool live, string clientId, string clientSecret);
}

public sealed class OpenApiTradingSessionFactory(IOpenApiConnectionFactory connectionFactory) : IOpenApiTradingSessionFactory
{
    public IOpenApiTradingSession Create(bool live, string clientId, string clientSecret)
        => new OpenApiTradingSession(connectionFactory.Create(live, clientId, clientSecret));
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

    public Task SwapAccessTokenAsync(long ctidTraderAccountId, string accessToken, CancellationToken ct)
        => connection.AuthorizeAccountAsync(ctidTraderAccountId, accessToken, ct);

    public Task StartAsync(CancellationToken ct) => connection.StartAsync(ct);

    public async IAsyncEnumerable<ExecutionEvent> SourceExecutionsAsync(
        long ctidTraderAccountId, [EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var message in connection.Events.WithCancellation(ct))
        {
            if (message.PayloadType != (uint)ProtoOAPayloadType.ProtoOaExecutionEvent) continue;

            var executionEvent = ProtoOAExecutionEvent.Parser.ParseFrom(message.Payload);
            if (executionEvent.CtidTraderAccountId != ctidTraderAccountId) continue;

            var isPositionOpen = executionEvent.Position is { PositionStatus: ProtoOAPositionStatus.PositionStatusOpen };

            // A resting pending order (limit/stop) carries a non-open Position placeholder, so classify
            // its placement/cancel as an order event first. A *fill* of a limit/stop (e.g. a triggered
            // stop-loss that closes a position) is NOT a pending event — let it fall through to the
            // position branch below so the close mirrors.
            var pendingCancelled = executionEvent.ExecutionType is ProtoOAExecutionType.OrderCancelled
                or ProtoOAExecutionType.OrderExpired or ProtoOAExecutionType.OrderRejected;
            var pendingPlaced = executionEvent.ExecutionType is ProtoOAExecutionType.OrderAccepted
                or ProtoOAExecutionType.OrderReplaced;
            if (!isPositionOpen && (pendingPlaced || pendingCancelled) && executionEvent.Order is { } pendingOrder
                && PendingKind(pendingOrder.OrderType) is { } kind)
            {
                var cancelled = pendingCancelled;
                var tradeData = pendingOrder.TradeData;
                yield return new ExecutionEvent(
                    executionEvent.CtidTraderAccountId,
                    executionEvent.ExecutionType.ToString(),
                    pendingOrder.HasPositionId ? pendingOrder.PositionId : 0,
                    tradeData.SymbolId,
                    tradeData.TradeSide == ProtoOATradeSide.Buy,
                    tradeData.Volume,
                    pendingOrder.HasLimitPrice ? pendingOrder.LimitPrice : pendingOrder.HasStopPrice ? pendingOrder.StopPrice : 0,
                    pendingOrder.HasStopLoss ? pendingOrder.StopLoss : null,
                    pendingOrder.HasTakeProfit ? pendingOrder.TakeProfit : null,
                    IsOpen: false,
                    OrderId: pendingOrder.OrderId,
                    IsPendingOrder: true,
                    IsOrderCancelled: cancelled,
                    OrderKind: kind,
                    LimitPrice: pendingOrder.HasLimitPrice ? pendingOrder.LimitPrice : null,
                    StopPrice: pendingOrder.HasStopPrice ? pendingOrder.StopPrice : null,
                    TrailingStopLoss: pendingOrder.TrailingStopLoss,
                    ExpirationTimestamp: pendingOrder.HasExpirationTimestamp ? pendingOrder.ExpirationTimestamp : null,
                    SlippageInPoints: pendingOrder.HasSlippageInPoints ? (int)pendingOrder.SlippageInPoints : null,
                    BaseSlippagePrice: pendingOrder.HasBaseSlippagePrice ? pendingOrder.BaseSlippagePrice : null,
                    ServerTimestamp: executionEvent.Deal is { HasExecutionTimestamp: true } pendingDeal ? pendingDeal.ExecutionTimestamp : null,
                    SourceLabel: tradeData.Label);
                continue;
            }

            if (executionEvent.Position is { } position)
            {
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
                    position.PositionStatus == ProtoOAPositionStatus.PositionStatusOpen,
                    OrderId: executionEvent.Order?.OrderId ?? 0,
                    OrderKind: MarketKind(executionEvent.Order?.OrderType),
                    TrailingStopLoss: position.TrailingStopLoss,
                    SlippageInPoints: executionEvent.Order is { HasSlippageInPoints: true } slipOrder ? (int)slipOrder.SlippageInPoints : null,
                    BaseSlippagePrice: executionEvent.Order is { HasBaseSlippagePrice: true } baseOrder ? baseOrder.BaseSlippagePrice : null,
                    ServerTimestamp: executionEvent.Deal is { HasExecutionTimestamp: true } positionDeal ? positionDeal.ExecutionTimestamp : null,
                    SourceLabel: tradeData.Label);
                continue;
            }
        }
    }

    private static CopyOrderKind? PendingKind(ProtoOAOrderType orderType) => orderType switch
    {
        ProtoOAOrderType.Limit => CopyOrderKind.Limit,
        ProtoOAOrderType.Stop => CopyOrderKind.Stop,
        ProtoOAOrderType.StopLimit => CopyOrderKind.StopLimit,
        _ => null
    };

    private static CopyOrderKind MarketKind(ProtoOAOrderType? orderType) => orderType switch
    {
        ProtoOAOrderType.MarketRange => CopyOrderKind.MarketRange,
        _ => CopyOrderKind.Market
    };

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
        long ctidTraderAccountId, long symbolId, bool isBuy, long volume, string label,
        CancellationToken ct, int? slippageInPoints = null, double? baseSlippagePrice = null)
    {
        var request = new ProtoOANewOrderReq
        {
            CtidTraderAccountId = ctidTraderAccountId,
            SymbolId = symbolId,
            OrderType = slippageInPoints.HasValue ? ProtoOAOrderType.MarketRange : ProtoOAOrderType.Market,
            TradeSide = isBuy ? ProtoOATradeSide.Buy : ProtoOATradeSide.Sell,
            Volume = volume,
            Label = label
        };
        if (slippageInPoints.HasValue) request.SlippageInPoints = slippageInPoints.Value;
        if (baseSlippagePrice.HasValue) request.BaseSlippagePrice = baseSlippagePrice.Value;
        await connection.SendAsync(request, (int)ProtoOAPayloadType.ProtoOaNewOrderReq, ct);
    }

    public async Task SendPendingOrderAsync(long ctidTraderAccountId, long symbolId, bool isBuy, long volume,
        CopyOrderKind kind, double price, string label, CancellationToken ct,
        long? expirationTimestamp = null, int? slippageInPoints = null)
    {
        var request = new ProtoOANewOrderReq
        {
            CtidTraderAccountId = ctidTraderAccountId,
            SymbolId = symbolId,
            OrderType = kind switch
            {
                CopyOrderKind.Stop => ProtoOAOrderType.Stop,
                CopyOrderKind.StopLimit => ProtoOAOrderType.StopLimit,
                _ => ProtoOAOrderType.Limit
            },
            TradeSide = isBuy ? ProtoOATradeSide.Buy : ProtoOATradeSide.Sell,
            Volume = volume,
            Label = label
        };
        if (kind is CopyOrderKind.Stop or CopyOrderKind.StopLimit) request.StopPrice = price;
        else request.LimitPrice = price;
        if (kind == CopyOrderKind.StopLimit && slippageInPoints.HasValue) request.SlippageInPoints = slippageInPoints.Value;
        if (expirationTimestamp.HasValue)
        {
            request.TimeInForce = ProtoOATimeInForce.GoodTillDate;
            request.ExpirationTimestamp = expirationTimestamp.Value;
        }
        await connection.SendAsync(request, (int)ProtoOAPayloadType.ProtoOaNewOrderReq, ct);
    }

    public async Task AmendPendingOrderAsync(long ctidTraderAccountId, long orderId, CopyOrderKind kind, long volume,
        double price, long? expirationTimestamp, int? slippageInPoints, CancellationToken ct)
    {
        var request = new ProtoOAAmendOrderReq
        {
            CtidTraderAccountId = ctidTraderAccountId,
            OrderId = orderId,
            Volume = volume
        };
        if (kind is CopyOrderKind.Stop or CopyOrderKind.StopLimit) request.StopPrice = price;
        else request.LimitPrice = price;
        if (kind == CopyOrderKind.StopLimit && slippageInPoints.HasValue) request.SlippageInPoints = slippageInPoints.Value;
        if (expirationTimestamp.HasValue) request.ExpirationTimestamp = expirationTimestamp.Value;
        await connection.SendAsync(request, (int)ProtoOAPayloadType.ProtoOaAmendOrderReq, ct);
    }

    public async Task CancelOrderAsync(long ctidTraderAccountId, long orderId, CancellationToken ct)
    {
        var request = new ProtoOACancelOrderReq
        {
            CtidTraderAccountId = ctidTraderAccountId,
            OrderId = orderId
        };
        await connection.SendAsync(request, (int)ProtoOAPayloadType.ProtoOaCancelOrderReq, ct);
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

    public async Task<IReadOnlyList<PositionValuation>> LoadPositionValuationsAsync(
        long ctidTraderAccountId, CancellationToken ct)
    {
        var response = await connection.SendAsync(
            new ProtoOAReconcileReq { CtidTraderAccountId = ctidTraderAccountId },
            (int)ProtoOAPayloadType.ProtoOaReconcileReq, ct);

        return ProtoOAReconcileRes.Parser.ParseFrom(response.Payload).Position
            .Select(p =>
            {
                var moneyScale = Math.Pow(10, p.HasMoneyDigits ? p.MoneyDigits : 2);
                return new PositionValuation(
                    p.PositionId,
                    p.TradeData.SymbolId,
                    p.TradeData.TradeSide == ProtoOATradeSide.Buy,
                    p.TradeData.Volume,
                    p.HasPrice ? p.Price : 0,
                    p.Swap / moneyScale,
                    p.HasCommission ? p.Commission / moneyScale : 0);
            })
            .ToList();
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

    public async Task AmendPositionSltpAsync(long ctidTraderAccountId, long positionId, double? stopLoss,
        double? takeProfit, bool trailingStopLoss, CancellationToken ct)
    {
        var request = new ProtoOAAmendPositionSLTPReq
        {
            CtidTraderAccountId = ctidTraderAccountId,
            PositionId = positionId
        };
        if (stopLoss.HasValue) request.StopLoss = stopLoss.Value;
        if (takeProfit.HasValue) request.TakeProfit = takeProfit.Value;
        if (trailingStopLoss) request.TrailingStopLoss = true;
        await connection.SendAsync(request, (int)ProtoOAPayloadType.ProtoOaAmendPositionSltpReq, ct);
    }

    public async Task<IReadOnlyList<OpenPositionSnapshot>> ReconcileAsync(long ctidTraderAccountId, CancellationToken ct)
    {
        var response = await connection.SendAsync(
            new ProtoOAReconcileReq { CtidTraderAccountId = ctidTraderAccountId },
            (int)ProtoOAPayloadType.ProtoOaReconcileReq, ct);

        return ProtoOAReconcileRes.Parser.ParseFrom(response.Payload).Position
            .Select(p => new OpenPositionSnapshot(
                p.PositionId, p.TradeData.SymbolId, p.TradeData.TradeSide == ProtoOATradeSide.Buy,
                p.TradeData.Volume, p.TradeData.Label ?? string.Empty,
                p.HasStopLoss ? p.StopLoss : null, p.TrailingStopLoss))
            .ToList();
    }

    public async Task<(double Bid, double Ask)> LoadSpotPriceAsync(long ctidTraderAccountId, long symbolId, CancellationToken ct)
    {
        const double priceScale = 100000.0;
        var subscribe = new ProtoOASubscribeSpotsReq { CtidTraderAccountId = ctidTraderAccountId };
        subscribe.SymbolId.Add(symbolId);
        await connection.SendAsync(subscribe, (int)ProtoOAPayloadType.ProtoOaSubscribeSpotsReq, ct);
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            await foreach (var message in connection.Events.WithCancellation(timeout.Token))
            {
                if (message.PayloadType != (uint)ProtoOAPayloadType.ProtoOaSpotEvent) continue;
                var spot = ProtoOASpotEvent.Parser.ParseFrom(message.Payload);
                if (spot.CtidTraderAccountId != ctidTraderAccountId || spot.SymbolId != symbolId) continue;
                if (!spot.HasBid && !spot.HasAsk) continue;
                var bid = spot.HasBid ? spot.Bid / priceScale : spot.Ask / priceScale;
                var ask = spot.HasAsk ? spot.Ask / priceScale : spot.Bid / priceScale;
                return (bid, ask);
            }
        }
        finally
        {
            var unsubscribe = new ProtoOAUnsubscribeSpotsReq { CtidTraderAccountId = ctidTraderAccountId };
            unsubscribe.SymbolId.Add(symbolId);
            await connection.SendAsync(unsubscribe, (int)ProtoOAPayloadType.ProtoOaUnsubscribeSpotsReq, ct);
        }

        throw new InvalidOperationException($"No spot price received for symbol {symbolId}.");
    }

    public async Task<IReadOnlyList<PendingOrderSnapshot>> ReconcilePendingOrdersAsync(
        long ctidTraderAccountId, CancellationToken ct)
    {
        var response = await connection.SendAsync(
            new ProtoOAReconcileReq { CtidTraderAccountId = ctidTraderAccountId },
            (int)ProtoOAPayloadType.ProtoOaReconcileReq, ct);

        return ProtoOAReconcileRes.Parser.ParseFrom(response.Payload).Order
            .Where(o => PendingKind(o.OrderType) is not null)
            .Select(o => new PendingOrderSnapshot(
                o.OrderId, o.TradeData.SymbolId, o.TradeData.TradeSide == ProtoOATradeSide.Buy,
                o.TradeData.Volume, PendingKind(o.OrderType)!.Value,
                o.HasLimitPrice ? o.LimitPrice : o.HasStopPrice ? o.StopPrice : 0,
                o.TradeData.Label ?? string.Empty))
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
            .Select(s => new SymbolDetails(s.SymbolId, s.LotSize, s.StepVolume, s.MinVolume, s.PipPosition,
                s.MaxVolume, s.Digits))
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
