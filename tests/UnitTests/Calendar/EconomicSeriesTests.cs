using Core.Calendar;
using Core.Constants;
using Core.Domain;
using FluentAssertions;
using Xunit;

namespace UnitTests.Calendar;

public sealed class EconomicSeriesTests
{
    private static readonly DateTimeOffset Now = new(2024, 2, 1, 0, 0, 0, TimeSpan.Zero);

    private static EconomicSeries Cpi(double prior = 0.85) =>
        EconomicSeries.Create(
            new SeriesCode("US.CPI.MoM"), new CountryCode("US"), "US CPI (MoM)",
            MarketMovingCategory.Inflation, ReleaseCadence.Monthly, prior, "FRED", "CPIAUCSL");

    [Fact]
    public void Create_derives_default_impact_from_prior()
    {
        Cpi(0.85).DefaultImpact.Should().Be(ImpactLevel.Critical);
        Cpi(0.10).DefaultImpact.Should().Be(ImpactLevel.Low);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void Create_rejects_out_of_range_prior(double prior)
    {
        var act = () => Cpi(prior);
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.CalendarImpactScoreOutOfRange);
    }

    [Fact]
    public void ScheduleRelease_produces_an_event_bound_to_the_series_by_id()
    {
        var series = Cpi();
        var e = series.ScheduleRelease(ReleaseWindow.Exact(Now.AddDays(12)), "America/New_York", Now);

        e.SeriesId.Should().Be(series.Id);
        e.SeriesCode.Value.Should().Be("US.CPI.MOM");
        e.Released.Should().BeFalse();
    }

    [Fact]
    public void SetImpactPrior_reclassifies_default_impact()
    {
        var series = Cpi(0.10);
        series.DefaultImpact.Should().Be(ImpactLevel.Low);

        series.SetImpactPrior(0.85);
        series.DefaultImpact.Should().Be(ImpactLevel.Critical);
    }
}
