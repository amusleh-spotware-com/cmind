using Core;
using Core.Calendar;
using Core.Constants;
using Core.Domain;
using FluentAssertions;
using Xunit;

namespace UnitTests.Calendar;

public sealed class NewsWindowPolicyTests
{
    private static readonly DateTimeOffset Release = new(2024, 2, 13, 13, 30, 0, TimeSpan.Zero);
    private readonly NewsWindowPolicy _policy = new();

    private static CalendarEventSnapshot Nfp(ImpactLevel impact = ImpactLevel.High) =>
        new(CalendarEventId.New(), new SeriesCode("US.NFP"), new CountryCode("US"), Release, impact);

    [Fact]
    public void Inside_window_is_blackout_and_reports_edges()
    {
        var rule = new NewsWindowRule(ImpactLevel.High, 30, 30);
        var result = _policy.Evaluate(new Symbol("EURUSD"), Release.AddMinutes(-5), rule, [Nfp()]);

        result.InBlackout.Should().BeTrue();
        result.StartsAt.Should().Be(Release.AddMinutes(-30));
        result.EndsAt.Should().Be(Release.AddMinutes(30));
    }

    [Theory]
    [InlineData(-30)]
    [InlineData(30)]
    public void Window_edges_are_inclusive(int offsetMinutes)
    {
        var rule = new NewsWindowRule(ImpactLevel.High, 30, 30);
        _policy.Evaluate(new Symbol("EURUSD"), Release.AddMinutes(offsetMinutes), rule, [Nfp()])
            .InBlackout.Should().BeTrue();
    }

    [Fact]
    public void Outside_window_is_clear()
    {
        var rule = new NewsWindowRule(ImpactLevel.High, 30, 30);
        _policy.Evaluate(new Symbol("EURUSD"), Release.AddMinutes(31), rule, [Nfp()])
            .InBlackout.Should().BeFalse();
    }

    [Fact]
    public void Below_min_impact_does_not_trigger()
    {
        var rule = new NewsWindowRule(ImpactLevel.Critical, 30, 30);
        _policy.Evaluate(new Symbol("EURUSD"), Release, rule, [Nfp(ImpactLevel.High)])
            .InBlackout.Should().BeFalse();
    }

    [Fact]
    public void Symbol_not_affected_by_country_is_clear()
    {
        var rule = new NewsWindowRule(ImpactLevel.High, 30, 30);
        _policy.Evaluate(new Symbol("AUDNZD"), Release, rule, [Nfp()]).InBlackout.Should().BeFalse();
    }

    [Fact]
    public void Currency_filter_narrows_the_rule()
    {
        var eurOnly = new NewsWindowRule(ImpactLevel.High, 30, 30, currencies: new HashSet<string> { "EUR" });
        _policy.Evaluate(new Symbol("EURUSD"), Release, eurOnly, [Nfp()]).InBlackout.Should().BeFalse();

        var usdOnly = new NewsWindowRule(ImpactLevel.High, 30, 30, currencies: new HashSet<string> { "USD" });
        _policy.Evaluate(new Symbol("EURUSD"), Release, usdOnly, [Nfp()]).InBlackout.Should().BeTrue();
    }

    [Fact]
    public void Series_filter_narrows_the_rule()
    {
        var cpiOnly = new NewsWindowRule(ImpactLevel.High, 30, 30, series: new HashSet<string> { "US.CPI" });
        _policy.Evaluate(new Symbol("EURUSD"), Release, cpiOnly, [Nfp()]).InBlackout.Should().BeFalse();
    }

    [Fact]
    public void Overlapping_events_return_the_earliest_starting_window()
    {
        var early = new CalendarEventSnapshot(
            CalendarEventId.New(), new SeriesCode("US.CPI"), new CountryCode("US"),
            Release.AddMinutes(-10), ImpactLevel.High);

        var rule = new NewsWindowRule(ImpactLevel.High, 30, 30);
        var result = _policy.Evaluate(new Symbol("EURUSD"), Release, rule, [Nfp(), early]);

        result.InBlackout.Should().BeTrue();
        result.StartsAt.Should().Be(Release.AddMinutes(-40));
    }

    [Fact]
    public void Zero_length_window_is_rejected()
    {
        var act = () => new NewsWindowRule(ImpactLevel.High, 0, 0);
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.CalendarNewsWindowInvalid);
    }
}
