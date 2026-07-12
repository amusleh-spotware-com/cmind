using Core.Domain;

namespace Core.Ai.CurrencyStrength;

/// <summary>
/// A deployment-scoped point-in-time snapshot of the macro picture (the global picture is not per-user). Holds
/// the pre-serialized read model — the current ranking, the forward pair-outlook matrix for every horizon, the
/// raw indicators, and the AI narrative — plus provenance: how it was sourced and the calendar's point-in-time
/// anchor. One <c>SaveChanges</c> mutates exactly this one aggregate; it is immutable once created.
/// </summary>
public sealed class CurrencyStrengthSnapshot : AuditedEntity<CurrencyStrengthSnapshotId>
{
    public DateTimeOffset AsOf { get; private set; }
    public string RankingJson { get; private set; } = default!;
    public string HorizonsJson { get; private set; } = default!;
    public string IndicatorsJson { get; private set; } = default!;
    public string Narrative { get; private set; } = default!;
    public SnapshotSource Source { get; private set; }
    public DateTimeOffset? CalendarKnownAt { get; private set; }

    private CurrencyStrengthSnapshot()
    {
    }

    public static CurrencyStrengthSnapshot Create(
        DateTimeOffset asOf,
        string rankingJson,
        string horizonsJson,
        string indicatorsJson,
        string narrative,
        SnapshotSource source,
        DateTimeOffset? calendarKnownAt) =>
        new()
        {
            AsOf = asOf,
            RankingJson = rankingJson,
            HorizonsJson = horizonsJson,
            IndicatorsJson = indicatorsJson,
            Narrative = narrative ?? string.Empty,
            Source = source,
            CalendarKnownAt = calendarKnownAt
        };
}
