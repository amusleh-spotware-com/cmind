using System;
using System.Linq;
using Core.Health;
using Core.Quant;
using FluentAssertions;
using Xunit;

namespace UnitTests;

public class StrategyHealthMonitorTests
{
    private readonly StrategyHealthMonitor _monitor = new();

    private static ReturnSeries TwoPhase(int n, double m1, double j1, double m2, double j2)
    {
        var half = n / 2;
        return ReturnSeries.From(Enumerable.Range(0, n).Select(i =>
        {
            var (m, j) = i < half ? (m1, j1) : (m2, j2);
            return m + (i % 2 == 0 ? j : -j);
        }).ToArray());
    }

    [Fact]
    public void Short_series_is_unknown()
    {
        var r = _monitor.Assess(ReturnSeries.From(new[] { 0.01, 0.02, 0.01 }));
        r.Health.Should().Be(StrategyHealth.Unknown);
    }

    [Fact]
    public void Stationary_strong_edge_is_healthy_with_no_change_point()
    {
        var r = _monitor.Assess(TwoPhase(40, 0.005, 0.001, 0.005, 0.001));
        r.Health.Should().Be(StrategyHealth.Healthy);
        r.ChangePointIndex.Should().BeNull();
    }

    [Fact]
    public void Edge_that_dies_is_decayed_with_a_change_point()
    {
        var r = _monitor.Assess(TwoPhase(40, 0.01, 0.002, 0.0, 0.02)); // Sharpe ~5 → ~0
        r.Health.Should().Be(StrategyHealth.Decayed);
        r.EarlierSharpe.Should().BeGreaterThan(r.RecentSharpe);
        r.ChangePointIndex.Should().NotBeNull();
        r.ChangePointIndex!.Value.Should().BeInRange(15, 25);
    }

    [Fact]
    public void Moderate_decline_is_degrading()
    {
        var r = _monitor.Assess(TwoPhase(40, 0.004, 0.002, 0.002, 0.002)); // Sharpe 2 → 1
        r.Health.Should().Be(StrategyHealth.Degrading);
    }

    [Fact]
    public void Rationale_is_populated()
    {
        var r = _monitor.Assess(TwoPhase(40, 0.01, 0.002, 0.0, 0.02));
        r.Rationale.Should().Contain("Sharpe");
    }
}
