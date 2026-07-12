using Core.Domain;
using Core.Signals;
using FluentAssertions;
using Xunit;

namespace UnitTests;

public class SignalsTests
{
    [Theory]
    [InlineData(70, ContrarianBias.Bearish)]
    [InlineData(60, ContrarianBias.Bearish)]
    [InlineData(50, ContrarianBias.Neutral)]
    [InlineData(40, ContrarianBias.Bullish)]
    [InlineData(25, ContrarianBias.Bullish)]
    public void Positioning_maps_to_contrarian_bias(double longPct, ContrarianBias expected)
    {
        new RetailPositioning(longPct).Bias.Should().Be(expected);
    }

    [Fact]
    public void Positioning_exposes_short_and_strength()
    {
        var p = new RetailPositioning(80);
        p.ShortPercent.Should().Be(20);
        p.Strength.Should().BeApproximately(0.6, 1e-9);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(double.NaN)]
    public void Positioning_rejects_out_of_range(double bad)
    {
        var act = () => new RetailPositioning(bad);
        act.Should().Throw<DomainException>().Which.Code.Should().Be("domain.signals.positioning_invalid");
    }

    [Fact]
    public void Sentiment_score_validates_and_classifies()
    {
        new SentimentScore(0.5).IsBullish.Should().BeTrue();
        new SentimentScore(-0.5).IsBearish.Should().BeTrue();
        var act = () => new SentimentScore(2.0);
        act.Should().Throw<DomainException>().Which.Code.Should().Be("domain.signals.sentiment_score_invalid");
    }

    [Fact]
    public void Point_in_time_signal_requires_a_stamp_and_guards_leakage()
    {
        var t = new DateTimeOffset(2026, 7, 12, 0, 0, 0, TimeSpan.Zero);
        var signal = new PointInTimeSignal(t, "sentiment", 0.3, "test");
        signal.IsKnownAt(t.AddMinutes(1)).Should().BeTrue();
        signal.IsKnownAt(t.AddMinutes(-1)).Should().BeFalse();

        var act = () => new PointInTimeSignal(default, "k", 0, "p");
        act.Should().Throw<DomainException>().Which.Code.Should().Be("domain.signals.point_in_time_invalid");
    }
}
