using System.ComponentModel.DataAnnotations;
using Core.Constants;
using Core.Domain;

namespace Core.Calendar;

/// <summary>
/// A tracked indicator in the catalog — CPI, NFP, a rate decision — identified by a stable <see cref="Code"/>
/// and bound to the primary source that publishes it. Carries the release cadence and the impact prior used
/// when scoring its events. Concrete scheduled releases are <see cref="EconomicEvent"/> aggregates that
/// reference the series by strong id.
/// </summary>
public sealed class EconomicSeries : AuditedEntity<EconomicSeriesId>
{
    [MaxLength(64)] public string SeriesCodeValue { get; private set; } = default!;
    [MaxLength(2)] public string CountryValue { get; private set; } = default!;
    [MaxLength(128)] public string Name { get; private set; } = default!;
    public MarketMovingCategory Category { get; private set; }
    public ReleaseCadence Cadence { get; private set; }
    public ImpactLevel DefaultImpact { get; private set; }

    /// <summary>The impact prior weight for this indicator class, in [0, 1], fed into <see cref="ImpactModel"/>.</summary>
    public double ImpactPrior { get; private set; }

    /// <summary>The source that publishes this series (e.g. <c>FRED</c>) and its native series identifier.</summary>
    [MaxLength(32)] public string SourceName { get; private set; } = default!;
    [MaxLength(128)] public string SourceSeriesId { get; private set; } = default!;

    public SeriesCode Code => new(SeriesCodeValue);
    public CountryCode Country => new(CountryValue);

    private EconomicSeries()
    {
    }

    public static EconomicSeries Create(
        SeriesCode code,
        CountryCode country,
        string name,
        MarketMovingCategory category,
        ReleaseCadence cadence,
        double impactPrior,
        string sourceName,
        string sourceSeriesId)
    {
        if (impactPrior is < 0 or > 1) throw new DomainException(DomainErrors.CalendarImpactScoreOutOfRange);

        var series = new EconomicSeries
        {
            SeriesCodeValue = code.Value,
            CountryValue = country.Value,
            Name = DomainGuard.AgainstNullOrWhiteSpace(name, DomainErrors.NameRequired),
            Category = category,
            Cadence = cadence,
            ImpactPrior = impactPrior,
            SourceName = DomainGuard.AgainstNullOrWhiteSpace(sourceName, DomainErrors.CalendarSeriesCodeRequired),
            SourceSeriesId = DomainGuard.AgainstNullOrWhiteSpace(sourceSeriesId, DomainErrors.CalendarSeriesCodeRequired)
        };

        series.DefaultImpact = ImpactBands.Default.Classify(new ImpactScore(impactPrior * 100));
        return series;
    }

    public void Rename(string name) =>
        Name = DomainGuard.AgainstNullOrWhiteSpace(name, DomainErrors.NameRequired);

    /// <summary>Adjusts the impact prior (0..1) as more realized-vol history accrues.</summary>
    public void SetImpactPrior(double impactPrior)
    {
        if (impactPrior is < 0 or > 1) throw new DomainException(DomainErrors.CalendarImpactScoreOutOfRange);
        ImpactPrior = impactPrior;
        DefaultImpact = ImpactBands.Default.Classify(new ImpactScore(impactPrior * 100));
    }

    /// <summary>Schedules a concrete release of this series; returns a new <see cref="EconomicEvent"/> aggregate.</summary>
    public EconomicEvent ScheduleRelease(ReleaseWindow window, string sourceTimeZone, DateTimeOffset now)
    {
        var impact = ImpactModel.Score(new ImpactInputs(ImpactPrior, 0, 0));
        return EconomicEvent.Schedule(Id, Code, Country, window, sourceTimeZone, impact, now);
    }
}
