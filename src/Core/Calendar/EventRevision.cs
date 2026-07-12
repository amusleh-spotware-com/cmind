using System.ComponentModel.DataAnnotations;

namespace Core.Calendar;

/// <summary>
/// One append-only fact about an <see cref="EconomicEvent"/>: what we knew, and when we knew it. Actuals,
/// forecasts, impact scores and schedule moves are never overwritten — each change is a new revision, giving
/// a full audit trail and point-in-time correctness. Owned by the event aggregate; ordered by
/// <see cref="Sequence"/> and monotonic in <see cref="KnownAt"/>.
/// </summary>
public sealed class EventRevision
{
    public int Sequence { get; private set; }

    /// <summary>When <em>we</em> learned this fact (point-in-time anchor). Monotonic across the chain.</summary>
    public DateTimeOffset KnownAt { get; private set; }

    public RevisionKind Kind { get; private set; }

    public decimal? Actual { get; private set; }
    public decimal? Forecast { get; private set; }
    public decimal? Previous { get; private set; }

    public double ImpactScore { get; private set; }
    public ImpactLevel ImpactLevel { get; private set; }
    public int ImpactModelVersion { get; private set; }

    [MaxLength(32)] public string? Unit { get; private set; }
    [MaxLength(256)] public string? SourceRef { get; private set; }

    /// <summary>Set only on a <see cref="RevisionKind.Rescheduled"/> revision — the new release instant.</summary>
    public DateTimeOffset? RescheduledInstant { get; private set; }

    private EventRevision()
    {
    }

    internal static EventRevision Create(
        int sequence,
        DateTimeOffset knownAt,
        RevisionKind kind,
        ImpactAssessment impact,
        decimal? actual = null,
        decimal? forecast = null,
        decimal? previous = null,
        string? unit = null,
        string? sourceRef = null,
        DateTimeOffset? rescheduledInstant = null)
        => new()
        {
            Sequence = sequence,
            KnownAt = knownAt,
            Kind = kind,
            Actual = actual,
            Forecast = forecast,
            Previous = previous,
            ImpactScore = impact.Score.Value,
            ImpactLevel = impact.Level,
            ImpactModelVersion = impact.Version,
            Unit = unit,
            SourceRef = sourceRef,
            RescheduledInstant = rescheduledInstant
        };
}
