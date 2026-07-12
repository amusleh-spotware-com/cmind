using System.ComponentModel.DataAnnotations;
using Core.Constants;
using Core.Domain;

namespace Core.Calendar;

/// <summary>
/// A concrete scheduled release instance of an <see cref="EconomicSeries"/> — the "CPI print on 2024-02-13".
/// Owns an append-only, monotonic-in-<c>KnownAt</c> chain of <see cref="EventRevision"/>s (scheduled →
/// released → revised → rescheduled → rescored), so the calendar is point-in-time correct and every change is
/// auditable rather than a silent overwrite. References its series by strong id, never a navigation property.
/// </summary>
public sealed class EconomicEvent : AuditedEntity<CalendarEventId>
{
    private readonly List<EventRevision> _revisions = [];

    public EconomicSeriesId SeriesId { get; private set; }

    [MaxLength(64)] public string SeriesCodeValue { get; private set; } = default!;

    [MaxLength(2)] public string CountryValue { get; private set; } = default!;

    /// <summary>The single UTC anchor for this release; the source's own timezone is kept alongside.</summary>
    public DateTimeOffset EffectiveAt { get; private set; }

    public ReleasePrecision Precision { get; private set; }

    [MaxLength(64)] public string SourceTimeZone { get; private set; } = default!;

    public bool Released { get; private set; }

    public IReadOnlyList<EventRevision> Revisions => _revisions;

    public SeriesCode SeriesCode => new(SeriesCodeValue);
    public CountryCode Country => new(CountryValue);

    private EconomicEvent()
    {
    }

    public static EconomicEvent Schedule(
        EconomicSeriesId seriesId,
        SeriesCode seriesCode,
        CountryCode country,
        ReleaseWindow window,
        string sourceTimeZone,
        ImpactAssessment priorImpact,
        DateTimeOffset now)
    {
        var economicEvent = new EconomicEvent
        {
            SeriesId = seriesId,
            SeriesCodeValue = seriesCode.Value,
            CountryValue = country.Value,
            EffectiveAt = window.Instant,
            Precision = window.Precision,
            SourceTimeZone = DomainGuard.AgainstNullOrWhiteSpace(sourceTimeZone, DomainErrors.CalendarSeriesCodeRequired)
        };

        economicEvent.Append(RevisionKind.Scheduled, now, priorImpact);
        return economicEvent;
    }

    /// <summary>Records the first printed actual. <paramref name="earlyRelease"/> allows a print before the anchor.</summary>
    public void Release(
        decimal actual,
        decimal? forecast,
        decimal? previous,
        ImpactAssessment impact,
        string? unit,
        string? sourceRef,
        DateTimeOffset knownAt,
        bool earlyRelease = false)
    {
        if (Released) throw new DomainException(DomainErrors.CalendarEventTransitionInvalid);
        if (!earlyRelease && knownAt < EffectiveAt) throw new DomainException(DomainErrors.CalendarActualBeforeRelease);

        Released = true;
        Append(RevisionKind.Released, knownAt, impact, actual, forecast, previous, unit, sourceRef);
        RaiseDomainEvent(new EconomicEventReleased(Id, SeriesId, actual, EffectiveAt));
    }

    /// <summary>Records a later revised value; the print must already have been released.</summary>
    public void Revise(
        decimal actual,
        decimal? forecast,
        decimal? previous,
        ImpactAssessment impact,
        string? unit,
        string? sourceRef,
        DateTimeOffset knownAt)
    {
        if (!Released) throw new DomainException(DomainErrors.CalendarEventTransitionInvalid);

        Append(RevisionKind.Revised, knownAt, impact, actual, forecast, previous, unit, sourceRef);
        RaiseDomainEvent(new EconomicEventRevised(Id, SeriesId, actual, knownAt));
    }

    /// <summary>The source moved the release instant — recorded as an auditable revision, never a silent edit.</summary>
    public void AdjustSchedule(DateTimeOffset newInstant, ImpactAssessment impact, string? sourceRef, DateTimeOffset knownAt)
    {
        var moved = newInstant.ToUniversalTime();
        if (moved == EffectiveAt) return;

        var previousInstant = EffectiveAt;
        EffectiveAt = moved;
        Append(RevisionKind.Rescheduled, knownAt, impact, sourceRef: sourceRef, rescheduledInstant: moved);
        RaiseDomainEvent(new EconomicEventRescheduled(Id, SeriesId, previousInstant, moved));
    }

    /// <summary>Recomputes the impact score under a new model version — a new revision, not a mutate.</summary>
    public void RescoreImpact(ImpactAssessment impact, DateTimeOffset knownAt)
    {
        var latest = LatestRevision;
        Append(RevisionKind.Rescored, knownAt, impact,
            latest?.Actual, latest?.Forecast, latest?.Previous, latest?.Unit, latest?.SourceRef);
    }

    public EventRevision? LatestRevision => _revisions.Count == 0 ? null : _revisions[^1];

    /// <summary>The most recent revision known at <paramref name="asOf"/> — the point-in-time view (no look-ahead).</summary>
    public EventRevision? RevisionAsOf(DateTimeOffset asOf)
    {
        EventRevision? found = null;
        foreach (var revision in _revisions)
        {
            if (revision.KnownAt > asOf) break;
            found = revision;
        }

        return found;
    }

    /// <summary>
    /// The impact level known at <paramref name="asOf"/>. Before any revision was known it defaults to
    /// <see cref="ImpactLevel.Low"/> — never the first revision's level, which would be a look-ahead leak.
    /// </summary>
    public ImpactLevel ImpactLevelAsOf(DateTimeOffset asOf) => RevisionAsOf(asOf)?.ImpactLevel ?? ImpactLevel.Low;

    /// <summary>A pure snapshot for the news-window policy, at the given point in time.</summary>
    public CalendarEventSnapshot SnapshotAsOf(DateTimeOffset asOf) =>
        new(Id, SeriesCode, Country, EffectiveAt, ImpactLevelAsOf(asOf));

    private void Append(
        RevisionKind kind,
        DateTimeOffset knownAt,
        ImpactAssessment impact,
        decimal? actual = null,
        decimal? forecast = null,
        decimal? previous = null,
        string? unit = null,
        string? sourceRef = null,
        DateTimeOffset? rescheduledInstant = null)
    {
        if (LatestRevision is { } last && knownAt < last.KnownAt)
            throw new DomainException(DomainErrors.CalendarRevisionOutOfOrder);

        _revisions.Add(EventRevision.Create(
            _revisions.Count, knownAt, kind, impact, actual, forecast, previous, unit, sourceRef, rescheduledInstant));
    }
}
