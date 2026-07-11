using System.Collections.Concurrent;
using Core;
using Core.CopyTrading;
using Core.Domain;
using CopyEngine;
using CTraderOpenApi.Client;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace UnitTests.CopyTrading;

// Phase 3 execution transparency: the copy host emits a per-copy execution fact to its ICopyEventSink on
// every open (success or failure), carrying the data the transparency report needs — symbol, side, wire
// volume, master price, realized slippage and latency. The default no-op sink means this is inert unless
// transparency is enabled, so these tests inject a capturing sink to observe the facts.
public sealed class CopyTransparencyTests
{
    private const long Source = 100;
    private const long Slave = 200;
    private const long SymbolId = 1;
    private static readonly SymbolDetails Details = new(SymbolId, LotSize: 100, StepVolume: 1, MinVolume: 1, PipPosition: 5);

    private sealed class CapturingSink : ICopyEventSink
    {
        public ConcurrentQueue<CopyExecutionRecord> Records { get; } = new();
        public void Record(CopyExecutionRecord record) => Records.Enqueue(record);
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
        condition().Should().BeTrue("the expected copy-execution fact was not emitted in time");
    }

    private static async Task DriveAsync(FakeTradingSession session, CapturingSink sink, Func<Task> act)
    {
        var plan = new CopyProfilePlan(CopyProfileId.New(), Live: false, "client", "secret", Source, "token", 1,
            [new CopyDestinationPlan(Slave, "t", 1, Destination())]);
        var host = new CopyEngineHost(plan, new FakeTradingSessionFactory(session),
            new CopyDecisionEngine(new CopySizingCalculator()), TimeProvider.System, NullLogger.Instance, sink);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var run = Task.Run(() => host.RunAsync(cts.Token), CancellationToken.None);
        try { await act(); }
        finally { cts.Cancel(); try { await run; } catch { /* cancellation */ } }
    }

    [Fact]
    public async Task A_successful_open_emits_an_Opened_execution_fact()
    {
        var session = NewSession();
        var sink = new CapturingSink();

        await DriveAsync(session, sink, async () =>
        {
            session.PushOpen(Source, positionId: 7001, SymbolId, isBuy: true, volume: 100);
            await WaitUntil(() => sink.Records.Any(r => r.Kind == CopyExecutionKind.Opened && r.SourcePositionId == 7001));
        });

        var record = sink.Records.Single(r => r.SourcePositionId == 7001);
        record.Kind.Should().Be(CopyExecutionKind.Opened);
        record.DestinationCtidTraderAccountId.Should().Be(Slave);
        record.Symbol.Should().Be("EURUSD");
        record.IsBuy.Should().BeTrue();
        record.Volume.Should().BeGreaterThan(0);
        record.LatencyMilliseconds.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task A_rejected_open_emits_a_Failed_execution_fact_with_the_reason()
    {
        var session = NewSession();
        session.FailOrdersForCtid.Add(Slave); // the destination rejects every order
        var sink = new CapturingSink();

        await DriveAsync(session, sink, async () =>
        {
            session.PushOpen(Source, positionId: 7002, SymbolId, isBuy: false, volume: 100);
            await WaitUntil(() => sink.Records.Any(r => r.Kind == CopyExecutionKind.Failed && r.SourcePositionId == 7002));
        });

        var record = sink.Records.Single(r => r.Kind == CopyExecutionKind.Failed && r.SourcePositionId == 7002);
        record.DestinationCtidTraderAccountId.Should().Be(Slave);
        record.Reason.Should().NotBeNullOrEmpty();
    }
}
