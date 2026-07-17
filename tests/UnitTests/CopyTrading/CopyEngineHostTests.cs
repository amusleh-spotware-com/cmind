using Core;
using Core.Constants;
using Core.Domain;
using CopyEngine;
using CTraderOpenApi.Client;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
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
        Func<Task> act, CapturingLogger? logger = null, TimeProvider? timeProvider = null)
    {
        var host = new CopyEngineHost(plan, new FakeTradingSessionFactory(session),
            new CopyDecisionEngine(new CopySizingCalculator()), timeProvider ?? TimeProvider.System,
            logger ?? (ILogger)NullLogger.Instance);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var run = Task.Run(() => host.RunAsync(cts.Token), CancellationToken.None);
        try { await act(); }
        finally { cts.Cancel(); try { await run; } catch { /* cancellation */ } }
    }

    private static async Task WaitUntil(Func<bool> condition, TimeSpan? timeout = null)
    {
        // Generous by default: these host actions run on a real-clock background task, so a tight limit
        // flakes under CI load (the wait must simply be shorter than the host's own ~10s lifetime).
        var limit = timeout ?? TimeSpan.FromSeconds(8);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        while (stopwatch.Elapsed < limit)
        {
            if (condition()) return;
            await Task.Delay(20);
        }
        condition().Should().BeTrue("the expected host action did not occur in time");
    }

    [Fact]
    public async Task Host_emits_live_activity_lines_for_the_owner_log_viewer()
    {
        var session = NewSession();
        var plan = Plan(new CopyDestinationPlan(Slave, "t", 1, Destination(Slave)));
        var log = new CapturingCopyLogSink();

        var host = new CopyEngineHost(plan, new FakeTradingSessionFactory(session),
            new CopyDecisionEngine(new CopySizingCalculator()), TimeProvider.System, NullLogger.Instance,
            logSink: log);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var run = Task.Run(() => host.RunAsync(cts.Token), CancellationToken.None);
        try
        {
            await WaitUntil(() => log.Lines.Any(l => l.Contains("Copy engine started")));
            session.PushOpen(Source, positionId: 2001, SymbolId, isBuy: true, volume: 100);
            await WaitUntil(() => log.Lines.Any(l => l.Contains("Source opened"))
                                  && log.Lines.Any(l => l.Contains(Slave.ToString()) && l.Contains("placed")));
        }
        finally
        {
            cts.Cancel();
            try { await run; } catch { /* cancellation */ }
        }

        log.Lines.Should().Contain(l => l.Contains("Copy engine started"));
        log.Lines.Should().Contain(l => l.Contains("Source opened EURUSD"));
        log.Lines.Should().Contain(l => l.Contains("placed") && l.Contains("EURUSD"));
    }

    private sealed class CapturingCopyLogSink : Core.CopyTrading.ICopyLogSink
    {
        private readonly List<string> _lines = [];
        private readonly object _gate = new();
        public string[] Lines { get { lock (_gate) return [.. _lines]; } }
        public void Append(CopyProfileId profileId, string line) { lock (_gate) _lines.Add(line); }
        public void Complete(CopyProfileId profileId) { }
    }

    [Fact]
    public async Task Open_is_skipped_when_the_symbol_is_in_a_news_blackout()
    {
        var session = NewSession();
        var plan = Plan(new CopyDestinationPlan(Slave, "t", 1, Destination(Slave)));

        var host = new CopyEngineHost(plan, new FakeTradingSessionFactory(session),
            new CopyDecisionEngine(new CopySizingCalculator()), TimeProvider.System, NullLogger.Instance,
            newsBlackout: (symbol, _) => ValueTask.FromResult(symbol == "EURUSD"));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var run = Task.Run(() => host.RunAsync(cts.Token), CancellationToken.None);
        try
        {
            session.PushOpen(Source, positionId: 1001, SymbolId, isBuy: true, volume: 100);
            await Task.Delay(500);
            session.Orders.Should().BeEmpty("the source symbol is inside a news blackout");
        }
        finally { cts.Cancel(); try { await run; } catch { /* cancellation */ } }
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
    public async Task Stale_source_open_past_max_delay_is_skipped()
    {
        var now = new DateTimeOffset(2026, 07, 11, 12, 00, 00, TimeSpan.Zero);
        var clock = new FakeTimeProvider(now);
        var session = NewSession();
        var plan = Plan(new CopyDestinationPlan(Slave, "t", 1,
            Destination(Slave, d => d.ConfigureMaxDelay(MaxCopyDelay.Seconds(30)))));

        await DriveAsync(session, plan, async () =>
        {
            var staleTimestamp = now.AddSeconds(-60).ToUnixTimeMilliseconds();
            session.PushOpen(Source, 6001, SymbolId, isBuy: true, volume: 100, serverTimestamp: staleTimestamp);
            await Task.Delay(200);
        }, timeProvider: clock);

        session.Orders.Should().BeEmpty("a signal older than the 30s max-lag is dropped (real latency, fixes G1)");
    }

    [Fact]
    public async Task Fresh_source_open_within_max_delay_is_copied()
    {
        var now = new DateTimeOffset(2026, 07, 11, 12, 00, 00, TimeSpan.Zero);
        var clock = new FakeTimeProvider(now);
        var session = NewSession();
        var plan = Plan(new CopyDestinationPlan(Slave, "t", 1,
            Destination(Slave, d => d.ConfigureMaxDelay(MaxCopyDelay.Seconds(30)))));

        await DriveAsync(session, plan, async () =>
        {
            var freshTimestamp = now.AddSeconds(-2).ToUnixTimeMilliseconds();
            session.PushOpen(Source, 6002, SymbolId, isBuy: true, volume: 100, serverTimestamp: freshTimestamp);
            await WaitUntil(() => session.Orders.Count == 1);
        }, timeProvider: clock);

        session.Orders.Should().ContainSingle("a signal within the max-lag copies normally");
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
    public async Task Open_outside_the_trading_hours_window_is_skipped()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 07, 11, 05, 00, 00, TimeSpan.Zero)); // 05:00 UTC
        var session = NewSession();
        var plan = Plan(new CopyDestinationPlan(Slave, "t", 1,
            Destination(Slave, d => d.ConfigureTradingHours(new TradingWindow(540, 1020))))); // 09:00–17:00

        await DriveAsync(session, plan, async () =>
        {
            session.PushOpen(Source, 1001, SymbolId, isBuy: true, volume: 100);
            await Task.Delay(150);
        }, timeProvider: clock);

        session.Orders.Should().BeEmpty("a copy outside the destination's trading-hours window is skipped");
    }

    [Fact]
    public async Task Open_inside_the_trading_hours_window_is_copied()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 07, 11, 10, 00, 00, TimeSpan.Zero)); // 10:00 UTC
        var session = NewSession();
        var plan = Plan(new CopyDestinationPlan(Slave, "t", 1,
            Destination(Slave, d => d.ConfigureTradingHours(new TradingWindow(540, 1020))))); // 09:00–17:00

        await DriveAsync(session, plan, async () =>
        {
            session.PushOpen(Source, 1002, SymbolId, isBuy: true, volume: 100);
            await WaitUntil(() => session.Orders.Count == 1);
        }, timeProvider: clock);

        session.Orders.Should().ContainSingle("a copy inside the window is placed normally");
    }

    [Fact]
    public async Task Execution_jitter_delays_but_still_places_the_copy()
    {
        var session = NewSession();
        var plan = Plan(new CopyDestinationPlan(Slave, "t", 1, Destination(Slave, d => d.SetExecutionJitter(10))));

        await DriveAsync(session, plan, async () =>
        {
            session.PushOpen(Source, 1001, SymbolId, isBuy: true, volume: 100);
            await WaitUntil(() => session.Orders.Count == 1);
        });

        session.Orders.Should().ContainSingle("execution jitter delays the copy but never drops it");
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
    public async Task Source_label_filter_copies_only_matching_master_trades()
    {
        var session = NewSession();
        var plan = Plan(new CopyDestinationPlan(Slave, "t", 1, Destination(Slave, d => d.SetSourceLabelFilter("botA"))));

        await DriveAsync(session, plan, async () =>
        {
            session.PushOpen(Source, 1001, SymbolId, isBuy: true, volume: 100, sourceLabel: "botB"); // filtered out
            session.PushOpen(Source, 1002, SymbolId, isBuy: true, volume: 100, sourceLabel: "botA"); // copied
            await WaitUntil(() => session.Orders.Any(o => o.Label == "1002"));
            await Task.Delay(100);
        });

        session.Orders.Should().ContainSingle(o => o.Label == "1002");
        session.Orders.Should().NotContain(o => o.Label == "1001", "a master trade whose label doesn't match is not copied");
    }

    [Fact]
    public async Task Per_symbol_volume_multiplier_scales_the_copy_size()
    {
        var session = NewSession();
        var plan = Plan(new CopyDestinationPlan(Slave, "t", 1, Destination(Slave, d =>
            d.SetSymbolMap([new SymbolMapEntry(new Symbol("EURUSD"), new Symbol("EURUSD"), volumeMultiplier: 2)]))));

        await DriveAsync(session, plan, async () =>
        {
            session.PushOpen(Source, 1001, SymbolId, isBuy: true, volume: 100);
            await WaitUntil(() => session.Orders.Count == 1);
        });

        session.Orders.Single().Volume.Should().Be(200, "the per-symbol 2x override doubles the copied volume");
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
    public async Task Account_protection_sell_out_closes_all_and_blocks_new_opens_on_equity_breach()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 07, 11, 12, 00, 00, TimeSpan.Zero));
        var session = NewSession();
        session.Balance = 3000; // equity below the 5000 stop
        session.SeedPosition(Source, positionId: 5001, SymbolId, isBuy: true, volume: 100, label: "5001");
        session.SeedPosition(Slave, positionId: 9001, SymbolId, isBuy: true, volume: 100, label: "5001");
        var plan = Plan(new CopyDestinationPlan(Slave, "t", 1, Destination(Slave, d =>
            d.SetAccountProtection(new AccountProtectionPolicy(AccountProtectionMode.SellOut, 5000, null)))));

        await DriveAsync(session, plan, async () =>
        {
            await Task.Delay(150); // let the host start its equity-guard timer before advancing the clock
            clock.Advance(CopyDefaults.EquityGuardInterval + TimeSpan.FromSeconds(1)); // fire a guard tick
            await WaitUntil(() => session.Closes.Any(c => c.PositionId == 9001));

            // A protected destination opens nothing further.
            session.PushOpen(Source, positionId: 6001, SymbolId, isBuy: true, volume: 100);
            await Task.Delay(150);
        }, timeProvider: clock);

        session.Closes.Should().Contain(c => c.PositionId == 9001, "sell-out closes every copy on an equity breach");
        session.Orders.Should().BeEmpty("a protected destination opens no new positions");
    }

    [Fact]
    public async Task Account_protection_close_only_blocks_new_opens_without_closing_existing()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 07, 11, 12, 00, 00, TimeSpan.Zero));
        var session = NewSession();
        session.Balance = 3000; // equity below the 5000 stop
        session.SeedPosition(Source, positionId: 5001, SymbolId, isBuy: true, volume: 100, label: "5001");
        session.SeedPosition(Slave, positionId: 9001, SymbolId, isBuy: true, volume: 100, label: "5001");
        var plan = Plan(new CopyDestinationPlan(Slave, "t", 1, Destination(Slave, d =>
            d.SetAccountProtection(new AccountProtectionPolicy(AccountProtectionMode.CloseOnly, 5000, null)))));

        CapturingLogger log = new();
        await DriveAsync(session, plan, async () =>
        {
            await Task.Delay(150);
            clock.Advance(CopyDefaults.EquityGuardInterval + TimeSpan.FromSeconds(1));
            await WaitUntil(() => log.Records.Any(r => r.EventId == 1081)); // protection triggered
            session.PushOpen(Source, positionId: 6001, SymbolId, isBuy: true, volume: 100);
            await Task.Delay(150);
        }, log, clock);

        session.Orders.Should().BeEmpty("close-only blocks new opens once triggered");
        session.Closes.Should().BeEmpty("close-only does not liquidate existing copies (unlike sell-out)");
    }

    [Fact]
    public async Task Prop_rule_daily_loss_breach_flattens_and_locks_out_the_destination()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 07, 11, 12, 00, 00, TimeSpan.Zero));
        var session = NewSession();
        session.Balance = 10000;
        session.SeedPosition(Source, positionId: 5001, SymbolId, isBuy: true, volume: 100, label: "5001");
        session.SeedPosition(Slave, positionId: 9001, SymbolId, isBuy: true, volume: 100, label: "5001");
        var log = new CapturingLogger();
        var plan = Plan(new CopyDestinationPlan(Slave, "t", 1, Destination(Slave, d =>
            d.SetPropRuleGuard(new PropRuleGuard(dailyLossCap: 1500, trailingDrawdown: 0)))));

        await DriveAsync(session, plan, async () =>
        {
            await Task.Delay(150);
            clock.Advance(CopyDefaults.EquityGuardInterval + TimeSpan.FromSeconds(1)); // tick 1: baseline 10000
            await Task.Delay(120);
            session.Balance = 8000; // equity falls 2000, past the 1500 daily-loss cap
            clock.Advance(CopyDefaults.EquityGuardInterval + TimeSpan.FromSeconds(1)); // tick 2: breach
            await WaitUntil(() => log.Records.Any(r => r.EventId == 1082));

            session.PushOpen(Source, positionId: 6001, SymbolId, isBuy: true, volume: 100);
            await Task.Delay(120);
        }, log, clock);

        session.Closes.Should().Contain(c => c.PositionId == 9001, "a daily-loss breach auto-flattens the destination");
        session.Orders.Should().BeEmpty("a locked-out destination opens nothing further");
        log.Records.Should().Contain(r => r.EventId == 1082, "the prop-rule breach alert fires");
    }

    [Fact]
    public async Task Consistency_threshold_alerts_when_daily_profit_approaches_the_limit()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 07, 11, 12, 00, 00, TimeSpan.Zero));
        var session = NewSession();
        session.Balance = 10000;
        var log = new CapturingLogger();
        var plan = Plan(new CopyDestinationPlan(Slave, "t", 1, Destination(Slave, d => d.SetConsistencyThreshold(5))));

        await DriveAsync(session, plan, async () =>
        {
            await Task.Delay(150);
            clock.Advance(CopyDefaults.EquityGuardInterval + TimeSpan.FromSeconds(1)); // tick 1: baseline 10000
            await Task.Delay(120);
            session.Balance = 10600; // +6% daily profit, past the 5% consistency threshold
            clock.Advance(CopyDefaults.EquityGuardInterval + TimeSpan.FromSeconds(1)); // tick 2: alert
            await WaitUntil(() => log.Records.Any(r => r.EventId == 1083));
        }, log, clock);

        log.Records.Should().Contain(r => r.EventId == 1083, "the consistency pre-alert fires as daily profit approaches the limit");
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
    public async Task Flatten_all_closes_every_copy_and_blocks_new_opens()
    {
        var session = NewSession();
        session.SeedPosition(Source, positionId: 5001, SymbolId, isBuy: true, volume: 100, label: "5001");
        session.SeedPosition(Slave, positionId: 9001, SymbolId, isBuy: true, volume: 100, label: "5001");
        var plan = Plan(new CopyDestinationPlan(Slave, "t", 1, Destination(Slave)));
        var host = new CopyEngineHost(plan, new FakeTradingSessionFactory(session),
            new CopyDecisionEngine(new CopySizingCalculator()), TimeProvider.System, (ILogger)NullLogger.Instance);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var run = Task.Run(() => host.RunAsync(cts.Token), CancellationToken.None);
        try
        {
            await Task.Delay(200); // let the initial resync settle
            host.PushFlatten();
            await WaitUntil(() => session.Closes.Any(c => c.PositionId == 9001));

            session.PushOpen(Source, positionId: 6001, SymbolId, isBuy: true, volume: 100);
            await Task.Delay(150);
        }
        finally { cts.Cancel(); try { await run; } catch { /* cancellation */ } }

        session.Closes.Should().Contain(c => c.PositionId == 9001, "flatten-all closes every copied position");
        session.Orders.Should().BeEmpty("after a panic flatten the destinations are locked against new opens");
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
    public async Task Sync_open_off_does_not_open_pre_existing_master_positions_on_start()
    {
        var session = NewSession();
        session.SeedPosition(Source, positionId: 8001, SymbolId, isBuy: true, volume: 100, label: string.Empty);
        var plan = Plan(new CopyDestinationPlan(Slave, "t", 1,
            Destination(Slave, d => d.SetSyncPolicy(syncOpenOnStart: false, syncClosedOnStart: true))));

        await DriveAsync(session, plan, () => Task.Delay(200));

        session.Orders.Should().BeEmpty("sync-open-off leaves the master's pre-existing trades uncopied at start");
    }

    [Fact]
    public async Task Sync_closed_off_leaves_orphaned_copies_untouched_on_start()
    {
        var session = NewSession();
        // A copy whose source the master no longer holds (closed while the profile was stopped).
        session.SeedPosition(Slave, positionId: 7777, SymbolId, isBuy: true, volume: 100, label: "9999");
        var plan = Plan(new CopyDestinationPlan(Slave, "t", 1,
            Destination(Slave, d => d.SetSyncPolicy(syncOpenOnStart: true, syncClosedOnStart: false))));

        await DriveAsync(session, plan, () => Task.Delay(200));

        session.Closes.Should().BeEmpty("sync-closed-off leaves what the master closed while stopped");
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
    public async Task Resync_tolerates_a_position_not_found_and_still_closes_other_orphans()
    {
        var session = NewSession();
        // Two orphaned copies the master no longer holds; the broker reports the first already closed.
        session.SeedPosition(Slave, positionId: 7001, SymbolId, isBuy: true, volume: 100, label: "9001");
        session.SeedPosition(Slave, positionId: 7002, SymbolId, isBuy: true, volume: 100, label: "9002");
        session.RejectReasonForCtid[Slave] = CtraderRejectReason.PositionNotFound; // one-shot on the next close
        var plan = Plan(new CopyDestinationPlan(Slave, "t", 1, Destination(Slave)));

        await DriveAsync(session, plan, () =>
            WaitUntil(() => session.Closes.Any(c => c.Ctid == Slave)));

        session.Closes.Should().ContainSingle(c => c.Ctid == Slave,
            "a POSITION_NOT_FOUND on one orphan must not abort the resync — the other orphan still closes");
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
    public async Task Manage_only_opens_nothing_but_still_closes_existing_copies()
    {
        var session = NewSession();
        // Master holds 5001, already mirrored on the manage-only slave as 9001 — must stay managed.
        session.SeedPosition(Source, positionId: 5001, SymbolId, isBuy: true, volume: 100, label: "5001");
        session.SeedPosition(Slave, positionId: 9001, SymbolId, isBuy: true, volume: 100, label: "5001");
        var plan = Plan(new CopyDestinationPlan(Slave, "t", 1,
            Destination(Slave, d => { d.SetManageOnly(true); d.SetPendingOrderCopying(true); })));

        await DriveAsync(session, plan, async () =>
        {
            // A fresh master open opens nothing on a manage-only destination.
            session.PushOpen(Source, positionId: 6001, SymbolId, isBuy: true, volume: 100);
            // A fresh master pending also places nothing.
            session.PushPending(Source, orderId: 6500, SymbolId, isBuy: true, volume: 100, CopyOrderKind.Limit, 1.05);
            await Task.Delay(150);
            session.Orders.Should().BeEmpty("manage-only opens no new positions");
            session.Pendings.Should().BeEmpty("manage-only places no new pendings");

            // The master closing the already-copied position still closes the copy.
            session.PushClose(Source, 5001, SymbolId, isBuy: true, volume: 100);
            await WaitUntil(() => session.Closes.Any(c => c.PositionId == 9001));
        });

        session.Orders.Should().BeEmpty();
        session.Closes.Should().Contain(c => c.PositionId == 9001, "existing copies are still managed and closed");
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
    public async Task Stop_loss_price_is_normalized_to_destination_symbol_digits()
    {
        // The destination symbol quotes 3 digits; a master SL at finer precision must be rounded before the
        // amend or the real server rejects it with INVALID_STOPLOSS_TAKEPROFIT (the cMAM M6 bug).
        var session = new FakeTradingSession(
            new Dictionary<long, string> { [SymbolId] = "EURUSD" },
            new Dictionary<string, long> { ["EURUSD"] = SymbolId },
            new SymbolDetails(SymbolId, LotSize: 100, StepVolume: 1, MinVolume: 1, PipPosition: 5, Digits: 3));
        var plan = Plan(new CopyDestinationPlan(Slave, "t", 1, Destination(Slave, d => d.SetCopyProtection(true, false))));

        await DriveAsync(session, plan, async () =>
        {
            session.PushOpen(Source, 4801, SymbolId, isBuy: true, volume: 100, stopLoss: 1.23456);
            await WaitUntil(() => session.Amends.Count == 1);
        });

        session.Amends.Single().StopLoss.Should().Be(1.235, "the stop-loss is normalized to the destination's 3 digits");
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
    public async Task Take_profit_set_on_an_existing_position_is_mirrored_even_when_the_stop_loss_is_unchanged()
    {
        var session = NewSession();
        var plan = Plan(new CopyDestinationPlan(Slave, "t", 1, Destination(Slave, d => d.SetCopyProtection(true, true))));

        await DriveAsync(session, plan, async () =>
        {
            session.PushOpen(Source, 5501, SymbolId, isBuy: true, volume: 100, stopLoss: 1.09);
            await WaitUntil(() => session.Amends.Count == 1); // stop-loss applied on open
            // Add a take-profit; the stop-loss is unchanged. This is the exact regression: a TP-only change
            // was neither detected (only SL was tracked) nor amended (take-profit was hardcoded null).
            session.PushOpen(Source, 5501, SymbolId, isBuy: true, volume: 100, stopLoss: 1.09, takeProfit: 1.20);
            await WaitUntil(() => session.Amends.Count == 2);
        });

        var last = session.Amends.Last();
        last.TakeProfit.Should().Be(1.20, "a take-profit set on an existing position must mirror to the destination");
        last.StopLoss.Should().Be(1.09, "the unchanged stop-loss is preserved on the amend");
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

    [Fact]
    public async Task One_master_open_copies_to_every_slave()
    {
        var session = NewSession();
        var plan = Plan(
            new CopyDestinationPlan(Slave, "t", 1, Destination(Slave)),
            new CopyDestinationPlan(Slave2, "t", 1, Destination(Slave2)));

        await DriveAsync(session, plan, async () =>
        {
            session.PushOpen(Source, 7201, SymbolId, isBuy: true, volume: 100);
            await WaitUntil(() => session.Orders.Count == 2);
        });

        session.Orders.Select(o => o.Ctid).Should().BeEquivalentTo([Slave, Slave2],
            "a 1:many profile mirrors the master open onto every slave");
        session.Orders.Should().OnlyContain(o => o.Volume == 100 && o.Label == "7201");
    }

    // Different risk-management (money-management) sizing modes each size the copy as configured, through
    // the real engine + fake session. Master and destination share the fake's 10000 balance/equity/free
    // margin, so proportional ratios are 1; a master open of 1 lot (100 wire units at LotSize 100) sizes as
    // the mode dictates. The live counterpart (Risk_sizing_mode_places_a_live_copy) clamps to min lot for
    // safety; here the exact sized volume is asserted.
    public static IEnumerable<object[]> SizingModes() =>
    [
        [MoneyManagementMode.FixedLot, 2.0, 200L],             // param is the destination lot size directly
        [MoneyManagementMode.LotMultiplier, 2.0, 200L],        // 1 master lot x 2
        [MoneyManagementMode.NotionalMultiplier, 1.0, 100L],   // equal contract size -> 1:1
        [MoneyManagementMode.ProportionalBalance, 1.0, 100L],  // equal balances -> ratio 1
        [MoneyManagementMode.ProportionalEquity, 1.0, 100L],
        [MoneyManagementMode.ProportionalFreeMargin, 1.0, 100L],
        [MoneyManagementMode.AutoProportional, 1.0, 100L],
        [MoneyManagementMode.FixedRiskPercent, 0.01, 100L],    // 0.01% of 10000 / contract size 1 = 1 lot
        [MoneyManagementMode.FixedLeverage, 0.0001, 100L],     // 0.0001x of 10000 / contract size 1 = 1 lot
    ];

    [Theory]
    [MemberData(nameof(SizingModes))]
    public async Task Risk_sizing_mode_sizes_the_copy(MoneyManagementMode mode, double parameter, long expectedVolume)
    {
        var session = NewSession();
        var plan = Plan(new CopyDestinationPlan(Slave, "t", 1,
            Destination(Slave, d => d.ConfigureRisk(new RiskSettings(mode, parameter)))));

        await DriveAsync(session, plan, async () =>
        {
            session.PushOpen(Source, 7101, SymbolId, isBuy: true, volume: 100);
            await WaitUntil(() => session.Orders.Count == 1);
        });

        session.Orders.Single().Volume.Should().Be(expectedVolume,
            $"the {mode} sizing mode must size the copy as configured");
    }
}
