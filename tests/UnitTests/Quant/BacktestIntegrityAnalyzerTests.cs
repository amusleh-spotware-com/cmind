using System.Linq;
using Core.Quant;
using FluentAssertions;
using Xunit;

namespace UnitTests;

public class BacktestIntegrityAnalyzerTests
{
    private readonly BacktestIntegrityAnalyzer _analyzer = new();

    // Deterministic series builders (mean m, alternating jitter j so the sample std is exactly j).
    private static ReturnSeries Series(int n, double mean, double jitter) =>
        ReturnSeries.From(Enumerable.Range(0, n)
            .Select(i => mean + (i % 2 == 0 ? jitter : -jitter)).ToArray());

    private static ReturnSeries StrongEdge => Series(250, 0.005, 0.001);   // Sharpe ≈ 5
    private static ReturnSeries WeakEdge => Series(100, 0.006, 0.02);      // Sharpe ≈ 0.3
    private static ReturnSeries NoEdge => Series(50, 0.0001, 0.02);        // Sharpe ≈ 0.005

    [Fact]
    public void Single_trial_makes_deflated_equal_probabilistic()
    {
        var r = _analyzer.Analyze(WeakEdge, new TrialCount(1));
        r.DeflatedSharpe.Value.Should().BeApproximately(r.ProbabilisticSharpe.Value, 1e-9);
    }

    [Fact]
    public void More_trials_never_increase_the_deflated_sharpe()
    {
        var d1 = _analyzer.Analyze(WeakEdge, new TrialCount(1)).DeflatedSharpe.Value;
        var d100 = _analyzer.Analyze(WeakEdge, new TrialCount(100)).DeflatedSharpe.Value;
        var d10000 = _analyzer.Analyze(WeakEdge, new TrialCount(10000)).DeflatedSharpe.Value;

        d1.Should().BeGreaterThanOrEqualTo(d100);
        d100.Should().BeGreaterThanOrEqualTo(d10000);
        d1.Should().BeGreaterThan(d10000);
    }

    [Fact]
    public void Strong_edge_with_one_trial_is_robust()
    {
        var r = _analyzer.Analyze(StrongEdge, new TrialCount(1));
        r.Verdict.Should().Be(Verdict.Robust);
        r.ProbabilisticSharpe.Value.Should().BeGreaterThan(0.99);
        r.DeflatedSharpe.Value.Should().BeGreaterThan(0.99);
        r.TStatistic.Should().BeGreaterThan(3.0);
    }

    [Fact]
    public void No_edge_under_many_trials_is_overfit()
    {
        var r = _analyzer.Analyze(NoEdge, new TrialCount(1000));
        r.Verdict.Should().Be(Verdict.Overfit);
        r.DeflatedSharpe.Value.Should().BeLessThan(0.90);
    }

    [Fact]
    public void Trials_can_downgrade_a_borderline_result()
    {
        var few = _analyzer.Analyze(WeakEdge, new TrialCount(1)).Verdict;
        var many = _analyzer.Analyze(WeakEdge, new TrialCount(100000)).Verdict;
        ((int)many).Should().BeGreaterThanOrEqualTo((int)few); // Robust=0 < Fragile=1 < Overfit=2
    }

    [Fact]
    public void Non_finite_benchmark_and_bad_periods_fall_back_without_throwing()
    {
        var act = () => _analyzer.Analyze(StrongEdge, new TrialCount(1), double.NaN, periodsPerYear: -5);
        act.Should().NotThrow();
        var r = _analyzer.Analyze(StrongEdge, new TrialCount(1), double.NaN, periodsPerYear: 0);
        r.ProbabilisticSharpe.Value.Should().BeInRange(0.0, 1.0);
        r.AnnualizedSharpe.Should().Be(r.Sharpe * System.Math.Sqrt(252)); // periods fell back to 252
    }

    [Fact]
    public void Report_carries_inputs_and_no_pbo_in_single_report_mode()
    {
        var r = _analyzer.Analyze(StrongEdge, new TrialCount(7));
        r.Observations.Should().Be(250);
        r.Trials.Should().Be(7);
        r.ProbabilityOfBacktestOverfitting.Should().BeNull();
        r.Rationale.Should().Contain("Verdict");
    }
}
