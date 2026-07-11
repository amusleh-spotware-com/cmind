using System.Collections.Concurrent;
using Core;
using Core.Constants;
using Core.CopyTrading;
using Core.Domain;
using CopyEngine;
using CTraderOpenApi.Client;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace UnitTests.CopyTrading;

// 2b notification routing: the copy host raises an operational notification to its ICopyNotificationSink on
// safety-relevant events (a destination tripping the rejection breaker, a panic flatten). The default no-op
// sink keeps this inert in the engine's other tests; here a capturing sink observes the notifications.
public sealed class CopyNotificationTests
{
    private const long Source = 100;
    private const long Slave = 200;
    private const long SymbolId = 1;
    private static readonly SymbolDetails Details = new(SymbolId, LotSize: 100, StepVolume: 1, MinVolume: 1, PipPosition: 5);

    private sealed class CapturingNotifications : ICopyNotificationSink
    {
        public ConcurrentQueue<CopyNotificationRecord> Records { get; } = new();
        public void Notify(CopyNotificationRecord record) => Records.Enqueue(record);
    }

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
        condition().Should().BeTrue("the expected copy notification was not raised in time");
    }

    private static (CopyEngineHost Host, CancellationTokenSource Cts, Task Run) Start(
        FakeTradingSession session, ICopyNotificationSink notifications)
    {
        var plan = new CopyProfilePlan(CopyProfileId.New(), Live: false, "client", "secret", Source, "token", 1,
            [new CopyDestinationPlan(Slave, "t", 1, Destination())]);
        var host = new CopyEngineHost(plan, new FakeTradingSessionFactory(session),
            new CopyDecisionEngine(new CopySizingCalculator()), TimeProvider.System, NullLogger.Instance,
            notifications: notifications);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var run = Task.Run(() => host.RunAsync(cts.Token), CancellationToken.None);
        return (host, cts, run);
    }

    [Fact]
    public async Task A_tripped_destination_raises_a_DestinationTripped_notification()
    {
        var session = NewSession();
        session.FailOrdersForCtid.Add(Slave); // every open rejects
        var notifications = new CapturingNotifications();
        var (_, cts, run) = Start(session, notifications);
        try
        {
            for (var i = 0; i < CopyDefaults.RejectionBudget; i++)
                session.PushOpen(Source, positionId: 6001 + i, SymbolId, isBuy: true, volume: 100);
            await WaitUntil(() => notifications.Records.Any(r => r.Kind == CopyNotificationKind.DestinationTripped));
        }
        finally { cts.Cancel(); try { await run; } catch { /* cancellation */ } cts.Dispose(); }

        var record = notifications.Records.First(r => r.Kind == CopyNotificationKind.DestinationTripped);
        record.DestinationCtidTraderAccountId.Should().Be(Slave);
        record.Severity.Should().Be(CopyNotificationSeverity.Warning);
        record.Message.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task A_panic_flatten_raises_a_FlattenAll_notification()
    {
        var session = NewSession();
        var notifications = new CapturingNotifications();
        var (host, cts, run) = Start(session, notifications);
        try
        {
            host.PushFlatten();
            await WaitUntil(() => notifications.Records.Any(r => r.Kind == CopyNotificationKind.FlattenAll));
        }
        finally { cts.Cancel(); try { await run; } catch { /* cancellation */ } cts.Dispose(); }

        var record = notifications.Records.First(r => r.Kind == CopyNotificationKind.FlattenAll);
        record.DestinationCtidTraderAccountId.Should().BeNull("flatten-all is a profile-level notification");
        record.Severity.Should().Be(CopyNotificationSeverity.Critical);
    }
}
