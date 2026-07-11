using Core;
using Core.Domain;
using CopyEngine;
using CTraderOpenApi.Client;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace UnitTests.CopyTrading;

// Advanced copy scenarios against the cTrader-faithful FakeTradingSession: per-order-type filtering,
// pending-order expiry copying, market-range / stop-limit slippage mirroring, pending amends, and the
// desync/resync + in-place token-swap paths that keep live copying safe when connections drop or a
// cID's single valid access token rotates.
public sealed class CopyAdvancedScenariosTests
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

    private static CopyEngineHost Host(FakeTradingSession session, CopyProfilePlan plan, ILogger? logger = null)
        => new(plan, new FakeTradingSessionFactory(session),
            new CopyDecisionEngine(new CopySizingCalculator()), TimeProvider.System,
            logger ?? (ILogger)NullLogger.Instance);

    private static async Task DriveAsync(FakeTradingSession session, CopyProfilePlan plan,
        Func<Task> act, ILogger? logger = null)
    {
        var host = Host(session, plan, logger);
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
    public async Task Order_type_filter_skips_a_market_open_when_market_is_excluded()
    {
        var session = NewSession();
        var logger = new CapturingLogger();
        var plan = Plan(new CopyDestinationPlan(Slave, "t", 1,
            Destination(d => d.SetOrderTypeFilter(CopyOrderTypes.Limit | CopyOrderTypes.Stop))));

        await DriveAsync(session, plan, async () =>
        {
            session.PushOpen(Source, positionId: 1001, SymbolId, isBuy: true, volume: 100);
            await WaitUntil(() => logger.Records.Any(r => r.Message.Contains("order_type")));
        }, logger);

        session.Orders.Should().BeEmpty();
    }

    [Fact]
    public async Task Order_type_filter_allows_a_market_open_by_default()
    {
        var session = NewSession();
        var plan = Plan(new CopyDestinationPlan(Slave, "t", 1, Destination()));

        await DriveAsync(session, plan, async () =>
        {
            session.PushOpen(Source, positionId: 1001, SymbolId, isBuy: true, volume: 100);
            await WaitUntil(() => session.Orders.Count == 1);
        });

        session.Orders.Single().SlippageInPoints.Should().BeNull();
    }

    [Fact]
    public async Task Market_range_open_mirrors_master_slippage_and_base_price()
    {
        var session = NewSession();
        session.SetSpot(SymbolId, bid: 1.0995, ask: 1.1005);
        var plan = Plan(new CopyDestinationPlan(Slave, "t", 1, Destination()));

        await DriveAsync(session, plan, async () =>
        {
            session.PushOpen(Source, positionId: 1001, SymbolId, isBuy: true, volume: 100,
                orderKind: CopyOrderKind.MarketRange, slippageInPoints: 30);
            await WaitUntil(() => session.Orders.Count == 1);
        });

        var order = session.Orders.Single();
        order.SlippageInPoints.Should().Be(30);
        order.BaseSlippagePrice.Should().Be(1.1005);
    }

    [Fact]
    public async Task Market_range_slippage_is_dropped_when_master_slippage_copying_is_off()
    {
        var session = NewSession();
        var plan = Plan(new CopyDestinationPlan(Slave, "t", 1, Destination(d => d.SetSlippageCopying(false))));

        await DriveAsync(session, plan, async () =>
        {
            session.PushOpen(Source, positionId: 1001, SymbolId, isBuy: true, volume: 100,
                orderKind: CopyOrderKind.MarketRange, slippageInPoints: 30);
            await WaitUntil(() => session.Orders.Count == 1);
        });

        session.Orders.Single().SlippageInPoints.Should().BeNull();
    }

    [Fact]
    public async Task Pending_expiry_is_mirrored_by_default()
    {
        var session = NewSession();
        var plan = Plan(new CopyDestinationPlan(Slave, "t", 1, Destination(d => d.SetPendingOrderCopying(true))));

        await DriveAsync(session, plan, async () =>
        {
            session.PushPending(Source, orderId: 7001, SymbolId, isBuy: true, volume: 100,
                CopyOrderKind.Limit, price: 1.09, expirationTimestamp: 1_700_000_000_000);
            await WaitUntil(() => session.Pendings.Count == 1);
        });

        session.Pendings.Single().ExpirationTimestamp.Should().Be(1_700_000_000_000);
    }

    [Fact]
    public async Task Pending_expiry_is_dropped_when_expiry_copying_is_off()
    {
        var session = NewSession();
        var plan = Plan(new CopyDestinationPlan(Slave, "t", 1,
            Destination(d => { d.SetPendingOrderCopying(true); d.SetExpiryCopying(false); })));

        await DriveAsync(session, plan, async () =>
        {
            session.PushPending(Source, orderId: 7001, SymbolId, isBuy: true, volume: 100,
                CopyOrderKind.Limit, price: 1.09, expirationTimestamp: 1_700_000_000_000);
            await WaitUntil(() => session.Pendings.Count == 1);
        });

        session.Pendings.Single().ExpirationTimestamp.Should().BeNull();
    }

    [Fact]
    public async Task Stop_limit_pending_mirrors_master_slippage()
    {
        var session = NewSession();
        var plan = Plan(new CopyDestinationPlan(Slave, "t", 1, Destination(d => d.SetPendingOrderCopying(true))));

        await DriveAsync(session, plan, async () =>
        {
            session.PushPending(Source, orderId: 7002, SymbolId, isBuy: false, volume: 100,
                CopyOrderKind.StopLimit, price: 1.11, slippageInPoints: 20);
            await WaitUntil(() => session.Pendings.Count == 1);
        });

        var pending = session.Pendings.Single();
        pending.Kind.Should().Be(CopyOrderKind.StopLimit);
        pending.SlippageInPoints.Should().Be(20);
    }

    [Fact]
    public async Task Pending_order_type_filter_skips_an_excluded_stop()
    {
        var session = NewSession();
        var logger = new CapturingLogger();
        var plan = Plan(new CopyDestinationPlan(Slave, "t", 1,
            Destination(d => { d.SetPendingOrderCopying(true); d.SetOrderTypeFilter(CopyOrderTypes.Limit); })));

        await DriveAsync(session, plan, async () =>
        {
            session.PushPending(Source, orderId: 7003, SymbolId, isBuy: true, volume: 100, CopyOrderKind.Stop, price: 1.12);
            await WaitUntil(() => logger.Records.Any(r => r.Message.Contains("order_type")));
        }, logger);

        session.Pendings.Should().BeEmpty();
    }

    [Fact]
    public async Task Pending_amend_is_mirrored_onto_the_slave_order()
    {
        var session = NewSession();
        var plan = Plan(new CopyDestinationPlan(Slave, "t", 1, Destination(d => d.SetPendingOrderCopying(true))));

        await DriveAsync(session, plan, async () =>
        {
            session.PushPending(Source, orderId: 7004, SymbolId, isBuy: true, volume: 100, CopyOrderKind.Limit, price: 1.09);
            await WaitUntil(() => session.Pendings.Count == 1);
            session.PushPendingReplaced(Source, orderId: 7004, SymbolId, isBuy: true, volume: 100, CopyOrderKind.Limit, price: 1.085);
            await WaitUntil(() => session.AmendedPendings.Count == 1);
        });

        session.AmendedPendings.Single().Price.Should().Be(1.085);
    }

    [Fact]
    public async Task Resync_opens_a_copy_for_a_master_position_open_before_the_profile_started()
    {
        var session = NewSession();
        session.SeedPosition(Source, positionId: 8001, SymbolId, isBuy: true, volume: 100, label: string.Empty);
        var plan = Plan(new CopyDestinationPlan(Slave, "t", 1, Destination()));

        await DriveAsync(session, plan, () =>
            WaitUntil(() => session.Orders.Any(o => o.Label == "8001")));

        session.Orders.Should().ContainSingle(o => o.Label == "8001" && o.Ctid == Slave);
    }

    [Fact]
    public async Task Reconnect_resync_opens_missing_copies_and_closes_orphans_after_a_desync()
    {
        var session = NewSession();
        // A slave copy labelled with a source id the master no longer holds — must be closed on resync.
        session.SeedPosition(Slave, positionId: 9999, SymbolId, isBuy: true, volume: 50, label: "5555");
        var plan = Plan(new CopyDestinationPlan(Slave, "t", 1, Destination()));

        await DriveAsync(session, plan, async () =>
        {
            // Master trades while the socket is down; on reconnect the host must converge to master state.
            session.Disconnect();
            session.SeedPosition(Source, positionId: 8002, SymbolId, isBuy: true, volume: 100, label: string.Empty);
            await session.ReconnectAsync();
            await WaitUntil(() => session.Orders.Any(o => o.Label == "8002") && session.Closes.Any(c => c.PositionId == 9999));
        });

        session.Orders.Should().Contain(o => o.Label == "8002");
        session.Closes.Should().Contain(c => c.PositionId == 9999);
    }

    [Fact]
    public async Task Token_rotation_swaps_the_access_token_in_place_without_restarting_the_host()
    {
        var session = NewSession();
        var plan = Plan(new CopyDestinationPlan(Slave, "old", 1, Destination()));
        var host = Host(session, plan);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var run = Task.Run(() => host.RunAsync(cts.Token), CancellationToken.None);
        try
        {
            host.PushTokenUpdate(new[] { (Slave, "new-token") });
            await WaitUntil(() => session.Swaps.Any(s => s.Ctid == Slave && s.Token == "new-token"));

            // Host still processes events after the swap — the stream was never dropped.
            session.PushOpen(Source, positionId: 1234, SymbolId, isBuy: true, volume: 100);
            await WaitUntil(() => session.Orders.Any(o => o.Label == "1234"));
        }
        finally { cts.Cancel(); try { await run; } catch { /* cancellation */ } }

        session.CurrentToken(Slave).Should().Be("new-token");
    }

    [Fact]
    public async Task Cross_cid_invalidation_swaps_source_and_destination_tokens()
    {
        var session = NewSession();
        var plan = Plan(new CopyDestinationPlan(Slave, "old-dest", 1, Destination()));
        var host = Host(session, plan);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var run = Task.Run(() => host.RunAsync(cts.Token), CancellationToken.None);
        try
        {
            host.PushTokenUpdate(new[] { (Source, "new-source"), (Slave, "new-dest") });
            await WaitUntil(() =>
                session.Swaps.Any(s => s.Ctid == Source && s.Token == "new-source") &&
                session.Swaps.Any(s => s.Ctid == Slave && s.Token == "new-dest"));
        }
        finally { cts.Cancel(); try { await run; } catch { /* cancellation */ } }

        session.CurrentToken(Source).Should().Be("new-source");
        session.CurrentToken(Slave).Should().Be("new-dest");
    }
}
