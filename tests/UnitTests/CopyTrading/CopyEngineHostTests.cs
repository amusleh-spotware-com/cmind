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
        => new(CopyProfileId.New(), Live: false, "client", "secret", Source, "token", 1, destinations);

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
        var plan = Plan(new CopyDestinationPlan(Slave, "t", 1, Destination(Slave)));

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
        var plan = Plan(new CopyDestinationPlan(Slave, "t", 1,
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
        var plan = Plan(new CopyDestinationPlan(Slave, "t", 1,
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
            new CopyDestinationPlan(Slave, "t", 1, Destination(Slave)),
            new CopyDestinationPlan(Slave2, "t", 1, Destination(Slave2)));

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
        var plan = Plan(new CopyDestinationPlan(Slave, "t", 1, Destination(Slave)));

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
        var plan = Plan(new CopyDestinationPlan(Slave, "t", 1, Destination(Slave)));

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
        var plan = Plan(new CopyDestinationPlan(Slave, "t", 1, Destination(Slave)));

        await DriveAsync(session, plan, () =>
            WaitUntil(() => session.Closes.Any(c => c.Ctid == Slave && c.PositionId == 7777)));

        session.Closes.Should().Contain(c => c.PositionId == 7777, "orphaned copies are reconciled away on resync");
    }

    [Fact]
    public async Task Partial_close_mirrors_a_proportional_slice_on_the_slave()
    {
        var session = NewSession();
        var plan = Plan(new CopyDestinationPlan(Slave, "t", 1, Destination(Slave)));

        await DriveAsync(session, plan, async () =>
        {
            session.PushOpen(Source, 3001, SymbolId, isBuy: true, volume: 100);
            await WaitUntil(() => session.Orders.Count == 1);
            session.PushOpen(Source, 3001, SymbolId, isBuy: true, volume: 40); // 60% closed on the master
            await WaitUntil(() => session.Closes.Count == 1);
        });

        session.Closes.Single().Volume.Should().Be(60, "the copy shrinks proportionally to the master");
    }

    [Fact]
    public async Task Partial_close_is_ignored_when_mirroring_disabled()
    {
        var session = NewSession();
        var plan = Plan(new CopyDestinationPlan(Slave, "t", 1,
            Destination(Slave, d => d.SetPartialCloseMirroring(false, false))));

        await DriveAsync(session, plan, async () =>
        {
            session.PushOpen(Source, 3101, SymbolId, isBuy: true, volume: 100);
            await WaitUntil(() => session.Orders.Count == 1);
            session.PushOpen(Source, 3101, SymbolId, isBuy: true, volume: 40);
            await Task.Delay(150);
        });

        session.Closes.Should().BeEmpty("partial-close mirroring is off");
    }

    [Fact]
    public async Task Scale_in_is_ignored_by_default_and_mirrored_when_enabled()
    {
        var offSession = NewSession();
        var offPlan = Plan(new CopyDestinationPlan(Slave, "t", 1, Destination(Slave)));
        await DriveAsync(offSession, offPlan, async () =>
        {
            offSession.PushOpen(Source, 3201, SymbolId, isBuy: true, volume: 100);
            await WaitUntil(() => offSession.Orders.Count == 1);
            offSession.PushOpen(Source, 3201, SymbolId, isBuy: true, volume: 160);
            await Task.Delay(150);
        });
        offSession.Orders.Should().HaveCount(1, "scale-ins are ignored by default");

        var onSession = NewSession();
        var onPlan = Plan(new CopyDestinationPlan(Slave, "t", 1, Destination(Slave, d => d.SetPartialCloseMirroring(true, true))));
        await DriveAsync(onSession, onPlan, async () =>
        {
            onSession.PushOpen(Source, 3202, SymbolId, isBuy: true, volume: 100);
            await WaitUntil(() => onSession.Orders.Count == 1);
            onSession.PushOpen(Source, 3202, SymbolId, isBuy: true, volume: 160);
            await WaitUntil(() => onSession.Orders.Count == 2);
        });
        onSession.Orders.Should().HaveCount(2);
        onSession.Orders[1].Volume.Should().Be(60, "the scale-in mirrors the added volume");
    }

    [Theory]
    [InlineData(CopyOrderKind.Limit)]
    [InlineData(CopyOrderKind.Stop)]
    public async Task Pending_order_is_placed_on_the_slave_when_enabled(CopyOrderKind kind)
    {
        var session = NewSession();
        var plan = Plan(new CopyDestinationPlan(Slave, "t", 1, Destination(Slave, d => d.SetPendingOrderCopying(true))));

        await DriveAsync(session, plan, async () =>
        {
            session.PushPending(Source, orderId: 4001, SymbolId, isBuy: true, volume: 100, kind, price: 1.05);
            await WaitUntil(() => session.Pendings.Count == 1);
        });

        var pending = session.Pendings.Single();
        pending.Ctid.Should().Be(Slave);
        pending.Kind.Should().Be(kind);
        pending.Price.Should().Be(1.05);
        pending.Volume.Should().Be(100);
        pending.Label.Should().Be("4001");
    }

    [Fact]
    public async Task Pending_order_is_not_placed_when_disabled()
    {
        var session = NewSession();
        var plan = Plan(new CopyDestinationPlan(Slave, "t", 1, Destination(Slave)));

        await DriveAsync(session, plan, async () =>
        {
            session.PushPending(Source, 4101, SymbolId, isBuy: true, volume: 100, CopyOrderKind.Limit, 1.05);
            await Task.Delay(150);
        });

        session.Pendings.Should().BeEmpty("pending-order copying is off by default");
    }

    [Fact]
    public async Task Source_pending_cancel_cancels_the_slave_pending()
    {
        var session = NewSession();
        var plan = Plan(new CopyDestinationPlan(Slave, "t", 1, Destination(Slave, d => d.SetPendingOrderCopying(true))));

        await DriveAsync(session, plan, async () =>
        {
            session.PushPending(Source, 4201, SymbolId, isBuy: true, volume: 100, CopyOrderKind.Limit, 1.05);
            await WaitUntil(() => session.Pendings.Count == 1);
            session.PushPendingCancel(Source, 4201, SymbolId, isBuy: true, volume: 100, CopyOrderKind.Limit);
            await WaitUntil(() => session.Cancels.Count == 1);
        });

        session.Cancels.Single().Ctid.Should().Be(Slave);
    }

    [Fact]
    public async Task Filled_pending_does_not_double_open()
    {
        var session = NewSession();
        var plan = Plan(new CopyDestinationPlan(Slave, "t", 1, Destination(Slave, d => d.SetPendingOrderCopying(true))));

        await DriveAsync(session, plan, async () =>
        {
            session.PushPending(Source, 4301, SymbolId, isBuy: true, volume: 100, CopyOrderKind.Limit, 1.05);
            await WaitUntil(() => session.Pendings.Count == 1);
            // The source pending fills into a position carrying the originating order id.
            session.PushOpen(Source, positionId: 4399, SymbolId, isBuy: true, volume: 100, orderId: 4301);
            await WaitUntil(() => session.Orders.Count == 1);
        });

        session.Orders.Should().HaveCount(1, "a filled pending re-opens exactly once as a labelled market position");
        session.Cancels.Should().ContainSingle(c => c.Ctid == Slave, "the resting pending is retired on fill");
        session.Orders.Single().Label.Should().Be("4399");
    }

    [Fact]
    public async Task Trailing_stop_is_applied_to_the_copy_when_enabled()
    {
        var session = NewSession();
        var plan = Plan(new CopyDestinationPlan(Slave, "t", 1,
            Destination(Slave, d => { d.SetCopyProtection(true, false); d.SetTrailingStopCopying(true); })));

        await DriveAsync(session, plan, async () =>
        {
            session.PushOpen(Source, 4401, SymbolId, isBuy: true, volume: 100, stopLoss: 1.09, trailing: true);
            await WaitUntil(() => session.Amends.Count == 1);
        });

        session.Amends.Single().Trailing.Should().BeTrue("the copy trails when the source position trails");
    }

    [Fact]
    public async Task Source_stop_loss_move_re_amends_the_copy()
    {
        var session = NewSession();
        var plan = Plan(new CopyDestinationPlan(Slave, "t", 1, Destination(Slave, d => d.SetCopyProtection(true, false))));

        await DriveAsync(session, plan, async () =>
        {
            session.PushOpen(Source, 4501, SymbolId, isBuy: true, volume: 100, stopLoss: 1.09);
            await WaitUntil(() => session.Amends.Count == 1);
            session.PushOpen(Source, 4501, SymbolId, isBuy: true, volume: 100, stopLoss: 1.08); // SL moved
            await WaitUntil(() => session.Amends.Count == 2);
        });

        session.Amends.Last().StopLoss.Should().Be(1.08, "a source stop-loss move re-amends the copy");
    }

    [Fact]
    public async Task Advanced_mirroring_audit_events_fire()
    {
        var session = NewSession();
        var log = new CapturingLogger();
        var plan = Plan(new CopyDestinationPlan(Slave, "t", 1, Destination(Slave, d => d.SetPendingOrderCopying(true))));

        await DriveAsync(session, plan, async () =>
        {
            session.PushPending(Source, 4601, SymbolId, isBuy: true, volume: 100, CopyOrderKind.Limit, 1.05);
            await WaitUntil(() => session.Pendings.Count == 1);
            session.PushPendingCancel(Source, 4601, SymbolId, isBuy: true, volume: 100, CopyOrderKind.Limit);
            await WaitUntil(() => session.Cancels.Count == 1);
            session.PushOpen(Source, 4602, SymbolId, isBuy: true, volume: 100);
            await WaitUntil(() => session.Orders.Count == 1);
            session.PushOpen(Source, 4602, SymbolId, isBuy: true, volume: 40);
            await WaitUntil(() => session.Closes.Count == 1);
        }, log);

        log.Records.Select(r => r.EventId).Should().Contain(new[]
        {
            1058, // pending order placed
            1059, // pending order cancelled
            1056, // partial close
        });
    }
}
