using Core.Domain;
using Core.Quant;
using FluentAssertions;
using Xunit;

namespace UnitTests;

public class ReturnSeriesTests
{
    [Fact]
    public void From_requires_at_least_two_observations()
    {
        var act = () => ReturnSeries.From(new[] { 0.01 });
        act.Should().Throw<DomainException>().Which.Code.Should().Be("domain.quant.return_series_too_short");
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void From_rejects_non_finite_values(double bad)
    {
        var act = () => ReturnSeries.From(new[] { 0.01, bad });
        act.Should().Throw<DomainException>().Which.Code.Should().Be("domain.quant.return_series_not_finite");
    }

    [Fact]
    public void Computes_mean_and_standard_deviation()
    {
        var s = ReturnSeries.From(new[] { 0.02, 0.00, 0.02, 0.00 });
        s.Count.Should().Be(4);
        s.Mean.Should().BeApproximately(0.01, 1e-12);
        s.StandardDeviation.Should().BeApproximately(0.01, 1e-12); // population std of {.02,0,.02,0}
        s.Sharpe.Should().BeApproximately(1.0, 1e-12);
    }

    [Fact]
    public void Symmetric_series_has_zero_skewness()
    {
        var s = ReturnSeries.From(new[] { 0.01, -0.01, 0.01, -0.01, 0.01, -0.01 });
        s.Skewness.Should().BeApproximately(0.0, 1e-9);
    }

    [Fact]
    public void Flat_series_has_zero_sharpe()
    {
        var s = ReturnSeries.From(new[] { 0.005, 0.005, 0.005 });
        s.StandardDeviation.Should().Be(0.0);
        s.Sharpe.Should().Be(0.0);
    }

    [Fact]
    public void FromEquityCurve_derives_simple_returns()
    {
        var s = ReturnSeries.FromEquityCurve(new[] { 100.0, 110.0, 99.0 });
        s.Count.Should().Be(2);
        s.Values[0].Should().BeApproximately(0.10, 1e-12);
        s.Values[1].Should().BeApproximately(-0.10, 1e-12);
    }

    [Fact]
    public void FromEquityCurve_skips_non_positive_prior_equity()
    {
        var act = () => ReturnSeries.FromEquityCurve(new[] { 0.0, 100.0 });
        // only one usable transition remains -> too short
        act.Should().Throw<DomainException>().Which.Code.Should().Be("domain.quant.return_series_too_short");
    }

    [Fact]
    public void AnnualizedSharpe_scales_by_sqrt_of_periods()
    {
        var s = ReturnSeries.From(new[] { 0.02, 0.00, 0.02, 0.00 });
        s.AnnualizedSharpe(252).Should().BeApproximately(s.Sharpe * System.Math.Sqrt(252), 1e-9);
    }
}
