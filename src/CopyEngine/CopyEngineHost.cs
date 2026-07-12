using System.Collections.Concurrent;
using System.Threading.Channels;
using Core;
using Core.CopyTrading;
using Core.Constants;
using Core.Domain;
using Core.Logging;
using CTraderOpenApi;
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
    TimeProvider timeProvider,
    ILogger logger,
    ICopyEventSink? sink = null,
    ICopyNotificationSink? notifications = null,
    Func<string, CancellationToken, ValueTask<bool>>? newsBlackout = null)
{
    // Optional, opt-in news-window pause: when supplied, a source open whose symbol is inside a high-impact
    // economic-calendar blackout is skipped (not mirrored). Null (the default) leaves the engine unchanged.
    private readonly Func<string, CancellationToken, ValueTask<bool>>? _newsBlackout = newsBlackout;

    // Execution-transparency sink (Phase 3). Defaults to the no-op sink so the engine is unchanged when
    // transparency is off and in every test; the supervisor passes a channel-backed sink when enabled.
    private readonly ICopyEventSink _sink = sink ?? NullCopyEventSink.Instance;
    // Operational-notification sink (2b): the profile owner's copy alert feed. No-op by default.
    private readonly ICopyNotificationSink _notifications = notifications ?? NullCopyNotificationSink.Instance;
    private readonly Dictionary<long, string> _sourceSymbolNames = new();
    private readonly Dictionary<long, IReadOnlyDictionary<string, long>> _destinationSymbolIds = new();
    // Thread-safe: read+populated from the G4 bounded-parallel destination fan-out (distinct keys per
    // destination) as well as the serialized resync, so it must tolerate concurrent access.
    private readonly ConcurrentDictionary<(long Ctid, long SymbolId), SymbolDetails> _symbolDetails = new();
    private readonly HashSet<long> _openSourcePositions = [];
    private readonly Dictionary<long, long> _sourceVolumes = new();
    private readonly Dictionary<long, double?> _sourceStops = new();
    private readonly HashSet<long> _mirroredPendingOrders = [];
    // C13: when each mirrored pending was placed, so a slave copy whose master pending later vanishes can be
    // cancelled after the correlation timeout.
    private readonly Dictionary<long, DateTimeOffset> _mirroredPendingPlacedAt = new();
    // Distinguishes the profile's first resync (start) from a later reconnect resync, so the Sync-Open /
    // Sync-Closed-on-start toggles apply only at start; a mid-run reconnect always reconciles fully.
    private bool _hasResynced;
    private readonly Channel<IReadOnlyList<(long Ctid, string Token)>> _tokenUpdates =
        Channel.CreateUnbounded<IReadOnlyList<(long Ctid, string Token)>>(new UnboundedChannelOptions { SingleReader = true });
    private readonly Channel<byte> _flattenSignals =
        Channel.CreateUnbounded<byte>(new UnboundedChannelOptions { SingleReader = true });
    private readonly SemaphoreSlim _stateGate = new(1, 1);
    private readonly PropFirmEquityCalculator _equityCalculator = new();
    // G8 rejection circuit breaker: per-destination consecutive open-failure count and, once tripped, the
    // time until new opens resume. Thread-safe: written from the G4 bounded-parallel open fan-out, where
    // each destination (distinct ctid key) is touched by exactly one task at a time.
    private readonly ConcurrentDictionary<long, (int ConsecutiveFailures, DateTimeOffset? TrippedUntil)> _destinationHealth = new();
    // G7 local position cache: a per-destination mirror of the slave's open positions so the read-heavy
    // mirror paths (partial close, stop/trailing change, full close) don't re-Reconcile on every source
    // event. A missing/null entry = cold (the next read reconciles and warms it); a warm entry is kept
    // coherent by the host's own writes (close removes, partial close reduces, amend updates the stop). Any
    // open invalidates it (the broker assigns the new position id, unknowable without a reconcile), and a
    // resync rebuilds truth. Keyed per destination, so each ctid entry is touched by one fan-out task at a
    // time; resync (under the state gate) never races the fan-out.
    private readonly ConcurrentDictionary<long, List<OpenPositionSnapshot>?> _destinationBook = new();
    // G5 partial-fill true-up: the wire volume a fresh copy open requested, per (destination, source id),
    // awaiting one fill-verification pass. If the broker partially filled the open (filled < requested by a
    // whole lot-step) the next resync tops the slave up to target, then clears the entry. One-shot: a
    // lifecycle event (partial close, scale-in, close) removes the entry so the true-up never fights the
    // proportional management of a position. Thread-safe (written from the parallel open fan-out).
    private readonly ConcurrentDictionary<(long Ctid, long SourceId), long> _pendingTrueUp = new();
    // Account-protection latch: once a destination's equity guard fires, it stops receiving new opens until
    // the host restarts (a deliberate, non-silent safety stop). Mutated only under _stateGate.
    private readonly HashSet<long> _protectedDestinations = [];
    // Prop-rule state: per-destination trading-day baseline + peak equity, and the set locked out for the
    // current day (cleared when the UTC day rolls over). Mutated only under _stateGate.
    private readonly Dictionary<long, (DateOnly Day, double DayStartEquity, double PeakEquity)> _propGuardState = new();
    private readonly HashSet<long> _lockedOutDestinations = [];
    // Destinations already warned about the consistency threshold today (one alert per UTC day).
    private readonly HashSet<long> _consistencyAlerted = [];

    // Pushed by the supervisor when a cID's single valid access token rotates (refresh or re-auth after
    // the user links another account on the same cID, which invalidates the old token). The host
    // re-authorises the affected accounts in place on the live socket without dropping the event stream.
    public void PushTokenUpdate(IReadOnlyList<(long Ctid, string Token)> tokens) => _tokenUpdates.Writer.TryWrite(tokens);

    // Flatten-all panic button (C8): pushed by the supervisor when the user requests an immediate flatten.
    // The host closes every copied position on every destination and latches them against new opens. Returns
    // false if the signal could not be enqueued (host shutting down) so the caller keeps the request pending.
    public bool PushFlatten() => _flattenSignals.Writer.TryWrite(0);

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
        var equityGuardLoop = Task.Run(() => EquityGuardLoopAsync(session, loopCts.Token), CancellationToken.None);
        var flattenLoop = Task.Run(() => ConsumeFlattenSignalsAsync(session, loopCts.Token), CancellationToken.None);
        var pendingTimeoutLoop = Task.Run(() => PendingTimeoutLoopAsync(session, loopCts.Token), CancellationToken.None);
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
            _flattenSignals.Writer.TryComplete();
            loopCts.Cancel();
            try { await tokenSwapLoop; } catch { /* swap loop cancellation is expected on shutdown */ }
            try { await equityGuardLoop; } catch { /* equity guard cancellation is expected on shutdown */ }
            try { await flattenLoop; } catch { /* flatten loop cancellation is expected on shutdown */ }
            try { await pendingTimeoutLoop; } catch { /* pending-timeout loop cancellation is expected on shutdown */ }
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

    // Phase 2 account protection (ZuluGuard / Global Account Protection): periodically evaluates each
    // destination's live equity against its protection policy and, on breach, applies the mode.
    private async Task EquityGuardLoopAsync(IOpenApiTradingSession session, CancellationToken ct)
    {
        if (plan.Destinations.All(d => d.Config.AccountProtection.Mode == AccountProtectionMode.Off
                && !d.Config.PropRules.IsEnabled && d.Config.ConsistencyThresholdPercent <= 0))
            return;

        using var timer = new PeriodicTimer(CopyDefaults.EquityGuardInterval, timeProvider);
        while (await timer.WaitForNextTickAsync(ct))
        {
            await _stateGate.WaitAsync(ct);
            try { await EvaluateGuardsAsync(session, ct); }
            catch (Exception ex) { logger.LogWarning(ex, "Copy equity guard failed for profile {ProfileId}", plan.ProfileId.Value); }
            finally { _stateGate.Release(); }
        }
    }

    private async Task EvaluateGuardsAsync(IOpenApiTradingSession session, CancellationToken ct)
    {
        foreach (var destination in plan.Destinations)
        {
            var ctid = destination.CtidTraderAccountId;
            var policy = destination.Config.AccountProtection;
            var propRules = destination.Config.PropRules;
            var protectionActive = policy.Mode != AccountProtectionMode.Off && !_protectedDestinations.Contains(ctid);
            var dayGuardsActive = propRules.IsEnabled || destination.Config.ConsistencyThresholdPercent > 0;
            if (!protectionActive && !dayGuardsActive) continue;

            var equity = (await AccountSnapshotAsync(session, ctid, needsEquity: true, ct)).Equity;

            // Account protection (ZuluGuard): latch against new opens on breach; SellOut also liquidates.
            if (protectionActive && policy.IsTriggered(equity))
            {
                _protectedDestinations.Add(ctid);
                logger.CopyAccountProtectionTriggered(plan.ProfileId.Value, ctid, policy.Mode.ToString(), equity, policy.StopEquity);
                _notifications.Notify(new CopyNotificationRecord(plan.ProfileId, ctid,
                    CopyNotificationKind.AccountProtectionTriggered, CopyNotificationSeverity.Critical,
                    $"Account protection ({policy.Mode}) triggered on destination {ctid} at equity {equity:0.##}.",
                    timeProvider.GetUtcNow()));
                if (policy.Mode == AccountProtectionMode.SellOut)
                    await FlattenDestinationAsync(session, ctid, ct);
            }

            if (dayGuardsActive)
                await EvaluateDayGuardsAsync(session, destination, equity, ct);
        }
    }

    // Day-based guards off a shared per-destination day baseline + running peak equity:
    //  - Prop-rule guard (C7): on a daily-loss / trailing-drawdown breach, auto-flatten + lock out for the day.
    //  - Consistency pre-alert (C10): warn once/day when daily profit reaches the configured percent.
    // The day baseline/peak and both per-day latches reset when the UTC day rolls over.
    private async Task EvaluateDayGuardsAsync(
        IOpenApiTradingSession session, CopyDestinationPlan destination, double equity, CancellationToken ct)
    {
        var ctid = destination.CtidTraderAccountId;
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        if (!_propGuardState.TryGetValue(ctid, out var state) || state.Day != today)
        {
            _propGuardState[ctid] = (today, equity, equity); // new trading day: reset baseline + peak
            _lockedOutDestinations.Remove(ctid);             // and clear the day's latches
            _consistencyAlerted.Remove(ctid);
            return;
        }

        var peak = Math.Max(state.PeakEquity, equity);
        _propGuardState[ctid] = (today, state.DayStartEquity, peak);

        // Loss side: prop-rule breach -> flatten + lock out (once per day).
        var rules = destination.Config.PropRules;
        if (rules.IsEnabled && !_lockedOutDestinations.Contains(ctid))
        {
            var rule = rules.DailyLossBreached(state.DayStartEquity, equity) ? "daily_loss"
                : rules.TrailingDrawdownBreached(peak, equity) ? "trailing_drawdown"
                : null;
            if (rule is not null)
            {
                _lockedOutDestinations.Add(ctid);
                logger.CopyPropRuleBreached(plan.ProfileId.Value, ctid, rule, equity);
                _notifications.Notify(new CopyNotificationRecord(plan.ProfileId, ctid,
                    CopyNotificationKind.PropRuleBreached, CopyNotificationSeverity.Critical,
                    $"Prop rule '{rule}' breached on destination {ctid} at equity {equity:0.##}; flattened and locked out for the day.",
                    timeProvider.GetUtcNow()));
                await FlattenDestinationAsync(session, ctid, ct);
            }
        }

        // Profit side: consistency pre-alert (once per day) — independent of the loss-side lockout.
        var consistencyThreshold = destination.Config.ConsistencyThresholdPercent;
        if (consistencyThreshold > 0 && state.DayStartEquity > 0 && !_consistencyAlerted.Contains(ctid))
        {
            var dailyProfitPercent = (equity - state.DayStartEquity) / state.DayStartEquity * 100.0;
            if (dailyProfitPercent >= consistencyThreshold)
            {
                _consistencyAlerted.Add(ctid);
                logger.CopyConsistencyThresholdApproaching(plan.ProfileId.Value, ctid, dailyProfitPercent, consistencyThreshold);
            }
        }
    }

    // Closes every copied position on a destination immediately (best-effort market execution — no
    // slippage guarantee). Shared by SellOut account-protection and prop-rule auto-flatten.
    private async Task FlattenDestinationAsync(IOpenApiTradingSession session, long ctid, CancellationToken ct)
    {
        InvalidateBook(ctid); // G7: positions are being closed out from under the cache — force a rebuild
        foreach (var position in await session.ReconcileAsync(ctid, ct))
        {
            // Tolerate a position the broker already closed (POSITION_NOT_FOUND) — close each independently
            // so one stale id can't leave the rest of the account un-flattened.
            try { await session.ClosePositionAsync(ctid, position.PositionId, position.Volume, ct); }
            catch (Exception ex)
            {
                NoteIfTokenInvalidated(ex, ctid);
                logger.CopyCloseFailed(plan.ProfileId.Value, ctid, position.PositionId, ex);
            }
        }
    }

    private async Task ConsumeFlattenSignalsAsync(IOpenApiTradingSession session, CancellationToken ct)
    {
        await foreach (var _ in _flattenSignals.Reader.ReadAllAsync(ct))
        {
            await _stateGate.WaitAsync(ct);
            try
            {
                foreach (var destination in plan.Destinations)
                {
                    await FlattenDestinationAsync(session, destination.CtidTraderAccountId, ct);
                    _protectedDestinations.Add(destination.CtidTraderAccountId); // block re-opens after the panic flatten
                }
                logger.CopyFlattenAll(plan.ProfileId.Value, plan.Destinations.Count);
                _notifications.Notify(new CopyNotificationRecord(plan.ProfileId, null, CopyNotificationKind.FlattenAll,
                    CopyNotificationSeverity.Critical,
                    $"Flatten-all executed: closed and locked {plan.Destinations.Count} destination(s) on request.",
                    timeProvider.GetUtcNow()));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Copy flatten-all failed for profile {ProfileId}", plan.ProfileId.Value);
            }
            finally
            {
                _stateGate.Release();
            }
        }
    }

    // C13 slave-pending fill-correlation timeout: periodically cancels a mirrored slave pending whose master
    // pending has vanished (neither still resting nor freshly filled) after the correlation timeout, so a
    // slave copy can't fill uncorrelated into an unmanaged position.
    private async Task PendingTimeoutLoopAsync(IOpenApiTradingSession session, CancellationToken ct)
    {
        if (plan.Destinations.All(d => !d.Config.CopyPendingOrders)) return;

        using var timer = new PeriodicTimer(CopyDefaults.PendingCheckInterval, timeProvider);
        while (await timer.WaitForNextTickAsync(ct))
        {
            await _stateGate.WaitAsync(ct);
            try { await CheckPendingTimeoutsAsync(session, ct); }
            catch (Exception ex) { logger.LogWarning(ex, "Copy pending-timeout check failed for profile {ProfileId}", plan.ProfileId.Value); }
            finally { _stateGate.Release(); }
        }
    }

    private async Task CheckPendingTimeoutsAsync(IOpenApiTradingSession session, CancellationToken ct)
    {
        if (_mirroredPendingPlacedAt.Count == 0) return;
        var now = timeProvider.GetUtcNow();
        var masterPendingIds = (await session.ReconcilePendingOrdersAsync(plan.SourceCtidTraderAccountId, ct))
            .Select(o => o.OrderId).ToHashSet();

        foreach (var (orderId, placedAt) in _mirroredPendingPlacedAt.ToArray())
        {
            if (masterPendingIds.Contains(orderId) || now - placedAt < CopyDefaults.PendingCorrelationTimeout) continue;
            await CancelDestinationPendingsAsync(session, orderId, ct);
            _mirroredPendingOrders.Remove(orderId);
            _mirroredPendingPlacedAt.Remove(orderId);
            logger.CopyPendingTimedOut(plan.ProfileId.Value, orderId);
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

    // G4 bounded-concurrency fan-out: dispatch a per-destination action to all destinations at once (up to
    // MaxDestinationConcurrency) instead of sequentially, so the Nth slave doesn't queue behind the first
    // N-1. The event handler still holds _stateGate, so the guard/token/flatten loops can't mutate host
    // state concurrently; the only host state a body writes is thread-safe (_symbolDetails,
    // _destinationHealth — both ConcurrentDictionary, keyed per destination), everything else it reads is
    // read-only during the gate-held event. Each body isolates its own failures, so cross-destination
    // ordering never changes the converged book.
    private Task ForEachDestinationAsync(Func<CopyDestinationPlan, CancellationToken, Task> body, CancellationToken ct)
        => Parallel.ForEachAsync(plan.Destinations,
            new ParallelOptions { MaxDegreeOfParallelism = CopyDefaults.MaxDestinationConcurrency, CancellationToken = ct },
            async (destination, token) => await body(destination, token));

    private async Task HandleOpenAsync(IOpenApiTradingSession session, ExecutionEvent execution, CancellationToken ct)
    {
        if (_openSourcePositions.Contains(execution.PositionId))
        {
            await HandlePositionUpdateAsync(session, execution, ct);
            return;
        }

        if (!_sourceSymbolNames.TryGetValue(execution.SymbolId, out var sourceName)) return;

        // News-window pause (opt-in): skip mirroring a fresh open while the symbol is in a high-impact blackout.
        if (_newsBlackout is not null && await _newsBlackout(sourceName, ct))
        {
            CopyMetrics.Instance.CopySkipped("news_blackout");
            logger.CopySkipped(plan.ProfileId.Value, plan.SourceCtidTraderAccountId, execution.PositionId, "news_blackout");
            return;
        }

        // A mirrored pending order just filled into this position: retire the resting destination
        // pending(s) so the copy re-opens as a labelled market position (uniform id-based reconcile).
        if (execution.OrderId != 0 && _mirroredPendingOrders.Remove(execution.OrderId))
        {
            _mirroredPendingPlacedAt.Remove(execution.OrderId);
            await CancelDestinationPendingsAsync(session, execution.OrderId, ct);
        }

        _openSourcePositions.Add(execution.PositionId);
        _sourceVolumes[execution.PositionId] = execution.Volume;
        _sourceStops[execution.PositionId] = execution.StopLoss;

        var sourceDetail = await SymbolDetailAsync(session, plan.SourceCtidTraderAccountId, execution.SymbolId, ct);
        var sourceLots = VolumeConversion.LotsFromProtocol(execution.Volume, sourceDetail.LotSize);
        var sourceSnapshot = await AccountSnapshotAsync(session, plan.SourceCtidTraderAccountId, AnyDestinationNeedsEquity(), ct);
        logger.CopySourceOpen(plan.ProfileId.Value, execution.PositionId, sourceName,
            execution.IsBuy ? "Buy" : "Sell", sourceLots);

        var dispatchStart = timeProvider.GetTimestamp();
        await ForEachDestinationAsync(async (destination, token) =>
        {
            try
            {
                await CopyOpenToDestinationAsync(session, destination, execution, sourceName,
                    sourceDetail, sourceSnapshot, applyProtection: true, isScaleIn: false, fromResync: false, token);
            }
            catch (Exception ex)
            {
                CopyMetrics.Instance.CopyFailed(destination.CtidTraderAccountId);
                // A token-invalid failure has its own M1 alert + auto-recovery path, so it must not also
                // feed the rejection breaker (that would trip a destination for a transient auth issue).
                if (!NoteIfTokenInvalidated(ex, destination.CtidTraderAccountId))
                    RecordCopyFailure(destination.CtidTraderAccountId);
                logger.CopyOpenFailed(plan.ProfileId.Value, destination.CtidTraderAccountId, execution.PositionId, ex);
                _sink.Record(new CopyExecutionRecord(plan.ProfileId, destination.CtidTraderAccountId,
                    execution.PositionId, sourceName, CopyExecutionKind.Failed, execution.IsBuy, execution.Volume,
                    execution.Price, null, EventAge(execution).TotalMilliseconds, ex.GetType().Name,
                    timeProvider.GetUtcNow()));
            }
        }, ct);
        CopyMetrics.Instance.RecordDispatchDuration(timeProvider.GetElapsedTime(dispatchStart).TotalMilliseconds);
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

        await ForEachDestinationAsync(async (destination, token) =>
        {
            if (!destination.Config.MirrorPartialClose) return;
            // G5: a partial close is proportional management — the open fill is no longer the target.
            _pendingTrueUp.TryRemove((destination.CtidTraderAccountId, execution.PositionId), out _);
            try
            {
                var positions = await DestinationPositionsAsync(session, destination.CtidTraderAccountId, token);
                foreach (var position in positions.Where(p => p.Label == label).ToList())
                {
                    var detail = await SymbolDetailAsync(session, destination.CtidTraderAccountId, position.SymbolId, token);
                    var slice = SliceVolume(position.Volume * closedFraction, position.Volume, detail);
                    if (slice <= 0) continue;
                    await session.ClosePositionAsync(destination.CtidTraderAccountId, position.PositionId, slice, token);
                    BookReduce(destination.CtidTraderAccountId, position.PositionId, slice);
                    logger.CopyPartialClose(plan.ProfileId.Value, destination.CtidTraderAccountId,
                        position.PositionId, slice, execution.PositionId);
                }
            }
            catch (Exception ex)
            {
                NoteIfTokenInvalidated(ex, destination.CtidTraderAccountId);
                logger.CopyCloseFailed(plan.ProfileId.Value, destination.CtidTraderAccountId, execution.PositionId, ex);
            }
        }, ct);
    }

    private async Task MirrorScaleInAsync(
        IOpenApiTradingSession session, ExecutionEvent execution, long addedVolume, CancellationToken ct)
    {
        if (!_sourceSymbolNames.TryGetValue(execution.SymbolId, out var sourceName)) return;
        var sourceDetail = await SymbolDetailAsync(session, plan.SourceCtidTraderAccountId, execution.SymbolId, ct);
        var sourceSnapshot = await AccountSnapshotAsync(session, plan.SourceCtidTraderAccountId, AnyDestinationNeedsEquity(), ct);
        var scaleInExecution = execution with { Volume = addedVolume };

        await ForEachDestinationAsync(async (destination, token) =>
        {
            if (!destination.Config.MirrorScaleIn) return;
            // G5: the position is now under proportional management — stop trying to true up its open fill.
            _pendingTrueUp.TryRemove((destination.CtidTraderAccountId, execution.PositionId), out _);
            try
            {
                await CopyOpenToDestinationAsync(session, destination, scaleInExecution, sourceName,
                    sourceDetail, sourceSnapshot, applyProtection: false, isScaleIn: true, fromResync: false, token);
            }
            catch (Exception ex)
            {
                NoteIfTokenInvalidated(ex, destination.CtidTraderAccountId);
                logger.CopyOpenFailed(plan.ProfileId.Value, destination.CtidTraderAccountId, execution.PositionId, ex);
            }
        }, ct);
    }

    private async Task MirrorStopChangeAsync(IOpenApiTradingSession session, ExecutionEvent execution, CancellationToken ct)
    {
        var label = execution.PositionId.ToString();
        await ForEachDestinationAsync(async (destination, token) =>
        {
            if (!destination.Config.CopyStopLoss && !destination.Config.CopyTrailingStop) return;
            try
            {
                var positions = await DestinationPositionsAsync(session, destination.CtidTraderAccountId, token);
                var match = positions.FirstOrDefault(p => p.Label == label);
                if (match is null) return;

                var trailing = destination.Config.CopyTrailingStop && execution.TrailingStopLoss;
                var stopLoss = destination.Config.CopyStopLoss ? execution.StopLoss : null;
                if (destination.Config.Reverse) stopLoss = destination.Config.CopyTakeProfit ? execution.TakeProfit : stopLoss;
                var detail = await SymbolDetailAsync(session, destination.CtidTraderAccountId, match.SymbolId, token);
                var roundedStop = RoundToDigits(stopLoss, detail.Digits);
                await session.AmendPositionSltpAsync(destination.CtidTraderAccountId, match.PositionId,
                    roundedStop, null, trailing, token);
                BookSetStop(destination.CtidTraderAccountId, match.PositionId, roundedStop, trailing);

                if (trailing) logger.CopyTrailingApplied(plan.ProfileId.Value, destination.CtidTraderAccountId, execution.PositionId);
                else logger.CopyStopLossAmended(plan.ProfileId.Value, destination.CtidTraderAccountId, execution.PositionId, stopLoss ?? 0);
            }
            catch (Exception ex)
            {
                NoteIfTokenInvalidated(ex, destination.CtidTraderAccountId);
                logger.CopyOpenFailed(plan.ProfileId.Value, destination.CtidTraderAccountId, execution.PositionId, ex);
            }
        }, ct);
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
        var sourceSnapshot = await AccountSnapshotAsync(session, plan.SourceCtidTraderAccountId, AnyDestinationNeedsEquity(), ct);
        var price = execution.OrderKind is CopyOrderKind.Stop or CopyOrderKind.StopLimit
            ? execution.StopPrice ?? 0
            : execution.LimitPrice ?? 0;
        var placedAny = 0;

        await ForEachDestinationAsync(async (destination, token) =>
        {
            if (!destination.Config.CopyPendingOrders || destination.Config.ManageOnly) return;
            if (!destination.Config.TradingHours.IsOpenAt((int)timeProvider.GetUtcNow().TimeOfDay.TotalMinutes))
            {
                logger.CopySkipped(plan.ProfileId.Value, destination.CtidTraderAccountId, execution.OrderId, "trading_hours");
                return;
            }
            if (!destination.Config.IsSourceLabelAllowed(execution.SourceLabel))
            {
                logger.CopySkipped(plan.ProfileId.Value, destination.CtidTraderAccountId, execution.OrderId, "source_label");
                return;
            }
            if (!destination.Config.IsOrderTypeAllowed(CopyDecisionEngine.ToOrderTypes(execution.OrderKind)))
            {
                logger.CopySkipped(plan.ProfileId.Value, destination.CtidTraderAccountId, execution.OrderId, "order_type");
                return;
            }
            try
            {
                if (await PlacePendingToDestinationAsync(session, destination, execution, sourceName,
                        sourceDetail, sourceSnapshot, price, token))
                    Interlocked.Exchange(ref placedAny, 1);
            }
            catch (Exception ex)
            {
                NoteIfTokenInvalidated(ex, destination.CtidTraderAccountId);
                logger.CopyOpenFailed(plan.ProfileId.Value, destination.CtidTraderAccountId, execution.OrderId, ex);
            }
        }, ct);

        if (placedAny == 1)
        {
            _mirroredPendingOrders.Add(execution.OrderId);
            _mirroredPendingPlacedAt[execution.OrderId] = timeProvider.GetUtcNow();
        }
    }

    private async Task<bool> PlacePendingToDestinationAsync(IOpenApiTradingSession session, CopyDestinationPlan destination,
        ExecutionEvent execution, string sourceName, SymbolDetails sourceDetail, AccountSnapshot sourceSnapshot, double price,
        CancellationToken ct)
    {
        var destinationName = destination.Config.ResolveDestinationSymbol(sourceName);
        if (!_destinationSymbolIds[destination.CtidTraderAccountId].TryGetValue(Normalize(destinationName), out var destinationSymbolId))
            return false;

        var destinationDetail = await SymbolDetailAsync(session, destination.CtidTraderAccountId, destinationSymbolId, ct);
        var destinationSnapshot = await AccountSnapshotAsync(
            session, destination.CtidTraderAccountId, NeedsEquity(destination.Config.Risk.Mode), ct);
        var sourceLots = VolumeConversion.LotsFromProtocol(execution.Volume, sourceDetail.LotSize);

        var decision = decisions.DecideOpen(destination.Config, new OpenDecisionContext(
            new SourcePosition(execution.OrderId, sourceName, execution.IsBuy, sourceLots, price,
                execution.StopLoss, execution.TakeProfit),
            sourceSnapshot, destinationSnapshot,
            Spec(sourceDetail), Spec(destinationDetail), price, Math.Pow(10, -destinationDetail.PipPosition),
            EventAge(execution), CopyDecisionEngine.ToOrderTypes(execution.OrderKind), execution.SlippageInPoints,
            destination.Config.ResolveVolumeMultiplier(sourceName),
            destination.Config.RiskFallbackLots));
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

        await ForEachDestinationAsync(async (destination, token) =>
        {
            if (!destination.Config.CopyPendingOrders || destination.Config.ManageOnly) return;
            try
            {
                var expiry = destination.Config.CopyPendingExpiry ? execution.ExpirationTimestamp : null;
                var slippage = execution.OrderKind == CopyOrderKind.StopLimit && destination.Config.CopyMasterSlippage
                    ? execution.SlippageInPoints
                    : null;
                var pendings = await session.ReconcilePendingOrdersAsync(destination.CtidTraderAccountId, token);
                foreach (var pending in pendings.Where(o => o.Label == label))
                {
                    await session.AmendPendingOrderAsync(destination.CtidTraderAccountId, pending.OrderId,
                        execution.OrderKind, pending.Volume, price, expiry, slippage, token);
                    logger.CopyPendingOrderAmended(plan.ProfileId.Value, destination.CtidTraderAccountId,
                        pending.OrderId, execution.OrderId);
                }
            }
            catch (Exception ex)
            {
                NoteIfTokenInvalidated(ex, destination.CtidTraderAccountId);
                logger.CopyOpenFailed(plan.ProfileId.Value, destination.CtidTraderAccountId, execution.OrderId, ex);
            }
        }, ct);
    }

    private async Task HandlePendingCancelledAsync(IOpenApiTradingSession session, ExecutionEvent execution, CancellationToken ct)
    {
        _mirroredPendingOrders.Remove(execution.OrderId);
        _mirroredPendingPlacedAt.Remove(execution.OrderId);
        await CancelDestinationPendingsAsync(session, execution.OrderId, ct);
    }

    private async Task CancelDestinationPendingsAsync(IOpenApiTradingSession session, long sourceOrderId, CancellationToken ct)
    {
        var label = sourceOrderId.ToString();
        await ForEachDestinationAsync(async (destination, token) =>
        {
            try
            {
                var pendings = await session.ReconcilePendingOrdersAsync(destination.CtidTraderAccountId, token);
                foreach (var pending in pendings.Where(o => o.Label == label))
                {
                    await session.CancelOrderAsync(destination.CtidTraderAccountId, pending.OrderId, token);
                    logger.CopyPendingOrderCancelled(plan.ProfileId.Value, destination.CtidTraderAccountId,
                        pending.OrderId, sourceOrderId);
                }
            }
            catch (Exception ex)
            {
                NoteIfTokenInvalidated(ex, destination.CtidTraderAccountId);
                logger.CopyCloseFailed(plan.ProfileId.Value, destination.CtidTraderAccountId, sourceOrderId, ex);
            }
        }, ct);
    }

    private async Task CopyOpenToDestinationAsync(IOpenApiTradingSession session, CopyDestinationPlan destination,
        ExecutionEvent execution, string sourceName, SymbolDetails sourceDetail, AccountSnapshot sourceSnapshot,
        bool applyProtection, bool isScaleIn, bool fromResync, CancellationToken ct)
    {
        if (_protectedDestinations.Contains(destination.CtidTraderAccountId))
        {
            CopyMetrics.Instance.CopySkipped("account_protection");
            return;
        }

        if (_lockedOutDestinations.Contains(destination.CtidTraderAccountId))
        {
            CopyMetrics.Instance.CopySkipped("prop_lockout");
            return;
        }

        if (destination.Config.ManageOnly)
        {
            CopyMetrics.Instance.CopySkipped("manage_only");
            return;
        }

        // C18 trading-hours window: no new opens outside the destination's configured UTC window.
        if (!destination.Config.TradingHours.IsOpenAt((int)timeProvider.GetUtcNow().TimeOfDay.TotalMinutes))
        {
            CopyMetrics.Instance.CopySkipped("trading_hours");
            return;
        }

        // C18 source-label filter (magic-number equivalent): copy only master trades whose label matches.
        if (!destination.Config.IsSourceLabelAllowed(execution.SourceLabel))
        {
            CopyMetrics.Instance.CopySkipped("source_label");
            return;
        }

        // G8 circuit breaker gates only LIVE opens (suppresses a per-event rejection storm). A resync is the
        // deliberate source-of-truth reconciliation — it must still reconverge a tripped destination's book
        // (open the master's missing positions), so it bypasses the breaker. The account-protection and
        // prop-lockout latches above are real safety stops and are honored even on resync.
        if (!fromResync && IsDestinationTripped(destination.CtidTraderAccountId))
        {
            CopyMetrics.Instance.CopySkipped("circuit_open");
            return;
        }

        var destinationName = destination.Config.ResolveDestinationSymbol(sourceName);
        if (!_destinationSymbolIds[destination.CtidTraderAccountId].TryGetValue(Normalize(destinationName), out var destinationSymbolId))
            return;

        var destinationDetail = await SymbolDetailAsync(session, destination.CtidTraderAccountId, destinationSymbolId, ct);
        var destinationSnapshot = await AccountSnapshotAsync(
            session, destination.CtidTraderAccountId, NeedsEquity(destination.Config.Risk.Mode), ct);
        var sourceLots = VolumeConversion.LotsFromProtocol(execution.Volume, sourceDetail.LotSize);

        var decision = decisions.DecideOpen(destination.Config, new OpenDecisionContext(
            new SourcePosition(execution.PositionId, sourceName, execution.IsBuy, sourceLots,
                execution.Price, execution.StopLoss, execution.TakeProfit),
            sourceSnapshot,
            destinationSnapshot,
            Spec(sourceDetail),
            Spec(destinationDetail),
            execution.Price,
            Math.Pow(10, -destinationDetail.PipPosition),
            EventAge(execution),
            CopyDecisionEngine.ToOrderTypes(execution.OrderKind),
            execution.SlippageInPoints,
            destination.Config.ResolveVolumeMultiplier(sourceName),
            destination.Config.RiskFallbackLots));

        if (decision.Kind != CopyActionKind.Open)
        {
            var reason = decision.SkipReason ?? "unknown";
            CopyMetrics.Instance.CopySkipped(reason);
            logger.CopySkipped(plan.ProfileId.Value, destination.CtidTraderAccountId, execution.PositionId, reason);
            return;
        }

        var effectiveBuy = destination.Config.Reverse ? !execution.IsBuy : execution.IsBuy;
        var wireVolume = VolumeConversion.ProtocolFromLots(decision.Lots, destinationDetail.LotSize);
        if (wireVolume <= 0)
        {
            CopyMetrics.Instance.CopySkipped("size_zero");
            logger.CopySkipped(plan.ProfileId.Value, destination.CtidTraderAccountId, execution.PositionId, "size_zero");
            return;
        }

        double? baseSlippagePrice = null;
        if (decision.SlippageInPoints is not null)
        {
            var (bid, ask) = await session.LoadSpotPriceAsync(destination.CtidTraderAccountId, destinationSymbolId, ct);
            baseSlippagePrice = effectiveBuy ? ask : bid;
        }

        // C11 execution jitter: a small random delay de-correlates order timestamps across the user's own
        // accounts (opt-in; compliance aid for firms that permit copying, not evasion of one that forbids it).
        if (destination.Config.ExecutionJitterMaxMs > 0)
            await Task.Delay(TimeSpan.FromMilliseconds(Random.Shared.Next(destination.Config.ExecutionJitterMaxMs + 1)), timeProvider, ct);

        await session.SendMarketOrderAsync(destination.CtidTraderAccountId, destinationSymbolId,
            effectiveBuy, wireVolume, execution.PositionId.ToString(), ct, decision.SlippageInPoints, baseSlippagePrice);
        CopyMetrics.Instance.CopyPlaced(destination.CtidTraderAccountId);
        CopyMetrics.Instance.RecordLatency(EventAge(execution).TotalMilliseconds);
        RecordCopySuccess(destination.CtidTraderAccountId);
        // G5: remember the requested wire volume so the next resync can true up a broker partial fill to
        // target. Only a fresh open is tracked; a scale-in is proportional management, not a fill to verify.
        if (!isScaleIn)
            _pendingTrueUp[(destination.CtidTraderAccountId, execution.PositionId)] = wireVolume;
        // G7: a new slave position now exists whose broker-assigned id we can't know without a reconcile —
        // mark the cache cold so the next mirror read rebuilds it.
        InvalidateBook(destination.CtidTraderAccountId);
        _sink.Record(new CopyExecutionRecord(plan.ProfileId, destination.CtidTraderAccountId,
            execution.PositionId, destinationName, CopyExecutionKind.Opened, effectiveBuy, wireVolume,
            execution.Price, decision.SlippageInPoints, EventAge(execution).TotalMilliseconds, null,
            timeProvider.GetUtcNow()));
        if (decision.SlippageInPoints is { } slippagePoints)
        {
            CopyMetrics.Instance.RecordSlippage(slippagePoints);
            logger.CopyMarketRangeSlippage(plan.ProfileId.Value, destination.CtidTraderAccountId, execution.PositionId, slippagePoints);
        }
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
                var detail = await SymbolDetailAsync(session, destination.CtidTraderAccountId, match.SymbolId, ct);
                await session.AmendPositionSltpAsync(destination.CtidTraderAccountId, match.PositionId,
                    RoundToDigits(stopLoss, detail.Digits), RoundToDigits(takeProfit, detail.Digits), trailing, ct);
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

        await ForEachDestinationAsync(async (destination, token) =>
        {
            _pendingTrueUp.TryRemove((destination.CtidTraderAccountId, sourcePositionId), out _); // G5: position gone
            try
            {
                var positions = await DestinationPositionsAsync(session, destination.CtidTraderAccountId, token);
                foreach (var position in positions.Where(p => p.Label == label).ToList())
                {
                    await session.ClosePositionAsync(destination.CtidTraderAccountId, position.PositionId, position.Volume, token);
                    BookRemove(destination.CtidTraderAccountId, position.PositionId);
                    logger.CopyPositionClosed(plan.ProfileId.Value, destination.CtidTraderAccountId,
                        position.PositionId, sourcePositionId);
                }
            }
            catch (Exception ex)
            {
                NoteIfTokenInvalidated(ex, destination.CtidTraderAccountId);
                logger.CopyCloseFailed(plan.ProfileId.Value, destination.CtidTraderAccountId, sourcePositionId, ex);
            }
        }, ct);
    }

    private async Task ResyncAsync(IOpenApiTradingSession session, CancellationToken ct)
    {
        var isInitialResync = !_hasResynced;
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
        foreach (var id in _mirroredPendingPlacedAt.Keys.Where(id => !sourcePendingIds.Contains(id)).ToArray())
            _mirroredPendingPlacedAt.Remove(id);

        var orphansClosed = 0;
        var sourceSnapshot = await AccountSnapshotAsync(session, plan.SourceCtidTraderAccountId, AnyDestinationNeedsEquity(), ct);
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
                    // Sync-Closed-off: on the first resync, leave copies the master closed while the profile
                    // was stopped. A mid-run reconnect always closes orphans so a desync converges.
                    if (isInitialResync && !destination.Config.SyncClosedOnStart) continue;
                    // M8 robust sync-closed: a position the broker already closed (POSITION_NOT_FOUND) must
                    // not abort the whole resync — close each orphan independently and move on.
                    try
                    {
                        await session.ClosePositionAsync(destination.CtidTraderAccountId, position.PositionId, position.Volume, ct);
                        orphansClosed++;
                        _pendingTrueUp.TryRemove((destination.CtidTraderAccountId, sourceId), out _); // G5: gone
                    }
                    catch (Exception ex)
                    {
                        NoteIfTokenInvalidated(ex, destination.CtidTraderAccountId);
                        logger.CopyCloseFailed(plan.ProfileId.Value, destination.CtidTraderAccountId, sourceId, ex);
                    }
                }
            }

            // Master positions opened before the profile started, or while the socket was dropped, have no
            // labelled copy on the destination yet. Open them now (labelled by source id) so a start with
            // pre-existing trades and a mid-run desync both converge to the master's live state.
            foreach (var source in sourcePositions)
            {
                if (mirroredSourceIds.Contains(source.PositionId)) continue;
                // Sync-Open-off: on the first resync, don't open copies for the master's pre-existing trades.
                // A mid-run reconnect always opens missing copies so a desync (master traded while dropped)
                // converges.
                if (isInitialResync && !destination.Config.SyncOpenOnStart) continue;
                if (!_sourceSymbolNames.TryGetValue(source.SymbolId, out var sourceName)) continue;

                var sourceDetail = await SymbolDetailAsync(session, plan.SourceCtidTraderAccountId, source.SymbolId, ct);
                var syntheticOpen = new ExecutionEvent(plan.SourceCtidTraderAccountId, "RESYNC_OPEN",
                    source.PositionId, source.SymbolId, source.IsBuy, source.Volume, 0,
                    source.StopLoss, null, IsOpen: true, TrailingStopLoss: source.TrailingStopLoss,
                    SourceLabel: source.Label);
                try
                {
                    await CopyOpenToDestinationAsync(session, destination, syntheticOpen, sourceName,
                        sourceDetail, sourceSnapshot, applyProtection: true, isScaleIn: false, fromResync: true, ct);
                }
                catch (Exception ex)
                {
                    logger.CopyOpenFailed(plan.ProfileId.Value, destination.CtidTraderAccountId, source.PositionId, ex);
                }
            }

            await TrueUpPartialFillsAsync(session, destination.CtidTraderAccountId, sourceOpenIds, ct);

            var destinationPendings = await session.ReconcilePendingOrdersAsync(destination.CtidTraderAccountId, ct);
            foreach (var pending in destinationPendings)
                if (long.TryParse(pending.Label, out var sourceOrderId) && !sourcePendingIds.Contains(sourceOrderId))
                    await session.CancelOrderAsync(destination.CtidTraderAccountId, pending.OrderId, ct);

            // G7: the resync mutated the slave book (opened missing, closed orphans, trued up) — drop the
            // cache so the next mirror read reconciles the freshly converged state.
            InvalidateBook(destination.CtidTraderAccountId);
        }

        _hasResynced = true;
        logger.CopyResync(plan.ProfileId.Value, sourceOpenIds.Count, orphansClosed);
    }

    // G5 partial-fill true-up: for each fresh copy open awaiting fill verification on this destination,
    // compare the slave's filled volume (summed over any copies carrying the source label) against the
    // requested target. If the broker filled short by at least one whole lot-step, place a top-up market
    // order to reach target and emit SlaveVolumeReconciled. One-shot: every entry is cleared after this pass
    // (a lifecycle event already removes it earlier), so a subsequent partial close / scale-in is never
    // undone. A source id no longer open is dropped; a missing copy is left for the open-missing pass.
    private async Task TrueUpPartialFillsAsync(
        IOpenApiTradingSession session, long ctid, HashSet<long> sourceOpenIds, CancellationToken ct)
    {
        var keys = _pendingTrueUp.Keys.Where(k => k.Ctid == ctid).ToArray();
        if (keys.Length == 0) return;

        var positions = await session.ReconcileAsync(ctid, ct);
        foreach (var key in keys)
        {
            if (!_pendingTrueUp.TryGetValue(key, out var requested)) continue;
            var matches = positions.Where(p => p.Label == key.SourceId.ToString()).ToList();
            if (matches.Count == 0)
            {
                if (!sourceOpenIds.Contains(key.SourceId)) _pendingTrueUp.TryRemove(key, out _);
                continue; // copy not present yet — the open-missing pass reopens it and re-arms the true-up
            }

            _pendingTrueUp.TryRemove(key, out _); // one-shot regardless of outcome
            var filled = matches.Sum(p => p.Volume);
            var template = matches[0];
            var detail = await SymbolDetailAsync(session, ctid, template.SymbolId, ct);
            var step = detail.StepVolume > 0 ? detail.StepVolume : 1;
            var shortfall = requested - filled;
            if (shortfall < step || shortfall < detail.MinVolume) continue;
            try
            {
                await session.SendMarketOrderAsync(ctid, template.SymbolId, template.IsBuy, shortfall,
                    key.SourceId.ToString(), ct);
                logger.CopySlaveVolumeReconciled(plan.ProfileId.Value, ctid, template.PositionId,
                    shortfall, requested, filled);
            }
            catch (Exception ex)
            {
                NoteIfTokenInvalidated(ex, ctid);
                logger.CopyOpenFailed(plan.ProfileId.Value, ctid, key.SourceId, ex);
            }
        }
    }

    // G7: the destination's open positions from the local cache when warm, else a reconcile that warms it.
    // Used by the mirror read paths (partial close, stop change, full close). ApplyProtection and the
    // resync/true-up deliberately keep direct reconciles — they poll for or rebuild fresh state.
    private async Task<IReadOnlyList<OpenPositionSnapshot>> DestinationPositionsAsync(
        IOpenApiTradingSession session, long ctid, CancellationToken ct)
    {
        if (_destinationBook.TryGetValue(ctid, out var cached) && cached is not null) return cached;
        var fresh = (await session.ReconcileAsync(ctid, ct)).ToList();
        _destinationBook[ctid] = fresh;
        return fresh;
    }

    // Marks the destination's cached book cold: the next read reconciles it. Called after any open (the new
    // position id is broker-assigned and unknowable without a reconcile) and after a flatten.
    private void InvalidateBook(long ctid) => _destinationBook[ctid] = null;

    private void BookRemove(long ctid, long positionId)
    {
        if (_destinationBook.TryGetValue(ctid, out var book) && book is not null)
            book.RemoveAll(p => p.PositionId == positionId);
    }

    private void BookReduce(long ctid, long positionId, long by)
    {
        if (!_destinationBook.TryGetValue(ctid, out var book) || book is null) return;
        var index = book.FindIndex(p => p.PositionId == positionId);
        if (index < 0) return;
        var remaining = book[index].Volume - by;
        if (remaining <= 0) book.RemoveAt(index);
        else book[index] = book[index] with { Volume = remaining };
    }

    private void BookSetStop(long ctid, long positionId, double? stopLoss, bool trailing)
    {
        if (!_destinationBook.TryGetValue(ctid, out var book) || book is null) return;
        var index = book.FindIndex(p => p.PositionId == positionId);
        if (index >= 0) book[index] = book[index] with { StopLoss = stopLoss, TrailingStopLoss = trailing };
    }

    private async Task<SymbolDetails> SymbolDetailAsync(
        IOpenApiTradingSession session, long ctid, long symbolId, CancellationToken ct)
    {
        if (_symbolDetails.TryGetValue((ctid, symbolId), out var cached)) return cached;
        var details = await session.LoadSymbolDetailsAsync(ctid, [symbolId], ct);
        var detail = details.Count > 0 ? details[0] : new SymbolDetails(symbolId, 0, 0, 0, 0);
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

    // Real copy latency (fixes G1): master event server timestamp -> now, via the injected clock. The
    // decision engine skips a stale signal past the destination's max-lag. Synthetic resync opens carry no
    // timestamp, so they age to zero and are never latency-skipped.
    private TimeSpan EventAge(ExecutionEvent execution)
    {
        if (execution.ServerTimestamp is not { } serverTimestamp) return TimeSpan.Zero;
        var age = timeProvider.GetUtcNow() - DateTimeOffset.FromUnixTimeMilliseconds(serverTimestamp);
        return age > TimeSpan.Zero ? age : TimeSpan.Zero;
    }

    // M1: a trading call rejected because the account's access token was invalidated (a partial/again-auth
    // on the cID kills the old token). Surface it as a distinct alert so the operator/notification channel
    // knows to recover; the supervisor's next cycle pushes the refreshed token and the in-place swap
    // resumes copying — no manual re-add. Returns true when the exception was a token-invalid one.
    private bool NoteIfTokenInvalidated(Exception ex, long ctid)
    {
        if (ex is not OpenApiException { Error.Kind: OpenApiErrorKind.TokenInvalid } tokenException) return false;
        logger.CopyTokenInvalidated(plan.ProfileId.Value, ctid, tokenException.Error.Code);
        return true;
    }

    // G8: a destination is tripped (its rejection budget exhausted) and still inside its cooldown, so new
    // opens are paused. An elapsed cooldown auto-resets the breaker so copying resumes. Existing positions
    // are never gated by this — a tripped destination is still managed and closed.
    private bool IsDestinationTripped(long ctid)
    {
        if (!_destinationHealth.TryGetValue(ctid, out var health) || health.TrippedUntil is not { } trippedUntil)
            return false;
        if (timeProvider.GetUtcNow() < trippedUntil) return true;
        _destinationHealth[ctid] = (0, null); // cooldown elapsed — resume
        return false;
    }

    private void RecordCopySuccess(long ctid) => _destinationHealth[ctid] = (0, null);

    // Counts a consecutive open failure; on breaching the rejection budget the destination trips for the
    // cooldown window and raises the Follower-Guard alert so a rejection storm can't keep firing orders.
    private void RecordCopyFailure(long ctid)
    {
        var failures = (_destinationHealth.TryGetValue(ctid, out var health) ? health.ConsecutiveFailures : 0) + 1;
        if (failures < CopyDefaults.RejectionBudget)
        {
            _destinationHealth[ctid] = (failures, null);
            return;
        }

        _destinationHealth[ctid] = (failures, timeProvider.GetUtcNow() + CopyDefaults.CircuitCooldown);
        logger.CopyDestinationTripped(plan.ProfileId.Value, ctid, failures, CopyDefaults.CircuitCooldown.TotalSeconds);
        _notifications.Notify(new CopyNotificationRecord(plan.ProfileId, ctid, CopyNotificationKind.DestinationTripped,
            CopyNotificationSeverity.Warning,
            $"Destination {ctid} paused after {failures} consecutive rejections (cooldown {CopyDefaults.CircuitCooldown.TotalSeconds:0}s).",
            timeProvider.GetUtcNow()));
    }

    // Real per-account sizing state (fixes G2). Balance is always read; equity is derived (the Open API
    // never delivers it) as balance + floating P&L only for the equity-proportional modes, so the common
    // path keeps its single balance round-trip. Free margin is reported as equity: used margin isn't
    // exposed by the reconcile surface, so equity is the honest available-funds proxy (was plain balance).
    private async Task<AccountSnapshot> AccountSnapshotAsync(
        IOpenApiTradingSession session, long ctid, bool needsEquity, CancellationToken ct)
    {
        var balance = await session.LoadBalanceAsync(ctid, ct);
        if (!needsEquity) return new AccountSnapshot(balance, balance, balance);

        var valuations = await session.LoadPositionValuationsAsync(ctid, ct);
        if (valuations.Count == 0) return new AccountSnapshot(balance, balance, balance);

        var pricing = new Dictionary<long, SymbolPricing>();
        foreach (var symbolId in valuations.Select(v => v.SymbolId).Distinct())
        {
            var (bid, ask) = await session.LoadSpotPriceAsync(ctid, symbolId, ct);
            pricing[symbolId] = new SymbolPricing(symbolId, bid, ask);
        }

        var equity = _equityCalculator.Compute(balance, valuations, pricing).Equity;
        return new AccountSnapshot(balance, equity, equity);
    }

    private bool AnyDestinationNeedsEquity() => plan.Destinations.Any(d => NeedsEquity(d.Config.Risk.Mode));

    private static bool NeedsEquity(MoneyManagementMode mode)
        => mode is MoneyManagementMode.ProportionalEquity or MoneyManagementMode.ProportionalFreeMargin;

    // M6: normalize an SL/TP price to the destination symbol's digit precision before amending. A master
    // price at the master symbol's precision (or a cross-broker digit mismatch) otherwise trips the real
    // server's INVALID_STOPLOSS_TAKEPROFIT. Digits == 0 means "unknown" -> leave the price untouched.
    private static double? RoundToDigits(double? price, int digits)
        => price is { } value && digits > 0 ? Math.Round(value, digits, MidpointRounding.AwayFromZero) : price;

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
