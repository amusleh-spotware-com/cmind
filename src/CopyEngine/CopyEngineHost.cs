using System.Threading.Channels;
using Core;
using Core.Domain;
using Core.Logging;
using CTraderOpenApi.Client;
using Microsoft.Extensions.Logging;

namespace CopyEngine;

public sealed record CopyDestinationPlan(long CtidTraderAccountId, string AccessToken, long TokenVersion, CopyDestination Config);

public sealed record CopyProfilePlan(
    CopyProfileId ProfileId,
    bool Live,
    string ClientId,
    string ClientSecret,
    long SourceCtidTraderAccountId,
    string SourceAccessToken,
    long SourceTokenVersion,
    IReadOnlyList<CopyDestinationPlan> Destinations);

/// <summary>
/// Runs one copy profile against a live authorised Open API session: mirrors source position opens,
/// partial closes, scale-ins, full closes, pending limit/stop orders and stop-loss/trailing changes
/// onto every destination, sized by <see cref="CopyDecisionEngine"/>. Destination copies are labelled
/// with the source position id (or, for a resting pending order, the source order id) so state can be
/// rebuilt from a reconcile after any reconnect (open-missing / close-orphaned) without duplicating
/// trades.
/// </summary>
public sealed class CopyEngineHost(
    CopyProfilePlan plan,
    IOpenApiTradingSessionFactory sessionFactory,
    CopyDecisionEngine decisions,
    ILogger logger)
{
    private readonly Dictionary<long, string> _sourceSymbolNames = new();
    private readonly Dictionary<long, IReadOnlyDictionary<string, long>> _destinationSymbolIds = new();
    private readonly Dictionary<(long Ctid, long SymbolId), SymbolDetails> _symbolDetails = new();
    private readonly HashSet<long> _openSourcePositions = [];
    private readonly Dictionary<long, long> _sourceVolumes = new();
    private readonly Dictionary<long, double?> _sourceStops = new();
    private readonly HashSet<long> _mirroredPendingOrders = [];
    private readonly Channel<IReadOnlyList<(long Ctid, string Token)>> _tokenUpdates =
        Channel.CreateUnbounded<IReadOnlyList<(long Ctid, string Token)>>(new UnboundedChannelOptions { SingleReader = true });
    private readonly SemaphoreSlim _stateGate = new(1, 1);

    // Pushed by the supervisor when a cID's single valid access token rotates (refresh or re-auth after
    // the user links another account on the same cID, which invalidates the old token). The host
    // re-authorises the affected accounts in place on the live socket without dropping the event stream.
    public void PushTokenUpdate(IReadOnlyList<(long Ctid, string Token)> tokens) => _tokenUpdates.Writer.TryWrite(tokens);

    public async Task RunAsync(CancellationToken ct)
    {
        await using var session = sessionFactory.Create(plan.Live, plan.ClientId, plan.ClientSecret);

        session.AttachAccount(plan.SourceCtidTraderAccountId, plan.SourceAccessToken);
        foreach (var destination in plan.Destinations)
            session.AttachAccount(destination.CtidTraderAccountId, destination.AccessToken);

        session.OnReconnected = async token =>
        {
            await _stateGate.WaitAsync(token);
            try { await ResyncAsync(session, token); }
            finally { _stateGate.Release(); }
        };
        await session.StartAsync(ct);
        logger.CopyHostStarted(plan.ProfileId.Value, plan.SourceCtidTraderAccountId, plan.Destinations.Count);

        // Hold the state gate across the initial reference-data load and first resync: OnReconnected is
        // already wired, so a socket flap during startup would otherwise run a second resync concurrently
        // and corrupt the host's non-concurrent state dictionaries.
        await _stateGate.WaitAsync(ct);
        try
        {
            await LoadReferenceDataAsync(session, ct);
            await ResyncAsync(session, ct);
        }
        finally
        {
            _stateGate.Release();
        }

        using var loopCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var tokenSwapLoop = Task.Run(() => ConsumeTokenUpdatesAsync(session, loopCts.Token), CancellationToken.None);
        try
        {
            await foreach (var execution in session.SourceExecutionsAsync(plan.SourceCtidTraderAccountId, ct))
            {
                await _stateGate.WaitAsync(ct);
                try
                {
                    if (execution.IsPendingOrder)
                    {
                        if (execution.IsOrderCancelled) await HandlePendingCancelledAsync(session, execution, ct);
                        else await HandlePendingPlacedAsync(session, execution, ct);
                    }
                    else if (execution.IsOpen) await HandleOpenAsync(session, execution, ct);
                    else await HandleCloseAsync(session, execution.PositionId, ct);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Copy handling failed for source position {PositionId}", execution.PositionId);
                }
                finally
                {
                    _stateGate.Release();
                }
            }
        }
        finally
        {
            _tokenUpdates.Writer.TryComplete();
            loopCts.Cancel();
            try { await tokenSwapLoop; } catch { /* swap loop cancellation is expected on shutdown */ }
        }
    }

    private async Task ConsumeTokenUpdatesAsync(IOpenApiTradingSession session, CancellationToken ct)
    {
        await foreach (var tokens in _tokenUpdates.Reader.ReadAllAsync(ct))
        {
            await _stateGate.WaitAsync(ct);
            try
            {
                foreach (var (ctid, token) in tokens)
                {
                    await session.SwapAccessTokenAsync(ctid, token, ct);
                    logger.CopyHostTokenSwapped(plan.ProfileId.Value, ctid);
                }

                await ResyncAsync(session, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Copy token swap failed for profile {ProfileId}", plan.ProfileId.Value);
            }
            finally
            {
                _stateGate.Release();
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
        if (_openSourcePositions.Contains(execution.PositionId))
        {
            await HandlePositionUpdateAsync(session, execution, ct);
            return;
        }

        if (!_sourceSymbolNames.TryGetValue(execution.SymbolId, out var sourceName)) return;

        // A mirrored pending order just filled into this position: retire the resting destination
        // pending(s) so the copy re-opens as a labelled market position (uniform id-based reconcile).
        if (execution.OrderId != 0 && _mirroredPendingOrders.Remove(execution.OrderId))
            await CancelDestinationPendingsAsync(session, execution.OrderId, ct);

        _openSourcePositions.Add(execution.PositionId);
        _sourceVolumes[execution.PositionId] = execution.Volume;
        _sourceStops[execution.PositionId] = execution.StopLoss;

        var sourceDetail = await SymbolDetailAsync(session, plan.SourceCtidTraderAccountId, execution.SymbolId, ct);
        var sourceLots = VolumeConversion.LotsFromProtocol(execution.Volume, sourceDetail.LotSize);
        var sourceBalance = await session.LoadBalanceAsync(plan.SourceCtidTraderAccountId, ct);
        logger.CopySourceOpen(plan.ProfileId.Value, execution.PositionId, sourceName,
            execution.IsBuy ? "Buy" : "Sell", sourceLots);

        foreach (var destination in plan.Destinations)
        {
            try
            {
                await CopyOpenToDestinationAsync(session, destination, execution, sourceName,
                    sourceDetail, sourceBalance, applyProtection: true, isScaleIn: false, ct);
            }
            catch (Exception ex)
            {
                logger.CopyOpenFailed(plan.ProfileId.Value, destination.CtidTraderAccountId, execution.PositionId, ex);
            }
        }
    }

    private async Task HandlePositionUpdateAsync(IOpenApiTradingSession session, ExecutionEvent execution, CancellationToken ct)
    {
        var previousVolume = _sourceVolumes.GetValueOrDefault(execution.PositionId, execution.Volume);
        var previousStop = _sourceStops.GetValueOrDefault(execution.PositionId);
        _sourceVolumes[execution.PositionId] = execution.Volume;
        _sourceStops[execution.PositionId] = execution.StopLoss;

        if (execution.Volume < previousVolume)
        {
            await MirrorPartialCloseAsync(session, execution, previousVolume, ct);
            return;
        }

        if (execution.Volume > previousVolume)
        {
            await MirrorScaleInAsync(session, execution, execution.Volume - previousVolume, ct);
            return;
        }

        if (!Equals(previousStop, execution.StopLoss) || execution.TrailingStopLoss)
            await MirrorStopChangeAsync(session, execution, ct);
    }

    private async Task MirrorPartialCloseAsync(
        IOpenApiTradingSession session, ExecutionEvent execution, long previousVolume, CancellationToken ct)
    {
        var closedFraction = previousVolume <= 0 ? 0 : (double)(previousVolume - execution.Volume) / previousVolume;
        if (closedFraction <= 0) return;
        var label = execution.PositionId.ToString();

        foreach (var destination in plan.Destinations)
        {
            if (!destination.Config.MirrorPartialClose) continue;
            try
            {
                var positions = await session.ReconcileAsync(destination.CtidTraderAccountId, ct);
                foreach (var position in positions.Where(p => p.Label == label))
                {
                    var detail = await SymbolDetailAsync(session, destination.CtidTraderAccountId, position.SymbolId, ct);
                    var slice = SliceVolume(position.Volume * closedFraction, position.Volume, detail);
                    if (slice <= 0) continue;
                    await session.ClosePositionAsync(destination.CtidTraderAccountId, position.PositionId, slice, ct);
                    logger.CopyPartialClose(plan.ProfileId.Value, destination.CtidTraderAccountId,
                        position.PositionId, slice, execution.PositionId);
                }
            }
            catch (Exception ex)
            {
                logger.CopyCloseFailed(plan.ProfileId.Value, destination.CtidTraderAccountId, execution.PositionId, ex);
            }
        }
    }

    private async Task MirrorScaleInAsync(
        IOpenApiTradingSession session, ExecutionEvent execution, long addedVolume, CancellationToken ct)
    {
        if (!_sourceSymbolNames.TryGetValue(execution.SymbolId, out var sourceName)) return;
        var sourceDetail = await SymbolDetailAsync(session, plan.SourceCtidTraderAccountId, execution.SymbolId, ct);
        var sourceBalance = await session.LoadBalanceAsync(plan.SourceCtidTraderAccountId, ct);
        var scaleInExecution = execution with { Volume = addedVolume };

        foreach (var destination in plan.Destinations)
        {
            if (!destination.Config.MirrorScaleIn) continue;
            try
            {
                await CopyOpenToDestinationAsync(session, destination, scaleInExecution, sourceName,
                    sourceDetail, sourceBalance, applyProtection: false, isScaleIn: true, ct);
            }
            catch (Exception ex)
            {
                logger.CopyOpenFailed(plan.ProfileId.Value, destination.CtidTraderAccountId, execution.PositionId, ex);
            }
        }
    }

    private async Task MirrorStopChangeAsync(IOpenApiTradingSession session, ExecutionEvent execution, CancellationToken ct)
    {
        var label = execution.PositionId.ToString();
        foreach (var destination in plan.Destinations)
        {
            if (!destination.Config.CopyStopLoss && !destination.Config.CopyTrailingStop) continue;
            try
            {
                var positions = await session.ReconcileAsync(destination.CtidTraderAccountId, ct);
                var match = positions.FirstOrDefault(p => p.Label == label);
                if (match is null) continue;

                var trailing = destination.Config.CopyTrailingStop && execution.TrailingStopLoss;
                var stopLoss = destination.Config.CopyStopLoss ? execution.StopLoss : null;
                if (destination.Config.Reverse) stopLoss = destination.Config.CopyTakeProfit ? execution.TakeProfit : stopLoss;
                await session.AmendPositionSltpAsync(destination.CtidTraderAccountId, match.PositionId, stopLoss, null, trailing, ct);

                if (trailing) logger.CopyTrailingApplied(plan.ProfileId.Value, destination.CtidTraderAccountId, execution.PositionId);
                else logger.CopyStopLossAmended(plan.ProfileId.Value, destination.CtidTraderAccountId, execution.PositionId, stopLoss ?? 0);
            }
            catch (Exception ex)
            {
                logger.CopyOpenFailed(plan.ProfileId.Value, destination.CtidTraderAccountId, execution.PositionId, ex);
            }
        }
    }

    private async Task HandlePendingPlacedAsync(IOpenApiTradingSession session, ExecutionEvent execution, CancellationToken ct)
    {
        if (execution.ExecutionType == "OrderReplaced" && _mirroredPendingOrders.Contains(execution.OrderId))
        {
            await HandlePendingAmendedAsync(session, execution, ct);
            return;
        }

        if (!_sourceSymbolNames.TryGetValue(execution.SymbolId, out var sourceName)) return;
        var sourceDetail = await SymbolDetailAsync(session, plan.SourceCtidTraderAccountId, execution.SymbolId, ct);
        var sourceBalance = await session.LoadBalanceAsync(plan.SourceCtidTraderAccountId, ct);
        var price = execution.OrderKind is CopyOrderKind.Stop or CopyOrderKind.StopLimit
            ? execution.StopPrice ?? 0
            : execution.LimitPrice ?? 0;
        var placedAny = false;

        foreach (var destination in plan.Destinations)
        {
            if (!destination.Config.CopyPendingOrders) continue;
            if (!destination.Config.IsOrderTypeAllowed(CopyDecisionEngine.ToOrderTypes(execution.OrderKind)))
            {
                logger.CopySkipped(plan.ProfileId.Value, destination.CtidTraderAccountId, execution.OrderId, "order_type");
                continue;
            }
            try
            {
                if (await PlacePendingToDestinationAsync(session, destination, execution, sourceName,
                        sourceDetail, sourceBalance, price, ct))
                    placedAny = true;
            }
            catch (Exception ex)
            {
                logger.CopyOpenFailed(plan.ProfileId.Value, destination.CtidTraderAccountId, execution.OrderId, ex);
            }
        }

        if (placedAny) _mirroredPendingOrders.Add(execution.OrderId);
    }

    private async Task<bool> PlacePendingToDestinationAsync(IOpenApiTradingSession session, CopyDestinationPlan destination,
        ExecutionEvent execution, string sourceName, SymbolDetails sourceDetail, double sourceBalance, double price,
        CancellationToken ct)
    {
        var destinationName = destination.Config.ResolveDestinationSymbol(sourceName);
        if (!_destinationSymbolIds[destination.CtidTraderAccountId].TryGetValue(Normalize(destinationName), out var destinationSymbolId))
            return false;

        var destinationDetail = await SymbolDetailAsync(session, destination.CtidTraderAccountId, destinationSymbolId, ct);
        var destinationBalance = await session.LoadBalanceAsync(destination.CtidTraderAccountId, ct);
        var sourceLots = VolumeConversion.LotsFromProtocol(execution.Volume, sourceDetail.LotSize);

        var decision = decisions.DecideOpen(destination.Config, new OpenDecisionContext(
            new SourcePosition(execution.OrderId, sourceName, execution.IsBuy, sourceLots, price,
                execution.StopLoss, execution.TakeProfit),
            Snapshot(sourceBalance), Snapshot(destinationBalance),
            Spec(sourceDetail), Spec(destinationDetail), price, Math.Pow(10, -destinationDetail.PipPosition),
            TimeSpan.Zero, CopyDecisionEngine.ToOrderTypes(execution.OrderKind), execution.SlippageInPoints));
        if (decision.Kind != CopyActionKind.Open) return false;

        var wireVolume = VolumeConversion.ProtocolFromLots(decision.Lots, destinationDetail.LotSize);
        if (wireVolume <= 0) return false;

        var effectiveBuy = destination.Config.Reverse ? !execution.IsBuy : execution.IsBuy;
        var expiry = destination.Config.CopyPendingExpiry ? execution.ExpirationTimestamp : null;
        var slippage = execution.OrderKind == CopyOrderKind.StopLimit && destination.Config.CopyMasterSlippage
            ? execution.SlippageInPoints
            : null;
        await session.SendPendingOrderAsync(destination.CtidTraderAccountId, destinationSymbolId, effectiveBuy,
            wireVolume, execution.OrderKind, price, execution.OrderId.ToString(), ct, expiry, slippage);
        logger.CopyPendingOrderPlaced(plan.ProfileId.Value, destination.CtidTraderAccountId, destinationName,
            execution.OrderKind.ToString(), effectiveBuy ? "Buy" : "Sell", wireVolume, price, execution.OrderId);
        if (expiry is { } expiryTimestamp)
            logger.CopyPendingExpiryMirrored(plan.ProfileId.Value, destination.CtidTraderAccountId, execution.OrderId, expiryTimestamp);
        return true;
    }

    private async Task HandlePendingAmendedAsync(IOpenApiTradingSession session, ExecutionEvent execution, CancellationToken ct)
    {
        var label = execution.OrderId.ToString();
        var price = execution.OrderKind is CopyOrderKind.Stop or CopyOrderKind.StopLimit
            ? execution.StopPrice ?? 0
            : execution.LimitPrice ?? 0;

        foreach (var destination in plan.Destinations)
        {
            if (!destination.Config.CopyPendingOrders) continue;
            try
            {
                var expiry = destination.Config.CopyPendingExpiry ? execution.ExpirationTimestamp : null;
                var slippage = execution.OrderKind == CopyOrderKind.StopLimit && destination.Config.CopyMasterSlippage
                    ? execution.SlippageInPoints
                    : null;
                var pendings = await session.ReconcilePendingOrdersAsync(destination.CtidTraderAccountId, ct);
                foreach (var pending in pendings.Where(o => o.Label == label))
                {
                    await session.AmendPendingOrderAsync(destination.CtidTraderAccountId, pending.OrderId,
                        execution.OrderKind, pending.Volume, price, expiry, slippage, ct);
                    logger.CopyPendingOrderAmended(plan.ProfileId.Value, destination.CtidTraderAccountId,
                        pending.OrderId, execution.OrderId);
                }
            }
            catch (Exception ex)
            {
                logger.CopyOpenFailed(plan.ProfileId.Value, destination.CtidTraderAccountId, execution.OrderId, ex);
            }
        }
    }

    private async Task HandlePendingCancelledAsync(IOpenApiTradingSession session, ExecutionEvent execution, CancellationToken ct)
    {
        _mirroredPendingOrders.Remove(execution.OrderId);
        await CancelDestinationPendingsAsync(session, execution.OrderId, ct);
    }

    private async Task CancelDestinationPendingsAsync(IOpenApiTradingSession session, long sourceOrderId, CancellationToken ct)
    {
        var label = sourceOrderId.ToString();
        foreach (var destination in plan.Destinations)
        {
            try
            {
                var pendings = await session.ReconcilePendingOrdersAsync(destination.CtidTraderAccountId, ct);
                foreach (var pending in pendings.Where(o => o.Label == label))
                {
                    await session.CancelOrderAsync(destination.CtidTraderAccountId, pending.OrderId, ct);
                    logger.CopyPendingOrderCancelled(plan.ProfileId.Value, destination.CtidTraderAccountId,
                        pending.OrderId, sourceOrderId);
                }
            }
            catch (Exception ex)
            {
                logger.CopyCloseFailed(plan.ProfileId.Value, destination.CtidTraderAccountId, sourceOrderId, ex);
            }
        }
    }

    private async Task CopyOpenToDestinationAsync(IOpenApiTradingSession session, CopyDestinationPlan destination,
        ExecutionEvent execution, string sourceName, SymbolDetails sourceDetail, double sourceBalance,
        bool applyProtection, bool isScaleIn, CancellationToken ct)
    {
        var destinationName = destination.Config.ResolveDestinationSymbol(sourceName);
        if (!_destinationSymbolIds[destination.CtidTraderAccountId].TryGetValue(Normalize(destinationName), out var destinationSymbolId))
            return;

        var destinationDetail = await SymbolDetailAsync(session, destination.CtidTraderAccountId, destinationSymbolId, ct);
        var destinationBalance = await session.LoadBalanceAsync(destination.CtidTraderAccountId, ct);
        var sourceLots = VolumeConversion.LotsFromProtocol(execution.Volume, sourceDetail.LotSize);

        var decision = decisions.DecideOpen(destination.Config, new OpenDecisionContext(
            new SourcePosition(execution.PositionId, sourceName, execution.IsBuy, sourceLots,
                execution.Price, execution.StopLoss, execution.TakeProfit),
            Snapshot(sourceBalance),
            Snapshot(destinationBalance),
            Spec(sourceDetail),
            Spec(destinationDetail),
            execution.Price,
            Math.Pow(10, -destinationDetail.PipPosition),
            TimeSpan.Zero,
            CopyDecisionEngine.ToOrderTypes(execution.OrderKind),
            execution.SlippageInPoints));

        if (decision.Kind != CopyActionKind.Open)
        {
            logger.CopySkipped(plan.ProfileId.Value, destination.CtidTraderAccountId, execution.PositionId,
                decision.SkipReason ?? "unknown");
            return;
        }

        var effectiveBuy = destination.Config.Reverse ? !execution.IsBuy : execution.IsBuy;
        var wireVolume = VolumeConversion.ProtocolFromLots(decision.Lots, destinationDetail.LotSize);
        if (wireVolume <= 0)
        {
            logger.CopySkipped(plan.ProfileId.Value, destination.CtidTraderAccountId, execution.PositionId, "size_zero");
            return;
        }

        double? baseSlippagePrice = null;
        if (decision.SlippageInPoints is not null)
        {
            var (bid, ask) = await session.LoadSpotPriceAsync(destination.CtidTraderAccountId, destinationSymbolId, ct);
            baseSlippagePrice = effectiveBuy ? ask : bid;
        }

        await session.SendMarketOrderAsync(destination.CtidTraderAccountId, destinationSymbolId,
            effectiveBuy, wireVolume, execution.PositionId.ToString(), ct, decision.SlippageInPoints, baseSlippagePrice);
        if (decision.SlippageInPoints is { } slippagePoints)
            logger.CopyMarketRangeSlippage(plan.ProfileId.Value, destination.CtidTraderAccountId, execution.PositionId, slippagePoints);
        if (isScaleIn)
            logger.CopyScaleIn(plan.ProfileId.Value, destination.CtidTraderAccountId, destinationName, wireVolume, execution.PositionId);
        else
            logger.CopyOrderPlaced(plan.ProfileId.Value, destination.CtidTraderAccountId, destinationName,
                effectiveBuy ? "Buy" : "Sell", wireVolume, execution.PositionId);

        if (applyProtection) await ApplyProtectionAsync(session, destination, execution, ct);
    }

    private async Task ApplyProtectionAsync(
        IOpenApiTradingSession session, CopyDestinationPlan destination, ExecutionEvent source, CancellationToken ct)
    {
        double? stopLoss = destination.Config.CopyStopLoss ? source.StopLoss : null;
        double? takeProfit = destination.Config.CopyTakeProfit ? source.TakeProfit : null;
        if (destination.Config.Reverse) (stopLoss, takeProfit) = (takeProfit, stopLoss);
        var trailing = destination.Config.CopyTrailingStop && source.TrailingStopLoss;
        if (stopLoss is null && takeProfit is null && !trailing) return;

        var label = source.PositionId.ToString();
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var positions = await session.ReconcileAsync(destination.CtidTraderAccountId, ct);
            var match = positions.FirstOrDefault(p => p.Label == label);
            if (match is not null)
            {
                await session.AmendPositionSltpAsync(destination.CtidTraderAccountId, match.PositionId, stopLoss, takeProfit, trailing, ct);
                logger.CopyProtectionApplied(plan.ProfileId.Value, destination.CtidTraderAccountId,
                    source.PositionId, stopLoss ?? 0, takeProfit ?? 0);
                if (trailing) logger.CopyTrailingApplied(plan.ProfileId.Value, destination.CtidTraderAccountId, source.PositionId);
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(400), ct);
        }
    }

    private async Task HandleCloseAsync(IOpenApiTradingSession session, long sourcePositionId, CancellationToken ct)
    {
        _openSourcePositions.Remove(sourcePositionId);
        _sourceVolumes.Remove(sourcePositionId);
        _sourceStops.Remove(sourcePositionId);
        var label = sourcePositionId.ToString();
        logger.CopySourceClose(plan.ProfileId.Value, sourcePositionId);

        foreach (var destination in plan.Destinations)
        {
            try
            {
                var positions = await session.ReconcileAsync(destination.CtidTraderAccountId, ct);
                foreach (var position in positions.Where(p => p.Label == label))
                {
                    await session.ClosePositionAsync(destination.CtidTraderAccountId, position.PositionId, position.Volume, ct);
                    logger.CopyPositionClosed(plan.ProfileId.Value, destination.CtidTraderAccountId,
                        position.PositionId, sourcePositionId);
                }
            }
            catch (Exception ex)
            {
                logger.CopyCloseFailed(plan.ProfileId.Value, destination.CtidTraderAccountId, sourcePositionId, ex);
            }
        }
    }

    private async Task ResyncAsync(IOpenApiTradingSession session, CancellationToken ct)
    {
        var sourcePositions = await session.ReconcileAsync(plan.SourceCtidTraderAccountId, ct);
        var sourceOpenIds = sourcePositions.Select(p => p.PositionId).ToHashSet();
        _openSourcePositions.Clear();
        _sourceVolumes.Clear();
        _sourceStops.Clear();
        foreach (var position in sourcePositions)
        {
            _openSourcePositions.Add(position.PositionId);
            _sourceVolumes[position.PositionId] = position.Volume;
            _sourceStops[position.PositionId] = position.StopLoss;
        }

        var sourcePendingIds = (await session.ReconcilePendingOrdersAsync(plan.SourceCtidTraderAccountId, ct))
            .Select(o => o.OrderId).ToHashSet();
        _mirroredPendingOrders.RemoveWhere(id => !sourcePendingIds.Contains(id));

        var orphansClosed = 0;
        var sourceBalance = await session.LoadBalanceAsync(plan.SourceCtidTraderAccountId, ct);
        foreach (var destination in plan.Destinations)
        {
            var destinationPositions = await session.ReconcileAsync(destination.CtidTraderAccountId, ct);
            var mirroredSourceIds = destinationPositions
                .Where(p => long.TryParse(p.Label, out _))
                .Select(p => long.Parse(p.Label))
                .ToHashSet();

            foreach (var position in destinationPositions)
            {
                if (long.TryParse(position.Label, out var sourceId) && !sourceOpenIds.Contains(sourceId))
                {
                    await session.ClosePositionAsync(destination.CtidTraderAccountId, position.PositionId, position.Volume, ct);
                    orphansClosed++;
                }
            }

            // Master positions opened before the profile started, or while the socket was dropped, have no
            // labelled copy on the destination yet. Open them now (labelled by source id) so a start with
            // pre-existing trades and a mid-run desync both converge to the master's live state.
            foreach (var source in sourcePositions)
            {
                if (mirroredSourceIds.Contains(source.PositionId)) continue;
                if (!_sourceSymbolNames.TryGetValue(source.SymbolId, out var sourceName)) continue;

                var sourceDetail = await SymbolDetailAsync(session, plan.SourceCtidTraderAccountId, source.SymbolId, ct);
                var syntheticOpen = new ExecutionEvent(plan.SourceCtidTraderAccountId, "RESYNC_OPEN",
                    source.PositionId, source.SymbolId, source.IsBuy, source.Volume, 0,
                    source.StopLoss, null, IsOpen: true, TrailingStopLoss: source.TrailingStopLoss);
                try
                {
                    await CopyOpenToDestinationAsync(session, destination, syntheticOpen, sourceName,
                        sourceDetail, sourceBalance, applyProtection: true, isScaleIn: false, ct);
                }
                catch (Exception ex)
                {
                    logger.CopyOpenFailed(plan.ProfileId.Value, destination.CtidTraderAccountId, source.PositionId, ex);
                }
            }

            var destinationPendings = await session.ReconcilePendingOrdersAsync(destination.CtidTraderAccountId, ct);
            foreach (var pending in destinationPendings)
                if (long.TryParse(pending.Label, out var sourceOrderId) && !sourcePendingIds.Contains(sourceOrderId))
                    await session.CancelOrderAsync(destination.CtidTraderAccountId, pending.OrderId, ct);
        }

        logger.CopyResync(plan.ProfileId.Value, sourceOpenIds.Count, orphansClosed);
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

    private static long SliceVolume(double raw, long available, SymbolDetails detail)
    {
        var step = detail.StepVolume > 0 ? detail.StepVolume : 1;
        var steps = (long)Math.Round(raw / step, MidpointRounding.AwayFromZero);
        var slice = Math.Min(steps * step, available);
        return slice < detail.MinVolume ? 0 : slice;
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
