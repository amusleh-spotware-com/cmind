using System.Diagnostics.Metrics;
using Core.Constants;

namespace CopyEngine;

/// <summary>
/// OpenTelemetry instruments for the copy engine (fixes G6 — previously only structured logs, no
/// quantitative signal). One process-wide <see cref="Meter"/> (name <see cref="ObservabilityDefaults.CopyMeterName"/>,
/// registered in the OTel metrics pipeline) so a latency or slippage regression is measurable, not just
/// visible in a log line. Instruments are cheap no-ops until a listener/exporter subscribes.
/// </summary>
public sealed class CopyMetrics
{
    // Process-wide instance: the Meter and its instruments are inherently global, and hosts are
    // constructed directly (not via DI), so an ambient singleton avoids threading metrics through every
    // CopyEngineHost ctor. A MeterListener subscribes by meter name regardless of who records.
    public static CopyMetrics Instance { get; } = new();

    private readonly Histogram<double> _copyLatencyMilliseconds;
    private readonly Histogram<double> _dispatchDurationMilliseconds;
    private readonly Histogram<int> _masterSlippagePoints;
    private readonly Counter<long> _copiesPlaced;
    private readonly Counter<long> _copiesSkipped;
    private readonly Counter<long> _copyFailures;

    public CopyMetrics()
    {
        var meter = new Meter(ObservabilityDefaults.CopyMeterName);
        _copyLatencyMilliseconds = meter.CreateHistogram<double>(
            "cmind.copy.latency", unit: "ms", description: "Master event -> copy dispatch latency.");
        _dispatchDurationMilliseconds = meter.CreateHistogram<double>(
            "cmind.copy.dispatch.duration", unit: "ms", description: "Time to fan a single master open out to all destinations.");
        _masterSlippagePoints = meter.CreateHistogram<int>(
            "cmind.copy.slippage.points", unit: "point", description: "Master market-range slippage mirrored onto a copy.");
        _copiesPlaced = meter.CreateCounter<long>(
            "cmind.copy.placed", description: "Copies placed on a destination.");
        _copiesSkipped = meter.CreateCounter<long>(
            "cmind.copy.skipped", description: "Copies skipped, tagged by reason.");
        _copyFailures = meter.CreateCounter<long>(
            "cmind.copy.failed", description: "Copy attempts that threw.");
    }

    public void RecordLatency(double milliseconds) => _copyLatencyMilliseconds.Record(milliseconds);

    public void RecordDispatchDuration(double milliseconds) => _dispatchDurationMilliseconds.Record(milliseconds);

    public void RecordSlippage(int points) => _masterSlippagePoints.Record(points);

    public void CopyPlaced(long destinationCtid)
        => _copiesPlaced.Add(1, new KeyValuePair<string, object?>("destination", destinationCtid));

    public void CopySkipped(string reason)
        => _copiesSkipped.Add(1, new KeyValuePair<string, object?>("reason", reason));

    public void CopyFailed(long destinationCtid)
        => _copyFailures.Add(1, new KeyValuePair<string, object?>("destination", destinationCtid));
}
