using Core;
using Core.Domain;
using CopyEngine;
using CTraderOpenApi.Client;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace UnitTests.CopyTrading;

// G2 regression: proportional-equity sizing must size off real account equity (balance + floating P&L),
// not plain balance. Before the fix the host fed Snapshot(balance,balance,balance), so a master sitting
// on floating profit sized its copies as if flat — the exact ROI-mismatch class users complain about.
// The fake models per-position valuations + spot, so equity is deterministic here (no live needed).
public sealed class CopyEquitySizingTests
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

    private static CopyDestination EquityDestination()
    {
        var profile = CopyProfile.Create(UserId.New(), "p", TradingAccountId.New());
        var destination = profile.AddDestination(TradingAccountId.New(), RiskSettings.Default);
        destination.ConfigureRisk(new RiskSettings(MoneyManagementMode.ProportionalEquity, 1));
        return destination;
    }

    private static CopyProfilePlan Plan(CopyDestination config)
        => new(CopyProfileId.New(), Live: false, "client", "secret", Source, "token", 1,
            [new CopyDestinationPlan(Slave, "t", 1, config)]);

    private static async Task DriveAsync(FakeTradingSession session, CopyProfilePlan plan, Func<Task> act)
    {
        var host = new CopyEngineHost(plan, new FakeTradingSessionFactory(session),
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

    // Master has a pre-existing long already mirrored on the slave (so resync opens nothing new). The
    // master position sits on floating profit that doubles master equity (10000 -> 20000) while the slave
    // copy is valued flat (equity == balance == 10000). A fresh master open of 1.0 lot must therefore copy
    // at equity ratio 10000/20000 = 0.5 lot = 50 wire units.
    [Fact]
    public async Task Proportional_equity_sizes_off_floating_pnl_not_balance()
    {
        var session = NewSession();
        session.Balance = 10_000;
        session.SetSpot(SymbolId, bid: 10_001, ask: 10_001);
        // pre-existing master long, entry 1.0 -> floating +10000 at bid 10001 (units = 100/100 = 1)
        session.SeedPosition(Source, positionId: 9001, SymbolId, isBuy: true, volume: 100, label: "9001");
        session.SetPositionValuation(9001, entryPrice: 1.0);
        // its slave mirror, valued flat (entry == bid) so slave equity stays at balance
        session.SeedPosition(Slave, positionId: 7001, SymbolId, isBuy: true, volume: 100, label: "9001");
        session.SetPositionValuation(7001, entryPrice: 10_001);

        await DriveAsync(session, Plan(EquityDestination()), async () =>
        {
            session.PushOpen(Source, positionId: 9002, SymbolId, isBuy: true, volume: 100);
            await WaitUntil(() => session.Orders.Any(o => o.Label == "9002"));
        });

        session.Orders.Single(o => o.Label == "9002").Volume
            .Should().Be(50, "the copy is sized on master equity (20000), not balance (10000)");
    }

    // Same setup but the master position is valued flat (entry == bid) -> master equity == balance, so the
    // fresh open copies 1:1 at equity ratio 1.0 = 100 wire units. Proves equity (not a constant) drives it.
    [Fact]
    public async Task Proportional_equity_with_flat_master_copies_one_to_one()
    {
        var session = NewSession();
        session.Balance = 10_000;
        session.SetSpot(SymbolId, bid: 10_001, ask: 10_001);
        session.SeedPosition(Source, positionId: 9101, SymbolId, isBuy: true, volume: 100, label: "9101");
        session.SetPositionValuation(9101, entryPrice: 10_001); // flat -> no floating P&L
        session.SeedPosition(Slave, positionId: 7101, SymbolId, isBuy: true, volume: 100, label: "9101");
        session.SetPositionValuation(7101, entryPrice: 10_001);

        await DriveAsync(session, Plan(EquityDestination()), async () =>
        {
            session.PushOpen(Source, positionId: 9102, SymbolId, isBuy: true, volume: 100);
            await WaitUntil(() => session.Orders.Any(o => o.Label == "9102"));
        });

        session.Orders.Single(o => o.Label == "9102").Volume
            .Should().Be(100, "with master equity == balance the equity ratio is 1.0");
    }
}
