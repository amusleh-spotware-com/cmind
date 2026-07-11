using Core;
using Core.Domain;
using CopyEngine;
using CTraderOpenApi.Client;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace UnitTests.CopyTrading;

// G7 local position cache: the read-heavy mirror paths serve from a per-destination book that the host
// keeps coherent through its own writes, so a burst of stop-loss changes on one position does not
// re-Reconcile the destination on every event. A reconnect resync remains the source of truth (it drops
// the cache), and correctness of the mirrored writes is covered by the broader mirror-scenario suite.
public sealed class CopyPositionCacheTests
{
    private const long Source = 100;
    private const long Slave = 200;
    private const long SymbolId = 1;
    private static readonly SymbolDetails Details = new(SymbolId, LotSize: 100, StepVolume: 1, MinVolume: 1, PipPosition: 5);

    private static FakeTradingSession NewSession()
        => new(
            new Dictionary<long, string> { [SymbolId] = "EURUSD" },
            new Dictionary<string, long> { ["EURUSD"] = SymbolId },
            Details);

    private static CopyDestination Destination(Action<CopyDestination>? configure = null)
    {
        var profile = CopyProfile.Create(UserId.New(), "p", TradingAccountId.New());
        var destination = profile.AddDestination(TradingAccountId.New(), RiskSettings.Default);
        configure?.Invoke(destination);
        return destination;
    }

    private static async Task WaitUntil(Func<bool> condition, TimeSpan? timeout = null)
    {
        var limit = timeout ?? TimeSpan.FromSeconds(3);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        while (stopwatch.Elapsed < limit)
        {
            if (condition()) return;
            await Task.Delay(20);
        }
        condition().Should().BeTrue("the expected host action did not occur in time");
    }

    [Fact]
    public async Task A_burst_of_stop_changes_reconciles_the_destination_only_once()
    {
        var session = NewSession();
        var plan = new CopyProfilePlan(CopyProfileId.New(), Live: false, "client", "secret", Source, "token", 1,
            [new CopyDestinationPlan(Slave, "t", 1, Destination(d => d.SetCopyProtection(true, false)))]);
        var host = new CopyEngineHost(plan, new FakeTradingSessionFactory(session),
            new CopyDecisionEngine(new CopySizingCalculator()), TimeProvider.System, NullLogger.Instance);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var run = Task.Run(() => host.RunAsync(cts.Token), CancellationToken.None);
        int before;
        try
        {
            // Open with no stop (so the initial protection pass issues no amend and no reconcile).
            session.PushOpen(Source, positionId: 7001, SymbolId, isBuy: true, volume: 100);
            await WaitUntil(() => session.Orders.Any(o => o.Ctid == Slave && o.Label == "7001"));
            before = session.ReconcileCountByCtid.GetValueOrDefault(Slave);

            // Five successive stop moves (a trailing-stop storm). The first warms the cache; the rest are
            // served from it, so only one reconcile is issued across the whole burst.
            for (var i = 1; i <= 5; i++)
                session.PushOpen(Source, positionId: 7001, SymbolId, isBuy: true, volume: 100,
                    stopLoss: 1.1000 - i * 0.0005);
            await WaitUntil(() => session.Amends.Count(a => a.Ctid == Slave) >= 5);
        }
        finally { cts.Cancel(); try { await run; } catch { /* cancellation */ } }

        session.Amends.Count(a => a.Ctid == Slave).Should().BeGreaterThanOrEqualTo(5, "every stop move is mirrored");
        (session.ReconcileCountByCtid.GetValueOrDefault(Slave) - before).Should().Be(1,
            "the cache serves the stop-change burst after a single warming reconcile");
    }
}
