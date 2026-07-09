using Core;
using Core.Domain;
using CTraderOpenApi.Client;
using Microsoft.Extensions.Logging;

namespace CopyEngine;

public sealed record CopyDestinationPlan(long CtidTraderAccountId, string AccessToken, CopyDestination Config);

public sealed record CopyProfilePlan(
    CopyProfileId ProfileId,
    bool Live,
    string ClientId,
    string ClientSecret,
    long SourceCtidTraderAccountId,
    string SourceAccessToken,
    IReadOnlyList<CopyDestinationPlan> Destinations);

/// <summary>
/// Runs one copy profile against a live authorised Open API session: mirrors source position opens
/// and closes onto every destination, sized by <see cref="CopyDecisionEngine"/>. Destination copies
/// are labelled with the source position id so state can be rebuilt from a reconcile after any
/// reconnect (open-missing / close-orphaned) without duplicating trades.
/// </summary>
public sealed class CopyEngineHost(
    CopyProfilePlan plan,
    IOpenApiConnectionFactory connectionFactory,
    CopyDecisionEngine decisions,
    ILogger logger)
{
    private readonly Dictionary<long, string> _sourceSymbolNames = new();
    private readonly Dictionary<long, IReadOnlyDictionary<string, long>> _destinationSymbolIds = new();
    private readonly Dictionary<(long Ctid, long SymbolId), SymbolDetails> _symbolDetails = new();
    private readonly HashSet<long> _openSourcePositions = [];

    public async Task RunAsync(CancellationToken ct)
    {
        await using var session = new OpenApiTradingSession(
            connectionFactory.Create(plan.Live, plan.ClientId, plan.ClientSecret));

        session.AttachAccount(plan.SourceCtidTraderAccountId, plan.SourceAccessToken);
        foreach (var destination in plan.Destinations)
            session.AttachAccount(destination.CtidTraderAccountId, destination.AccessToken);

        session.OnReconnected = token => ResyncAsync(session, token);
        await session.StartAsync(ct);
        await LoadReferenceDataAsync(session, ct);
        await ResyncAsync(session, ct);

        await foreach (var execution in session.SourceExecutionsAsync(plan.SourceCtidTraderAccountId, ct))
        {
            try
            {
                if (execution.IsOpen) await HandleOpenAsync(session, execution, ct);
                else await HandleCloseAsync(session, execution.PositionId, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Copy handling failed for source position {PositionId}", execution.PositionId);
            }
        }
    }

    private async Task LoadReferenceDataAsync(IOpenApiTradingSession session, CancellationToken ct)
    {
        foreach (var (id, name) in await session.LoadSymbolNamesAsync(plan.SourceCtidTraderAccountId, ct))
            _sourceSymbolNames[id] = name;

        foreach (var destination in plan.Destinations)
            _destinationSymbolIds[destination.CtidTraderAccountId] =
                await session.LoadSymbolIdsAsync(destination.CtidTraderAccountId, ct);
    }

    private async Task HandleOpenAsync(IOpenApiTradingSession session, ExecutionEvent execution, CancellationToken ct)
    {
        if (!_openSourcePositions.Add(execution.PositionId)) return;
        if (!_sourceSymbolNames.TryGetValue(execution.SymbolId, out var sourceName)) return;

        var sourceDetail = await SymbolDetailAsync(session, plan.SourceCtidTraderAccountId, execution.SymbolId, ct);
        var sourceLots = VolumeConversion.LotsFromProtocol(execution.Volume, sourceDetail.LotSize);
        var sourceBalance = await session.LoadBalanceAsync(plan.SourceCtidTraderAccountId, ct);

        foreach (var destination in plan.Destinations)
        {
            var destinationName = destination.Config.ResolveDestinationSymbol(sourceName);
            if (!_destinationSymbolIds[destination.CtidTraderAccountId].TryGetValue(Normalize(destinationName), out var destinationSymbolId))
                continue;

            var destinationDetail = await SymbolDetailAsync(session, destination.CtidTraderAccountId, destinationSymbolId, ct);
            var destinationBalance = await session.LoadBalanceAsync(destination.CtidTraderAccountId, ct);

            var decision = decisions.DecideOpen(destination.Config, new OpenDecisionContext(
                new SourcePosition(execution.PositionId, sourceName, execution.IsBuy, sourceLots,
                    execution.Price, execution.StopLoss, execution.TakeProfit),
                Snapshot(sourceBalance),
                Snapshot(destinationBalance),
                Spec(sourceDetail),
                Spec(destinationDetail),
                execution.Price,
                Math.Pow(10, -destinationDetail.PipPosition),
                TimeSpan.Zero));

            if (decision.Kind != CopyActionKind.Open) continue;

            var effectiveBuy = destination.Config.Reverse ? !execution.IsBuy : execution.IsBuy;
            var wireVolume = VolumeConversion.ProtocolFromLots(decision.Lots, destinationDetail.LotSize);
            if (wireVolume <= 0) continue;

            await session.SendMarketOrderAsync(destination.CtidTraderAccountId, destinationSymbolId,
                effectiveBuy, wireVolume, execution.PositionId.ToString(), ct);
        }
    }

    private async Task HandleCloseAsync(IOpenApiTradingSession session, long sourcePositionId, CancellationToken ct)
    {
        _openSourcePositions.Remove(sourcePositionId);
        var label = sourcePositionId.ToString();

        foreach (var destination in plan.Destinations)
        {
            var positions = await session.ReconcileAsync(destination.CtidTraderAccountId, ct);
            foreach (var position in positions.Where(p => p.Label == label))
                await session.ClosePositionAsync(destination.CtidTraderAccountId, position.PositionId, position.Volume, ct);
        }
    }

    private async Task ResyncAsync(IOpenApiTradingSession session, CancellationToken ct)
    {
        var sourcePositions = await session.ReconcileAsync(plan.SourceCtidTraderAccountId, ct);
        var sourceOpenIds = sourcePositions.Select(p => p.PositionId).ToHashSet();
        _openSourcePositions.Clear();
        foreach (var id in sourceOpenIds) _openSourcePositions.Add(id);

        foreach (var destination in plan.Destinations)
        {
            var destinationPositions = await session.ReconcileAsync(destination.CtidTraderAccountId, ct);
            foreach (var position in destinationPositions)
            {
                if (long.TryParse(position.Label, out var sourceId) && !sourceOpenIds.Contains(sourceId))
                    await session.ClosePositionAsync(destination.CtidTraderAccountId, position.PositionId, position.Volume, ct);
            }
        }
    }

    private async Task<SymbolDetails> SymbolDetailAsync(
        IOpenApiTradingSession session, long ctid, long symbolId, CancellationToken ct)
    {
        if (_symbolDetails.TryGetValue((ctid, symbolId), out var cached)) return cached;
        var details = await session.LoadSymbolDetailsAsync(ctid, [symbolId], ct);
        var detail = details.FirstOrDefault() ?? new SymbolDetails(symbolId, 0, 0, 0, 0);
        _symbolDetails[(ctid, symbolId)] = detail;
        return detail;
    }

    private static AccountSnapshot Snapshot(double balance) => new(balance, balance, balance);

    private static SymbolSpec Spec(SymbolDetails details)
    {
        var contractSize = details.LotSize / 100.0;
        var lotStep = details.LotSize > 0 ? (double)details.StepVolume / details.LotSize : 0.01;
        var minLot = details.LotSize > 0 ? (double)details.MinVolume / details.LotSize : 0;
        return new SymbolSpec(contractSize, lotStep, minLot, 0);
    }

    private static string Normalize(string symbolName)
        => new string(symbolName.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
}
