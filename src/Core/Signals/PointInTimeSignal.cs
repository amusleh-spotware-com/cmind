using Core.Constants;
using Core.Domain;

namespace Core.Signals;

/// <summary>A sentiment score in [-1, 1] (−1 fully bearish, +1 fully bullish). Self-validating.</summary>
public readonly record struct SentimentScore
{
    public double Value { get; }

    public SentimentScore(double value)
    {
        if (double.IsNaN(value) || value < -1.0 || value > 1.0)
            throw new DomainException(DomainErrors.SentimentScoreInvalid);
        Value = value;
    }

    public bool IsBullish => Value > 0.15;
    public bool IsBearish => Value < -0.15;
}

/// <summary>
/// A signal stamped with the moment it was <em>knowable</em> — the guard against look-ahead bias. Any
/// backtest or agent consuming a signal must only see values whose <see cref="AsOf"/> is at or before
/// the decision time; this type makes that stamp mandatory and non-default.
/// </summary>
public sealed record PointInTimeSignal
{
    public PointInTimeSignal(DateTimeOffset asOf, string kind, double value, string provenance)
    {
        if (asOf == default) throw new DomainException(DomainErrors.PointInTimeSignalInvalid);
        Kind = DomainGuard.AgainstNullOrWhiteSpace(kind, DomainErrors.PointInTimeSignalInvalid);
        Provenance = DomainGuard.AgainstNullOrWhiteSpace(provenance, DomainErrors.PointInTimeSignalInvalid);
        AsOf = asOf;
        Value = value;
    }

    public DateTimeOffset AsOf { get; }
    public string Kind { get; }
    public double Value { get; }
    public string Provenance { get; }

    /// <summary>True when this signal was knowable at (or before) the given decision time — no future leak.</summary>
    public bool IsKnownAt(DateTimeOffset decisionTime) => AsOf <= decisionTime;
}
