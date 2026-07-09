using Core;
using Core.Domain;

namespace CopyEngine;

public enum CopyActionKind
{
    Open,
    Skip
}

public sealed record CopyAction(CopyActionKind Kind, double Lots, bool Reversed, string? SkipReason)
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
    TimeSpan EventAge);

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

        var volume = calculator.Calculate(new CopySizingInput(
            context.Source.VolumeLots,
            context.MasterAccount,
            context.DestinationAccount,
            context.MasterSymbol,
            context.DestinationSymbol,
            destination.Risk,
            destination.Bounds));

        return volume.Skipped
            ? CopyAction.Skip("size_zero")
            : new CopyAction(CopyActionKind.Open, volume.Lots, destination.Reverse, null);
    }

    public IReadOnlyList<long> PositionsToOpen(
        IReadOnlyCollection<long> sourceOpenPositionIds, IReadOnlyDictionary<long, long> positionMap)
        => sourceOpenPositionIds.Where(id => !positionMap.ContainsKey(id)).ToList();

    public IReadOnlyList<long> DestinationPositionsToClose(
        IReadOnlyCollection<long> sourceOpenPositionIds, IReadOnlyDictionary<long, long> positionMap)
        => positionMap.Where(pair => !sourceOpenPositionIds.Contains(pair.Key))
            .Select(pair => pair.Value).ToList();
}
