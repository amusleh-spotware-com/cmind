using Core;
using Core.Constants;
using Core.Domain;
using CopyEngine;
using CTraderOpenApi.Client;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace UnitTests.CopyTrading;

// G8 rejection circuit breaker (Follower Guard): a destination that rejects repeatedly must stop
// receiving new opens (a rejection storm on a prop-firm account is a rule-breach risk), then auto-resume
// after a cooldown. Driven by a FakeTimeProvider so the cooldown boundary is exact.
public sealed class CopyCircuitBreakerTests
{
    private const long Source = 100;
    private const long Slave = 200;
    private const long SymbolId = 1;
    private const int CopyDestinationTrippedEventId = 1080;
    private static readonly SymbolDetails Details = new(SymbolId, LotSize: 100, StepVolume: 1, MinVolume: 1, PipPosition: 5);

    private static FakeTradingSession NewSession()
        => new(
            new Dictionary<long, string> { [SymbolId] = "EURUSD" },
            new Dictionary<string, long> { ["EURUSD"] = SymbolId },
            Details);

    private static CopyDestination Destination()
    {
        var profile = CopyProfile.Create(UserId.New(), "p", TradingAccountId.New());
        return profile.AddDestination(TradingAccountId.New(), RiskSettings.Default);
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
    public async Task Destination_trips_after_the_rejection_budget_then_resumes_after_cooldown()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 07, 11, 12, 00, 00, TimeSpan.Zero));
        var session = NewSession();
        session.FailOrdersForCtid.Add(Slave); // every open rejects
        var log = new CapturingLogger();
        var plan = new CopyProfilePlan(CopyProfileId.New(), Live: false, "client", "secret", Source, "token", 1,
            [new CopyDestinationPlan(Slave, "t", 1, Destination())]);

        var host = new CopyEngineHost(plan, new FakeTradingSessionFactory(session),
            new CopyDecisionEngine(new CopySizingCalculator()), clock, log);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var run = Task.Run(() => host.RunAsync(cts.Token), CancellationToken.None);
        try
        {
            // Exhaust the rejection budget -> the destination trips.
            for (var i = 0; i < CopyDefaults.RejectionBudget; i++)
                session.PushOpen(Source, positionId: 6001 + i, SymbolId, isBuy: true, volume: 100);
            await WaitUntil(() => log.Records.Any(r => r.EventId == CopyDestinationTrippedEventId));

            // Heal the destination but stay inside the cooldown: a new open is skipped, not attempted.
            session.FailOrdersForCtid.Remove(Slave);
            session.PushOpen(Source, positionId: 6100, SymbolId, isBuy: true, volume: 100);
            await Task.Delay(150);
            session.Orders.Should().BeEmpty("while tripped the destination receives no new opens");

            // Cooldown elapses -> the breaker resets and copying resumes.
            clock.Advance(CopyDefaults.CircuitCooldown + TimeSpan.FromSeconds(1));
            session.PushOpen(Source, positionId: 6200, SymbolId, isBuy: true, volume: 100);
            await WaitUntil(() => session.Orders.Any(o => o.Label == "6200"));
        }
        finally { cts.Cancel(); try { await run; } catch { /* cancellation */ } }

        log.Records.Should().Contain(r => r.EventId == CopyDestinationTrippedEventId,
            "the Follower Guard alert fires when the rejection budget is exhausted");
        session.Orders.Should().ContainSingle(o => o.Label == "6200", "copying resumes after the cooldown");
    }

    // Regression (root cause of the DST chaos convergence failure): the circuit breaker gates only LIVE
    // opens (per-event storm suppression). A resync is the deliberate source-of-truth reconciliation and
    // must reconverge a tripped destination's book *inside* the cooldown — otherwise a destination that
    // tripped during a rejection storm can never catch up until the cooldown elapses, so a reconnect-driven
    // resync leaves it permanently short of the master.
    [Fact]
    public async Task Resync_reopens_a_tripped_destinations_missing_positions_inside_the_cooldown()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 07, 11, 12, 00, 00, TimeSpan.Zero));
        var session = NewSession();
        session.FailOrdersForCtid.Add(Slave); // every live open rejects
        var log = new CapturingLogger();
        var plan = new CopyProfilePlan(CopyProfileId.New(), Live: false, "client", "secret", Source, "token", 1,
            [new CopyDestinationPlan(Slave, "t", 1, Destination())]);

        var host = new CopyEngineHost(plan, new FakeTradingSessionFactory(session),
            new CopyDecisionEngine(new CopySizingCalculator()), clock, log);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var run = Task.Run(() => host.RunAsync(cts.Token), CancellationToken.None);
        try
        {
            // Exhaust the rejection budget with live opens -> the destination trips (every copy rejected).
            for (var i = 0; i < CopyDefaults.RejectionBudget; i++)
                session.PushOpen(Source, positionId: 6001 + i, SymbolId, isBuy: true, volume: 100);
            await WaitUntil(() => log.Records.Any(r => r.EventId == CopyDestinationTrippedEventId));
            session.Orders.Should().BeEmpty("every live copy rejected while the destination was failing");

            // Heal the destination and force a reconnect resync — WITHOUT advancing the clock (still inside
            // the 60s cooldown). A master position the slave is missing must be (re)opened by the resync,
            // proving the breaker gates only live opens, not the source-of-truth reconciliation.
            session.FailOrdersForCtid.Remove(Slave);
            session.SeedPosition(Source, positionId: 8001, SymbolId, isBuy: true, volume: 100, label: string.Empty);
            session.Disconnect();
            await session.ReconnectAsync(cts.Token);

            await WaitUntil(() => session.Orders.Any(o => o.Label == "8001"));
        }
        finally { cts.Cancel(); try { await run; } catch { /* cancellation */ } }

        clock.GetUtcNow().Should().Be(new DateTimeOffset(2026, 07, 11, 12, 00, 00, TimeSpan.Zero),
            "the reopen happened inside the cooldown — the clock was never advanced");
        session.Orders.Should().ContainSingle(o => o.Label == "8001" && o.Ctid == Slave,
            "a resync reconverges a tripped destination even inside the circuit-breaker cooldown");
    }
}
