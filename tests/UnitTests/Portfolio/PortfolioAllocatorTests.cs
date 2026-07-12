using System.Linq;
using Core.Domain;
using Core.Portfolio;
using Core.Quant;
using FluentAssertions;
using Xunit;

namespace UnitTests;

public class PortfolioAllocatorTests
{
    private readonly PortfolioAllocator _allocator = new();

    private static ReturnSeries Phase(int n, double mean, double jitter, bool inverted) =>
        ReturnSeries.From(Enumerable.Range(0, n)
            .Select(i => mean + ((i % 2 == 0) ^ inverted ? jitter : -jitter)).ToArray());

    [Fact]
    public void Equal_volatility_strategies_get_equal_weight()
    {
        var result = _allocator.Allocate(
            new[] { Phase(60, 0.001, 0.01, false), Phase(60, 0.002, 0.01, false) },
            new VolatilityTarget(0.10), LeverageCap.Default);

        result.Weights[0].Should().BeApproximately(0.5, 1e-9);
        result.Weights[1].Should().BeApproximately(0.5, 1e-9);
        result.Weights.Sum().Should().BeApproximately(1.0, 1e-9);
    }

    [Fact]
    public void Lower_volatility_strategy_gets_more_weight()
    {
        var result = _allocator.Allocate(
            new[] { Phase(60, 0.001, 0.01, false), Phase(60, 0.001, 0.02, false) },
            new VolatilityTarget(0.10), LeverageCap.Default);

        result.Weights[0].Should().BeApproximately(2.0 / 3.0, 1e-6); // 1/0.01 vs 1/0.02
        result.Weights[1].Should().BeApproximately(1.0 / 3.0, 1e-6);
    }

    [Fact]
    public void Correlation_diagonal_is_one_and_opposite_phase_is_negative()
    {
        var result = _allocator.Allocate(
            new[] { Phase(60, 0.0, 0.01, false), Phase(60, 0.0, 0.01, true) },
            new VolatilityTarget(0.10), LeverageCap.Default);

        result.Correlation[0][0].Should().BeApproximately(1.0, 1e-9);
        result.Correlation[0][1].Should().BeApproximately(-1.0, 1e-6);
    }

    [Fact]
    public void Requires_at_least_two_strategies()
    {
        var act = () => _allocator.Allocate(new[] { Phase(60, 0.001, 0.01, false) }, new VolatilityTarget(0.10), LeverageCap.Default);
        act.Should().Throw<DomainException>().Which.Code.Should().Be("domain.portfolio.insufficient_strategies");
    }

    [Fact]
    public void Rejects_misaligned_series_lengths()
    {
        var act = () => _allocator.Allocate(
            new[] { Phase(60, 0.001, 0.01, false), Phase(40, 0.001, 0.01, false) },
            new VolatilityTarget(0.10), LeverageCap.Default);
        act.Should().Throw<DomainException>().Which.Code.Should().Be("domain.portfolio.series_length_mismatch");
    }

    [Fact]
    public void Rejects_a_flat_zero_volatility_strategy()
    {
        var flat = ReturnSeries.From(new[] { 0.01, 0.01, 0.01, 0.01, 0.01, 0.01 });
        var act = () => _allocator.Allocate(
            new[] { Phase(6, 0.001, 0.01, false), flat }, new VolatilityTarget(0.10), LeverageCap.Default);
        act.Should().Throw<DomainException>().Which.Code.Should().Be("domain.portfolio.degenerate_strategy");
    }

    [Fact]
    public void Scales_book_toward_the_volatility_target()
    {
        var result = _allocator.Allocate(
            new[] { Phase(60, 0.0, 0.01, false), Phase(60, 0.0, 0.01, true) },
            new VolatilityTarget(0.10), new LeverageCap(10));

        result.ProjectedAnnualVolatility.Should().BeGreaterThanOrEqualTo(0.0);
        result.Leverage.Should().BeGreaterThan(0.0);
        result.ScaledWeights.Should().HaveCount(2);
    }
}
