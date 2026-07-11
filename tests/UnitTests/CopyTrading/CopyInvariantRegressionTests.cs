using Core;
using Core.Domain;
using CopyEngine;
using CTraderOpenApi.Client;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace UnitTests.CopyTrading;

// Named regressions for the failure classes cMind's stateless, id-based, event-mirroring engine
// structurally prevents (overhaul plan §3.1 / §3.2):
//   C1/C17 — "ghost trades": equity-to-equity copiers rebalance/close open copies on a deposit or
//            withdrawal. cMind mirrors discrete master events by position id and sizes once at open, so
//            a balance move must open/close nothing. This is the headline differentiator — lock it.
//   C12/M5 — duplicate copy: a pending order that fills emits a dual (order + open) event; a naive
//            copier opens it twice (FX-Blue MT5 / cMAM "copied the limit order twice"). cMind dedupes
//            by order-id -> position-id and retires the resting pending, so a limit copies exactly once.
public sealed class CopyInvariantRegressionTests
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

    private static CopyProfilePlan Plan(params CopyDestinationPlan[] destinations)
        => new(CopyProfileId.New(), Live: false, "client", "secret", Source, "token", 1, destinations);

    private static async Task DriveAsync(FakeTradingSession session, CopyProfilePlan plan, Func<Task> act,
        ILogger? logger = null)
    {
        var host = new CopyEngineHost(plan, new FakeTradingSessionFactory(session),
            new CopyDecisionEngine(new CopySizingCalculator()), TimeProvider.System,
            logger ?? (ILogger)NullLogger.Instance);
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
    public async Task C1_balance_change_on_an_open_copy_opens_or_closes_nothing()
    {
        var session = NewSession();
        session.Balance = 10_000;
        var plan = Plan(new CopyDestinationPlan(Slave, "t", 1, Destination()));

        await DriveAsync(session, plan, async () =>
        {
            session.PushOpen(Source, positionId: 1001, SymbolId, isBuy: true, volume: 100);
            await WaitUntil(() => session.Orders.Count == 1);

            // A deposit then a withdrawal on the account — the equity-to-equity trigger that force-closes
            // or rebalances open copies on competitors. cMind must ignore it: no execution event, no copy.
            session.Balance = 25_000;
            await Task.Delay(150);
            session.Balance = 3_000;
            await Task.Delay(150);
        });

        session.Orders.Should().ContainSingle("a balance move never opens a new copy (no ghost trade)");
        session.Closes.Should().BeEmpty("a balance move never closes an open copy (no forced liquidation)");
    }

    [Fact]
    public async Task C12_limit_order_copies_exactly_once_on_place_then_fill()
    {
        var session = NewSession();
        var log = new CapturingLogger();
        var plan = Plan(new CopyDestinationPlan(Slave, "t", 1, Destination(d => d.SetPendingOrderCopying(true))));

        await DriveAsync(session, plan, async () =>
        {
            // Place the resting limit — mirrors as one pending on the slave.
            session.PushPending(Source, orderId: 5401, SymbolId, isBuy: true, volume: 100, CopyOrderKind.Limit, price: 1.05);
            await WaitUntil(() => session.Pendings.Count == 1);

            // The limit triggers: cTrader emits ORDER_FILLED carrying the originating order id + an OPEN
            // position (the FX-Blue/cMAM dual event). The resting pending is retired and the copy re-opens
            // exactly once as a labelled market position — never a second copy.
            session.PushOpen(Source, positionId: 5499, SymbolId, isBuy: true, volume: 100, orderId: 5401);
            await WaitUntil(() => session.Orders.Count == 1);
            await Task.Delay(150);
        }, log);

        session.Orders.Should().ContainSingle("a filled limit re-opens exactly once (no double-copy)");
        session.Orders.Single().Label.Should().Be("5499", "the copy is labelled by the filled position id");
        session.Cancels.Should().ContainSingle(c => c.Ctid == Slave, "the resting slave pending is retired on fill");
        log.Records.Select(r => r.EventId).Should().Contain(1048, "the single copy open is on the audit trail");
    }

    [Fact]
    public async Task C12_a_second_fill_event_for_the_same_order_does_not_copy_again()
    {
        var session = NewSession();
        var plan = Plan(new CopyDestinationPlan(Slave, "t", 1, Destination(d => d.SetPendingOrderCopying(true))));

        await DriveAsync(session, plan, async () =>
        {
            session.PushPending(Source, orderId: 5501, SymbolId, isBuy: true, volume: 100, CopyOrderKind.Limit, price: 1.05);
            await WaitUntil(() => session.Pendings.Count == 1);
            session.PushOpen(Source, positionId: 5599, SymbolId, isBuy: true, volume: 100, orderId: 5501);
            await WaitUntil(() => session.Orders.Count == 1);

            // A duplicate fill echo for the same already-consumed order id must be a no-op, not a copy.
            session.PushOpen(Source, positionId: 5599, SymbolId, isBuy: true, volume: 100, orderId: 5501);
            await Task.Delay(200);
        });

        session.Orders.Should().ContainSingle("a repeated fill for a consumed order id never double-copies");
    }
}
