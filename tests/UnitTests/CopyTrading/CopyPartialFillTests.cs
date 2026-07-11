using Core;
using Core.Domain;
using CopyEngine;
using CTraderOpenApi.Client;
using FluentAssertions;
using Xunit;

namespace UnitTests.CopyTrading;

// G5 slave partial-fill true-up: when the broker fills a copy open short of the requested volume, the next
// resync tops the slave up to target within one lot-step. The true-up is one-shot and never fights the
// proportional management of a position — a master partial close removes the pending true-up so no phantom
// top-up is issued.
public sealed class CopyPartialFillTests
{
    private const long Source = 100;
    private const long Slave = 200;
    private const long SymbolId = 1;
    private const int SlaveVolumeReconciledEventId = 1087;
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
    public async Task Resync_tops_up_a_broker_partial_fill_to_the_requested_target()
    {
        var session = NewSession();
        session.PartialFillFractionForCtid[Slave] = 0.5; // the broker fills only half the requested volume
        // The master already holds a 300-volume position; the profile mirrors it on start.
        session.SeedPosition(Source, positionId: 7001, SymbolId, isBuy: true, volume: 300, label: string.Empty);
        var log = new CapturingLogger();
        var plan = new CopyProfilePlan(CopyProfileId.New(), Live: false, "client", "secret", Source, "token", 1,
            [new CopyDestinationPlan(Slave, "t", 1, Destination())]);
        var host = new CopyEngineHost(plan, new FakeTradingSessionFactory(session),
            new CopyDecisionEngine(new CopySizingCalculator()), TimeProvider.System, log);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var run = Task.Run(() => host.RunAsync(cts.Token), CancellationToken.None);
        try
        {
            // The start-up resync opens the copy (requested 300, fake fills 150) and the same pass tops the
            // slave up by the 150 shortfall.
            await WaitUntil(() => log.Records.Any(r => r.EventId == SlaveVolumeReconciledEventId));
        }
        finally { cts.Cancel(); try { await run; } catch { /* cancellation */ } }

        session.Orders.Should().Contain(o => o.Ctid == Slave && o.Label == "7001" && o.Volume == 150,
            "the true-up issues a top-up market order for exactly the 150 shortfall (requested 300, filled 150)");
        log.Records.Should().Contain(r => r.EventId == SlaveVolumeReconciledEventId,
            "a SlaveVolumeReconciled event is emitted for the true-up");
    }

    [Fact]
    public async Task A_master_partial_close_cancels_the_true_up_so_no_phantom_top_up_is_issued()
    {
        var session = NewSession();
        session.PartialFillFractionForCtid[Slave] = 0.5; // broker fills half
        var log = new CapturingLogger();
        var plan = new CopyProfilePlan(CopyProfileId.New(), Live: false, "client", "secret", Source, "token", 1,
            [new CopyDestinationPlan(Slave, "t", 1,
                Destination(d => d.SetPartialCloseMirroring(mirrorPartialClose: true, mirrorScaleIn: true)))]);
        var host = new CopyEngineHost(plan, new FakeTradingSessionFactory(session),
            new CopyDecisionEngine(new CopySizingCalculator()), TimeProvider.System, log);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var run = Task.Run(() => host.RunAsync(cts.Token), CancellationToken.None);
        try
        {
            // Live open 300 (fills 150) records a pending true-up. The master then partially closes to 200 —
            // the copy is now under proportional management, so the open-fill true-up must be abandoned.
            session.PushOpen(Source, positionId: 7002, SymbolId, isBuy: true, volume: 300);
            await WaitUntil(() => session.Orders.Any(o => o.Ctid == Slave && o.Label == "7002"));
            session.PushOpen(Source, positionId: 7002, SymbolId, isBuy: true, volume: 200); // volume-down = partial close
            await WaitUntil(() => session.Closes.Any(c => c.Ctid == Slave));

            // Keep the master position alive (seed the source book) so the resync doesn't orphan-close it,
            // then reconnect: the true-up pass must find no pending entry and issue no top-up.
            session.SeedPosition(Source, positionId: 7002, SymbolId, isBuy: true, volume: 200, label: string.Empty);
            session.Disconnect();
            await session.ReconnectAsync(cts.Token);
            await Task.Delay(150); // give any (erroneous) true-up a chance to fire
        }
        finally { cts.Cancel(); try { await run; } catch { /* cancellation */ } }

        session.Orders.Count(o => o.Ctid == Slave && o.Label == "7002").Should().Be(1,
            "only the original open — a partial close cancels the true-up, so no top-up order is issued");
        log.Records.Should().NotContain(r => r.EventId == SlaveVolumeReconciledEventId,
            "no SlaveVolumeReconciled event fires once the position is under proportional management");
    }
}
