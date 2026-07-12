using Core.Calendar;
using Core.Constants;
using Core.Domain;
using FluentAssertions;
using Xunit;

namespace UnitTests.Calendar;

public sealed class CalendarValueObjectsTests
{
    [Fact]
    public void SeriesCode_upper_cases_and_trims()
    {
        new SeriesCode("  us.cpi.mom ").Value.Should().Be("US.CPI.MOM");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void SeriesCode_rejects_blank(string value)
    {
        var act = () => new SeriesCode(value);
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.CalendarSeriesCodeRequired);
    }

    [Theory]
    [InlineData("us", "US")]
    [InlineData("De", "DE")]
    public void CountryCode_normalizes_to_two_upper_letters(string input, string expected)
    {
        new CountryCode(input).Value.Should().Be(expected);
    }

    [Theory]
    [InlineData("USA")]
    [InlineData("U")]
    [InlineData("U1")]
    public void CountryCode_rejects_non_alpha2(string value)
    {
        var act = () => new CountryCode(value);
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.CalendarCountryCodeInvalid);
    }

    [Fact]
    public void CurrencyCode_requires_three_letters()
    {
        new CurrencyCode("usd").Value.Should().Be("USD");
        var act = () => new CurrencyCode("US");
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.CalendarCurrencyCodeInvalid);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(double.NaN)]
    public void ImpactScore_range_checks(double value)
    {
        var act = () => new ImpactScore(value);
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.CalendarImpactScoreOutOfRange);
    }

    [Fact]
    public void Surprise_zscore_is_positive_on_beat_negative_on_miss()
    {
        Surprise.From(110m, 100m, 5).ZScore.Should().BeApproximately(2.0, 1e-9);
        Surprise.From(110m, 100m, 5).IsBeat.Should().BeTrue();
        Surprise.From(90m, 100m, 5).IsMiss.Should().BeTrue();
    }

    [Fact]
    public void Surprise_is_zero_when_no_forecast_or_non_positive_stddev()
    {
        Surprise.From(110m, null, 5).ZScore.Should().Be(0);
        Surprise.From(110m, 100m, 0).ZScore.Should().Be(0);
    }

    [Fact]
    public void ReleaseWindow_anchors_instant_to_utc()
    {
        var local = new DateTimeOffset(2024, 2, 13, 8, 30, 0, TimeSpan.FromHours(-5));
        ReleaseWindow.Exact(local).Instant.Offset.Should().Be(TimeSpan.Zero);
        ReleaseWindow.Exact(local).Precision.Should().Be(ReleasePrecision.Exact);
    }
}
