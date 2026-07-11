using System.Diagnostics;
using Core;
using Core.Domain;
using CopyEngine;
using CTraderOpenApi.Client;
using Microsoft.Extensions.Logging.Abstractions;
using UnitTests.CopyTrading;

namespace StressTests.CopyTrading;

// Deterministic-simulation-testing world for the copy engine, in the style used by TigerBeetle /
// FoundationDB: one CopyEngineHost is driven against the cTrader-faithful FakeTradingSession while a
// seeded workload issues randomized source orders interleaved with injected faults (socket flap, order
// rejection, market-range rejection, token rotation). The workload keeps a membership-consistent source
// book so a reconnect resync always has a truthful master state to converge destinations onto. Tests
// assert invariants at quiescence, not implementation details, and every failure reproduces from its seed.
internal sealed class CopyDstWorld : IAsyncDisposable
{
    private const long SourceCtid = 100;
    private const long SymbolId = 1;
    private static readonly SymbolDetails Details = new(SymbolId, LotSize: 100, StepVolume: 1, MinVolume: 1, PipPosition: 5);

    private readonly SyncTradingSession _session;
    private readonly CopyEngineHost _host;
    private readonly CancellationTokenSource _cts;
    private readonly Task _run;
    private readonly long[] _destinations;
    private readonly HashSet<long> _openSource = [];
    private readonly Dictionary<long, (bool IsBuy, long Volume)> _sourceState = new();
    private long _positionSeq = 1000;

    public IReadOnlyList<long> Destinations => _destinations;
    public SyncTradingSession Session => _session;
    public IReadOnlyCollection<long> OpenSourceIds => _openSource;
    public bool HostFaulted => _run.IsFaulted;
    public Exception? HostFault => _run.Exception;

    public CopyDstWorld(int destinationCount, Action<CopyDestination>? configure = null, TimeSpan? runBudget = null)
    {
        _session = new SyncTradingSession(new FakeTradingSession(
            new Dictionary<long, string> { [SymbolId] = "EURUSD" },
            new Dictionary<string, long> { ["EURUSD"] = SymbolId },
            Details));
        _session.SetSpot(SymbolId, 1.10, 1.10);

        _destinations = new long[destinationCount];
        var destinationPlans = new List<CopyDestinationPlan>(destinationCount);
        for (var i = 0; i < destinationCount; i++)
        {
            var ctid = 200 + i;
            _destinations[i] = ctid;
            destinationPlans.Add(new CopyDestinationPlan(ctid, "token", 1, BuildDestination(configure)));
        }

        var plan = new CopyProfilePlan(CopyProfileId.New(), Live: false, "client", "secret",
            SourceCtid, "token", 1, destinationPlans);
        _host = new CopyEngineHost(plan, new SyncTradingSessionFactory(_session),
            new CopyDecisionEngine(new CopySizingCalculator()), TimeProvider.System, NullLogger.Instance);
        _cts = new CancellationTokenSource(runBudget ?? TimeSpan.FromSeconds(60));
        _run = Task.Run(() => _host.RunAsync(_cts.Token), CancellationToken.None);
    }

    private static CopyDestination BuildDestination(Action<CopyDestination>? configure)
    {
        var profile = CopyProfile.Create(UserId.New(), "p", TradingAccountId.New());
        var destination = profile.AddDestination(TradingAccountId.New(), RiskSettings.Default);
        configure?.Invoke(destination);
        return destination;
    }

    // ---- source workload (single test thread, kept consistent with the fake source book) -----------

    public long OpenSource(bool isBuy, long volume)
    {
        var id = ++_positionSeq;
        _session.SeedPosition(SourceCtid, id, SymbolId, isBuy, volume, id.ToString());
        _session.PushOpen(SourceCtid, id, SymbolId, isBuy, volume);
        _openSource.Add(id);
        _sourceState[id] = (isBuy, volume);
        return id;
    }

    // Emits a volume-down update the host mirrors as a partial close. The fake source book keeps the
    // position at membership level only (its stored volume is irrelevant to label convergence), so a
    // partial never evicts the position from the book regardless of prior scale-ins.
    public void PartialCloseSource(long id, long newVolume)
    {
        if (!_sourceState.TryGetValue(id, out var state) || newVolume <= 0 || newVolume >= state.Volume) return;
        _session.PushOpen(SourceCtid, id, SymbolId, state.IsBuy, newVolume);
        _sourceState[id] = (state.IsBuy, newVolume);
    }

    public void ScaleInSource(long id, long addedVolume)
    {
        if (!_sourceState.TryGetValue(id, out var state) || addedVolume <= 0) return;
        var newVolume = state.Volume + addedVolume;
        _session.PushOpen(SourceCtid, id, SymbolId, state.IsBuy, newVolume);
        _sourceState[id] = (state.IsBuy, newVolume);
    }

    public async Task CloseSource(long id)
    {
        if (!_sourceState.TryGetValue(id, out var state)) return;
        _session.PushClose(SourceCtid, id, SymbolId, state.IsBuy, state.Volume);
        await _session.ClosePositionAsync(SourceCtid, id, long.MaxValue, CancellationToken.None);
        _openSource.Remove(id);
        _sourceState.Remove(id);
    }

    // ---- fault injection ---------------------------------------------------------------------------

    public void FailOrders(long destinationCtid) => _session.SetFailOrders(destinationCtid, fail: true);
    public void HealOrders(long destinationCtid) => _session.SetFailOrders(destinationCtid, fail: false);
    public void RejectMarketRange(long destinationCtid) => _session.SetRejectMarketRange(destinationCtid);

    public async Task FlapConnectionAsync()
    {
        _session.Disconnect();
        await _session.ReconnectAsync(_cts.Token);
    }

    public void RotateTokens(string token)
        => _host.PushTokenUpdate(_destinations.Select(ctid => (ctid, token)).ToList());

    // ---- invariants --------------------------------------------------------------------------------

    // Every healthy destination must, at rest, mirror exactly the set of still-open source positions —
    // no orphans, none missing. Convergence is asserted on the label *set*: a scale-in legitimately opens
    // a second destination position under the same source label, so duplicate labels are expected and are
    // not a divergence. Destinations with order rejection currently injected may lag, so they are skipped.
    public async Task<bool> WaitForConvergenceAsync(TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            if (await IsConvergedAsync()) return true;
            await Task.Delay(25);
        }
        return await IsConvergedAsync();
    }

    // Drives repeated reconnect-resyncs until the book converges, the way a live supervisor periodically
    // reconciles. Needed after a burst workload: events are consumed asynchronously, so a single resync can
    // land before the host has drained the queue. Re-resyncing until stable lets the last reconcile win.
    public async Task<bool> ReconcileUntilConvergedAsync(TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            await FlapConnectionAsync();
            await Task.Delay(50);
            if (await IsConvergedAsync()) return true;
        }
        return await IsConvergedAsync();
    }

    private async Task<bool> IsConvergedAsync()
    {
        var expected = _openSource.Select(id => id.ToString()).ToHashSet();
        foreach (var destination in _destinations)
        {
            if (_session.IsFailing(destination)) continue;
            List<string> labels;
            try
            {
                labels = (await _session.ReconcileAsync(destination, CancellationToken.None))
                    .Select(p => p.Label).ToList();
            }
            catch
            {
                return false; // host mutating the book concurrently; not yet quiescent
            }

            if (!labels.ToHashSet().SetEquals(expected)) return false;
        }

        return true;
    }

    public async Task<IReadOnlyList<string>> DestinationLabelsAsync(long destinationCtid)
        => (await _session.ReconcileAsync(destinationCtid, CancellationToken.None)).Select(p => p.Label).ToList();

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        try { await _run; } catch { /* cancellation expected */ }
        _cts.Dispose();
    }
}
