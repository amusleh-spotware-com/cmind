using Core;
using Core.Domain;
using CTraderOpenApi.Client;

namespace CopyEngine;

public enum CopyActionKind
{
    Open,
    Skip
}

public sealed record CopyAction(CopyActionKind Kind, double Lots, bool Reversed, string? SkipReason, int? SlippageInPoints = null)
{
    public static CopyAction Skip(string reason) => new(CopyActionKind.Skip, 0, false, reason);
}

public sealed record SourcePosition(
    long PositionId,
    string Symbol,
    bool IsLong,
    double VolumeLots,
    double OpenPrice,
    double? StopLoss,
    double? TakeProfit);

public sealed record OpenDecisionContext(
    SourcePosition Source,
    AccountSnapshot MasterAccount,
    AccountSnapshot DestinationAccount,
    SymbolSpec MasterSymbol,
    SymbolSpec DestinationSymbol,
    double DestinationPrice,
    double DestinationPipSize,
    TimeSpan EventAge,
    CopyOrderTypes OrderType = CopyOrderTypes.Market,
    int? MasterSlippageInPoints = null,
    double VolumeMultiplier = 1,
    double RiskFallbackLots = 0);

/// <summary>
/// Pure copy-decision logic shared by every host. Applies direction, latency and slippage filters,
/// sizes the destination order, and reconciles source vs destination state after a reconnect. Holds
/// no I/O so it is fully unit-testable and deterministic.
/// </summary>
public sealed class CopyDecisionEngine(ICopySizingCalculator calculator)
{
    public CopyAction DecideOpen(CopyDestination destination, OpenDecisionContext context)
    {
        if (!destination.IsSymbolAllowed(context.Source.Symbol))
            return CopyAction.Skip("symbol_filter");

        if (!destination.IsOrderTypeAllowed(context.OrderType))
            return CopyAction.Skip("order_type");

        var effectiveLong = destination.Reverse ? !context.Source.IsLong : context.Source.IsLong;

        if (destination.Direction == CopyDirectionFilter.LongOnly && !effectiveLong)
            return CopyAction.Skip("direction");
        if (destination.Direction == CopyDirectionFilter.ShortOnly && effectiveLong)
            return CopyAction.Skip("direction");

        if (destination.MaxDelaySeconds > 0
            && context.EventAge > TimeSpan.FromSeconds(destination.MaxDelaySeconds))
            return CopyAction.Skip("max_delay");

        if (destination.SlippagePips > 0 && context.DestinationPipSize > 0)
        {
            var pips = Math.Abs(context.DestinationPrice - context.Source.OpenPrice) / context.DestinationPipSize;
            if (pips > destination.SlippagePips) return CopyAction.Skip("slippage");
        }

        // M7 risk-from-stop needs the master's stop distance; without a master SL there's nothing to size
        // the risk against, so skip rather than guess a size on an unstopped position.
        var masterStopDistance = context.Source.StopLoss is { } stopLoss
            ? Math.Abs(context.Source.OpenPrice - stopLoss)
            : 0;
        if (destination.Risk.Mode == MoneyManagementMode.RiskFromStopLoss && masterStopDistance <= 0
            && context.RiskFallbackLots <= 0)
            return CopyAction.Skip("no_stop_loss");

        var volume = calculator.Calculate(new CopySizingInput(
            context.Source.VolumeLots,
            context.MasterAccount,
            context.DestinationAccount,
            context.MasterSymbol,
            context.DestinationSymbol,
            destination.Risk,
            destination.Bounds,
            masterStopDistance,
            context.VolumeMultiplier,
            context.RiskFallbackLots));

        var slippageInPoints = context.OrderType == CopyOrderTypes.MarketRange && destination.CopyMasterSlippage
            ? context.MasterSlippageInPoints
            : null;

        if (volume.Skipped) return CopyAction.Skip("size_zero");

        // C14 lot sanity ceiling: a computed copy that dwarfs the master's own size (or an absolute cap) is
        // almost certainly a runaway multiplier/rounding bug — hard-block it rather than place a ruinous order.
        if (destination.LotSanity.IsBreached(volume.Lots, context.Source.VolumeLots))
            return CopyAction.Skip("lot_sanity");

        return new CopyAction(CopyActionKind.Open, volume.Lots, destination.Reverse, null, slippageInPoints);
    }

    public static CopyOrderTypes ToOrderTypes(CopyOrderKind kind) => kind switch
    {
        CopyOrderKind.MarketRange => CopyOrderTypes.MarketRange,
        CopyOrderKind.Limit => CopyOrderTypes.Limit,
        CopyOrderKind.Stop => CopyOrderTypes.Stop,
        CopyOrderKind.StopLimit => CopyOrderTypes.StopLimit,
        _ => CopyOrderTypes.Market
    };

    public IReadOnlyList<long> PositionsToOpen(
        IReadOnlyCollection<long> sourceOpenPositionIds, IReadOnlyDictionary<long, long> positionMap)
        => sourceOpenPositionIds.Where(id => !positionMap.ContainsKey(id)).ToList();

    public IReadOnlyList<long> DestinationPositionsToClose(
        IReadOnlyCollection<long> sourceOpenPositionIds, IReadOnlyDictionary<long, long> positionMap)
        => positionMap.Where(pair => !sourceOpenPositionIds.Contains(pair.Key))
            .Select(pair => pair.Value).ToList();
}
