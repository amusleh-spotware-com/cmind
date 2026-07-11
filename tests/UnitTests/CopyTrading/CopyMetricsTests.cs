using System.Diagnostics.Metrics;
using Core;
using Core.Constants;
using Core.Domain;
using CopyEngine;
using CTraderOpenApi.Client;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace UnitTests.CopyTrading;

// G6: the copy engine emits OpenTelemetry metrics, not just logs. Assert a real copy records the copy
// latency histogram and the placed counter on the cMind.Copy meter, via a MeterListener (the same
// mechanism an exporter uses). Measurements are asserted as present (not exact counts) because the meter
// is process-wide and test classes run in parallel.
public sealed class CopyMetricsTests
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

    private static CopyDestination Destination()
    {
        var profile = CopyProfile.Create(UserId.New(), "p", TradingAccountId.New());
        return profile.AddDestination(TradingAccountId.New(), RiskSettings.Default);
    }

    [Fact]
    public async Task A_copy_records_latency_and_placed_metrics()
    {
        var capturedInstruments = new System.Collections.Concurrent.ConcurrentBag<string>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == ObservabilityDefaults.CopyMeterName)
                    meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<double>((instrument, _, _, _) => capturedInstruments.Add(instrument.Name));
        listener.SetMeasurementEventCallback<long>((instrument, _, _, _) => capturedInstruments.Add(instrument.Name));
        listener.SetMeasurementEventCallback<int>((instrument, _, _, _) => capturedInstruments.Add(instrument.Name));
        listener.Start();

        var session = NewSession();
        var plan = new CopyProfilePlan(CopyProfileId.New(), Live: false, "client", "secret", Source, "token", 1,
            [new CopyDestinationPlan(Slave, "t", 1, Destination())]);
        var host = new CopyEngineHost(plan, new FakeTradingSessionFactory(session),
            new CopyDecisionEngine(new CopySizingCalculator()), TimeProvider.System, (ILogger)NullLogger.Instance);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var run = Task.Run(() => host.RunAsync(cts.Token), CancellationToken.None);
        try
        {
            session.PushOpen(Source, positionId: 1001, SymbolId, isBuy: true, volume: 100);
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            while (stopwatch.Elapsed < TimeSpan.FromSeconds(3) && session.Orders.Count == 0)
                await Task.Delay(20);
        }
        finally { cts.Cancel(); try { await run; } catch { /* cancellation */ } }

        listener.RecordObservableInstruments();
        capturedInstruments.Should().Contain("cmind.copy.placed", "a placed copy increments the counter");
        capturedInstruments.Should().Contain("cmind.copy.latency", "a placed copy records its dispatch latency");
    }
}
