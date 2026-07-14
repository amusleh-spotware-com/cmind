using System;
using System.Linq;
using Core.Portfolio;
using Core.Quant;
using FluentAssertions;
using Xunit;

namespace UnitTests;

public class PositionSizerTests
{
    private readonly PositionSizer _sizer = new();

    private static ReturnSeries Series(int n, double mean, double jitter) =>
        ReturnSeries.From(Enumerable.Range(0, n).Select(i => mean + (i % 2 == 0 ? jitter : -jitter)).ToArray());

    [Fact]
    public void Volatility_target_binds_when_it_is_the_smaller_exposure()
    {
        // std = 0.02 → annual vol ≈ 0.02·√252 ≈ 0.3175; target 10% → vol fraction ≈ 0.315.
        var r = _sizer.Size(Series(100, 0.002, 0.02), new VolatilityTarget(0.10), KellyFraction.Half, LeverageCap.Default);

        r.RealizedAnnualVolatility.Should().BeApproximately(0.02 * Math.Sqrt(252), 1e-6);
        r.VolatilityTargetFraction.Should().BeApproximately(0.10 / (0.02 * Math.Sqrt(252)), 1e-6);
        r.FullKellyFraction.Should().BeApproximately(0.002 / 0.0004, 1e-6); // μ/σ² = 5
        r.FractionalKellyFraction.Should().BeApproximately(2.5, 1e-6);      // half of 5
        r.RecommendedFraction.Should().BeApproximately(r.VolatilityTargetFraction, 1e-6); // vol target is smaller
    }

    [Fact]
    public void Sizes_a_rising_equity_curve_to_a_positive_exposure()
    {
        // A rising, wobbling equity/balance curve → returns derived from it carry a positive edge, so the
        // recommendation is a non-zero exposure. Guards the equity-curve sizing path end to end (domain).
        var equity = Enumerable.Range(0, 60).Select(i => 1000.0 + (i * 5) + (i % 2 == 0 ? 8 : -8)).ToArray();
        var r = _sizer.Size(ReturnSeries.FromEquityCurve(equity), new VolatilityTarget(0.10), KellyFraction.Half, LeverageCap.Default);

        r.FullKellyFraction.Should().BeGreaterThan(0.0);
        r.RecommendedFraction.Should().BeGreaterThan(0.0);
    }

    [Fact]
    public void Negative_edge_recommends_no_exposure()
    {
        var r = _sizer.Size(Series(50, -0.001, 0.02), new VolatilityTarget(0.10), KellyFraction.Half, LeverageCap.Default);
        r.FullKellyFraction.Should().BeLessThan(0.0);
        r.RecommendedFraction.Should().Be(0.0);
    }

    [Fact]
    public void Recommendation_is_capped_by_leverage()
    {
        // Tiny vol, big edge → both sizings blow past the cap; result is clamped to it.
        var r = _sizer.Size(Series(100, 0.005, 0.001), new VolatilityTarget(1.0), KellyFraction.Half, new LeverageCap(2.0));
        r.RecommendedFraction.Should().BeLessThanOrEqualTo(2.0);
        r.VolatilityTargetFraction.Should().BeLessThanOrEqualTo(2.0);
    }

    [Fact]
    public void Flat_series_has_no_edge()
    {
        var r = _sizer.Size(ReturnSeries.From(new[] { 0.01, 0.01, 0.01 }), new VolatilityTarget(0.10), KellyFraction.Half, LeverageCap.Default);
        r.FullKellyFraction.Should().Be(0.0);
        r.RecommendedFraction.Should().Be(0.0);
    }
}
