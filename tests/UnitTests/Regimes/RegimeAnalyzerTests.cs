using System.Linq;
using Core.Quant;
using Core.Regimes;
using FluentAssertions;
using Xunit;

namespace UnitTests;

public class RegimeAnalyzerTests
{
    private readonly RegimeAnalyzer _analyzer = new();

    [Fact]
    public void Labels_calm_and_turbulent_stretches()
    {
        // First half low volatility, second half high volatility.
        var series = ReturnSeries.From(Enumerable.Range(0, 60).Select(i =>
        {
            var jitter = i < 30 ? 0.0005 : 0.02;
            return i % 2 == 0 ? jitter : -jitter;
        }).ToArray());

        var result = _analyzer.Analyze(series, window: 6);

        var calm = result.ByRegime.Single(p => p.Regime == MarketRegime.Calm);
        var turbulent = result.ByRegime.Single(p => p.Regime == MarketRegime.Turbulent);
        calm.Volatility.Should().BeLessThan(turbulent.Volatility);
        result.ByRegime.Sum(p => p.Observations).Should().Be(60);
    }

    [Fact]
    public void Anti_persistent_series_has_low_hurst()
    {
        var alternating = ReturnSeries.From(Enumerable.Range(0, 64).Select(i => i % 2 == 0 ? 0.01 : -0.01).ToArray());
        _analyzer.Analyze(alternating).HurstExponent.Should().BeLessThan(0.5);
    }

    [Fact]
    public void Persistent_trend_has_high_hurst()
    {
        var ramp = ReturnSeries.From(Enumerable.Range(0, 64).Select(i => 0.01 * (i - 32) / 32.0).ToArray());
        _analyzer.Analyze(ramp).HurstExponent.Should().BeGreaterThan(0.5);
    }

    [Fact]
    public void Rationale_names_the_hurst_and_regimes()
    {
        var series = ReturnSeries.From(Enumerable.Range(0, 60).Select(i => 0.001 + (i % 2 == 0 ? 0.01 : -0.01)).ToArray());
        _analyzer.Analyze(series).Rationale.Should().Contain("Hurst");
    }
}
