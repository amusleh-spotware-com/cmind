using Core;
using Core.Domain;
using CopyEngine;
using CTraderOpenApi.Client;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace UnitTests.CopyTrading;

public sealed class CopyEngineHostTests
{
    private const long Source = 100;
    private const long Slave = 200;
    private const long Slave2 = 300;
    private const long SymbolId = 1;
    private static readonly SymbolDetails Details = new(SymbolId, LotSize: 100, StepVolume: 1, MinVolume: 1, PipPosition: 5);

    private static FakeTradingSession NewSession(IReadOnlyDictionary<string, long>? destinationSymbolIds = null)
        => new(
            new Dictionary<long, string> { [SymbolId] = "EURUSD" },
            destinationSymbolIds ?? new Dictionary<string, long> { ["EURUSD"] = SymbolId },
            Details);

    private static CopyDestination Destination(long _, Action<CopyDestination>? configure = null)
    {
        var profile = CopyProfile.Create(UserId.New(), "p", TradingAccountId.New());
        var destination = profile.AddDestination(TradingAccountId.New(), RiskSettings.Default);
        configure?.Invoke(destination);
        return destination;
    }

    private static CopyProfilePlan Plan(params CopyDestinationPlan[] destinations)
        => new(CopyProfileId.New(), Live: false, "client", "secret", Source, "token", destinations);

    private static async Task DriveAsync(FakeTradingSession session, CopyProfilePlan plan,
        Func<Task> act, CapturingLogger? logger = null)
    {
        var host = new CopyEngineHost(plan, new FakeTradingSessionFactory(session),
            new CopyDecisionEngine(new CopySizingCalculator()), logger ?? (ILogger)NullLogger.Instance);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var run = Task.Run(() => host.RunAsync(cts.Token), CancellationToken.None);
        try { await act(); }
        finally { cts.Cancel(); try { await run; } catch { /* cancellation */ } }
    }

    private static async Task WaitUntil(Func<bool> condition, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(3));
        while (DateTime.UtcNow < deadline)
        {
            if (condition()) return;
            await Task.Delay(20);
        }
        condition().Should().BeTrue("the expected host action did not occur in time");
    }

    [Fact]
    public async Task Open_mirrors_market_order_with_same_side_and_volume()
    {
        var session = NewSession();
        var plan = Plan(new CopyDestinationPlan(Slave, "t", Destination(Slave)));

        await DriveAsync(session, plan, async () =>
        {
            session.PushOpen(Source, positionId: 1001, SymbolId, isBuy: true, volume: 100);
            await WaitUntil(() => session.Orders.Count == 1);
        });

        var order = session.Orders.Single();
        order.Ctid.Should().Be(Slave);
        order.SymbolId.Should().Be(SymbolId);
        order.IsBuy.Should().BeTrue();
        order.Volume.Should().Be(100);
        order.Label.Should().Be("1001");
    }

    [Fact]
    public async Task Reverse_flips_side_and_swaps_stop_loss_and_take_profit()
    {
        var session = NewSession();
        var plan = Plan(new CopyDestinationPlan(Slave, "t",
            Destination(Slave, d => { d.SetReverse(true); d.SetCopyProtection(true, true); })));

        await DriveAsync(session, plan, async () =>
        {
            session.PushOpen(Source, 1002, SymbolId, isBuy: true, volume: 100, stopLoss: 1.09, takeProfit: 1.12);
            await WaitUntil(() => session.Orders.Count == 1 && session.Amends.Count == 1);
        });

        session.Orders.Single().IsBuy.Should().BeFalse("reverse copies the opposite side");
        var amend = session.Amends.Single();
        amend.StopLoss.Should().Be(1.12, "on reverse the source take-profit becomes the stop-loss");
        amend.TakeProfit.Should().Be(1.09, "on reverse the source stop-loss becomes the take-profit");
    }

    [Fact]
    public async Task Symbol_map_resolves_destination_symbol()
    {
        var session = NewSession(new Dictionary<string, long> { ["EURUSDX"] = 2 });
        var plan = Plan(new CopyDestinationPlan(Slave, "t",
            Destination(Slave, d => d.SetSymbolMap([new SymbolMapEntry(new Symbol("EURUSD"), new Symbol("EURUSD.x"))]))));

        await DriveAsync(session, plan, async () =>
        {
            session.PushOpen(Source, 1003, SymbolId, isBuy: true, volume: 100);
            await WaitUntil(() => session.Orders.Count == 1);
        });

        session.Orders.Single().SymbolId.Should().Be(2, "the mapped destination symbol id is used");
    }

    [Fact]
    public async Task Order_failure_on_one_slave_still_copies_to_others()
    {
        var session = NewSession();
        session.FailOrdersForCtid.Add(Slave); // first slave rejects
        var plan = Plan(
            new CopyDestinationPlan(Slave, "t", Destination(Slave)),
            new CopyDestinationPlan(Slave2, "t", Destination(Slave2)));

        await DriveAsync(session, plan, async () =>
        {
            session.PushOpen(Source, 1004, SymbolId, isBuy: true, volume: 100);
            await WaitUntil(() => session.Orders.Any(o => o.Ctid == Slave2));
        });

        session.Orders.Should().ContainSingle(o => o.Ctid == Slave2, "a failing slave must not block the others");
        session.Orders.Should().NotContain(o => o.Ctid == Slave);
    }

    [Fact]
    public async Task Source_close_closes_the_mirrored_copy()
    {
        var session = NewSession();
        var plan = Plan(new CopyDestinationPlan(Slave, "t", Destination(Slave)));

        await DriveAsync(session, plan, async () =>
        {
            session.PushOpen(Source, 1005, SymbolId, isBuy: true, volume: 100);
            await WaitUntil(() => session.Orders.Count == 1);
            session.PushClose(Source, 1005, SymbolId, isBuy: true, volume: 100);
            await WaitUntil(() => session.Closes.Count == 1);
        });

        session.Closes.Single().Ctid.Should().Be(Slave);
    }

    [Fact]
    public async Task Audit_log_records_every_trading_operation()
    {
        var session = NewSession();
        var log = new CapturingLogger();
        var plan = Plan(new CopyDestinationPlan(Slave, "t", Destination(Slave)));

        await DriveAsync(session, plan, async () =>
        {
            session.PushOpen(Source, 2001, SymbolId, isBuy: true, volume: 100, stopLoss: 1.09, takeProfit: 1.12);
            await WaitUntil(() => session.Orders.Count == 1 && session.Amends.Count == 1);
            session.PushClose(Source, 2001, SymbolId, isBuy: true, volume: 100);
            await WaitUntil(() => session.Closes.Count == 1);
        }, log);

        log.Records.Select(r => r.EventId).Should().Contain(new[]
        {
            1046, // host started
            1047, // source open
            1048, // order placed
            1050, // protection applied
            1052, // source close
            1053, // position closed
        });
    }

    [Fact]
    public async Task Reconnect_resync_closes_orphaned_destination_positions()
    {
        var session = NewSession();
        // A leftover copy on the slave whose source position is no longer open (closed while offline).
        session.SeedPosition(Slave, positionId: 7777, SymbolId, isBuy: true, volume: 100, label: "9999");
        var plan = Plan(new CopyDestinationPlan(Slave, "t", Destination(Slave)));

        await DriveAsync(session, plan, () =>
            WaitUntil(() => session.Closes.Any(c => c.Ctid == Slave && c.PositionId == 7777)));

        session.Closes.Should().Contain(c => c.PositionId == 7777, "orphaned copies are reconciled away on resync");
    }
}
