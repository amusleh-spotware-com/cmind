using Core;
using Core.Domain;
using CopyEngine;
using CTraderOpenApi.Client;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace UnitTests.CopyTrading;

// M4 (Phase 1): the prior copier's users reported "hit-and-miss" copying — a master SL move that didn't
// mirror, an op copied twice, an op missed. Determinism is our answer. These regressions pin the
// reliability contract: over a full master lifecycle every operation mirrors EXACTLY once — no misses, no
// duplicates — with the SL-movement case (the specific reported miss) called out on its own.
public sealed class CopyReliabilityTests
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

    private static CopyDestination Destination()
    {
        var profile = CopyProfile.Create(UserId.New(), "p", TradingAccountId.New());
        var destination = profile.AddDestination(TradingAccountId.New(), RiskSettings.Default);
        destination.SetCopyProtection(true, true);
        destination.SetPartialCloseMirroring(true, true);
        return destination;
    }

    private static CopyProfilePlan Plan()
        => new(CopyProfileId.New(), Live: false, "client", "secret", Source, "token", 1,
            [new CopyDestinationPlan(Slave, "t", 1, Destination())]);

    private static async Task DriveAsync(FakeTradingSession session, Func<Task> act)
    {
        var host = new CopyEngineHost(Plan(), new FakeTradingSessionFactory(session),
            new CopyDecisionEngine(new CopySizingCalculator()), TimeProvider.System, (ILogger)NullLogger.Instance);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var run = Task.Run(() => host.RunAsync(cts.Token), CancellationToken.None);
        try { await act(); }
        finally { cts.Cancel(); try { await run; } catch { /* cancellation */ } }
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
    public async Task Every_master_operation_mirrors_exactly_once_over_a_full_lifecycle()
    {
        var session = NewSession();

        await DriveAsync(session, async () =>
        {
            // open
            session.PushOpen(Source, positionId: 5001, SymbolId, isBuy: true, volume: 100, stopLoss: 1.09);
            await WaitUntil(() => session.Orders.Count == 1 && session.Amends.Count == 1);

            // stop-loss move -> exactly one further amend
            session.PushOpen(Source, positionId: 5001, SymbolId, isBuy: true, volume: 100, stopLoss: 1.085);
            await WaitUntil(() => session.Amends.Count == 2);

            // partial close 100 -> 40 -> exactly one proportional close on the single slave position
            session.PushOpen(Source, positionId: 5001, SymbolId, isBuy: true, volume: 40, stopLoss: 1.085);
            await WaitUntil(() => session.Closes.Count == 1);

            // full close -> exactly one further close (the remaining slave volume)
            session.PushClose(Source, 5001, SymbolId, isBuy: true, volume: 40);
            await WaitUntil(() => session.Closes.Count == 2);

            // settle: assert nothing fires a second time
            await Task.Delay(150);
        });

        session.Orders.Should().HaveCount(1, "the single master open is mirrored exactly once");
        session.Amends.Should().HaveCount(2, "protection at open + one stop-loss move, each exactly once");
        session.Closes.Should().HaveCount(2, "one partial close + one full close, each exactly once");
    }

    [Fact]
    public async Task Source_stop_loss_move_mirrors_exactly_once()
    {
        var session = NewSession();

        await DriveAsync(session, async () =>
        {
            session.PushOpen(Source, positionId: 5101, SymbolId, isBuy: true, volume: 100, stopLoss: 1.09);
            await WaitUntil(() => session.Amends.Count == 1);
            session.PushOpen(Source, positionId: 5101, SymbolId, isBuy: true, volume: 100, stopLoss: 1.08);
            await WaitUntil(() => session.Amends.Count == 2);
            await Task.Delay(150); // no third amend
        });

        session.Amends.Should().HaveCount(2);
        session.Amends[^1].StopLoss.Should().Be(1.08, "the moved stop-loss mirrors exactly once, to the new level");
    }
}
