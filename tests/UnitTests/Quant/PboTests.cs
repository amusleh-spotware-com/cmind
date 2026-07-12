using System;
using System.Linq;
using Core.Domain;
using Core.Quant;
using FluentAssertions;
using Xunit;

namespace UnitTests;

public class PboTests
{
    private static ReturnSeries Trial(Func<int, double> f, int t = 16) =>
        ReturnSeries.From(Enumerable.Range(0, t).Select(f).ToArray());

    // One trial dominates every subset (in- and out-of-sample) → the winner always generalizes.
    private static TrialSurface GenuineEdge => TrialSurface.From(new[]
    {
        Trial(i => 0.01 + (i % 2 == 0 ? 0.001 : -0.001)), // Sharpe ≈ 10 everywhere
        Trial(i => 0.0 + (i % 2 == 0 ? 0.02 : -0.02)),
        Trial(i => -0.005 + (i % 2 == 0 ? 0.02 : -0.02)),
        Trial(i => 0.001 + (i % 2 == 0 ? 0.02 : -0.02)),
    });

    // Two mirror-image trials: whichever wins in-sample loses out-of-sample → textbook overfitting.
    private static TrialSurface MirrorOverfit
    {
        get
        {
            static double J(int i) => i % 2 == 0 ? 0.002 : -0.002;
            return TrialSurface.From(new[]
            {
                Trial(i => (i < 8 ? 0.01 : -0.01) + J(i)),
                Trial(i => (i < 8 ? -0.01 : 0.01) + J(i)),
            });
        }
    }

    [Fact]
    public void Genuine_edge_has_low_pbo()
    {
        CombinatorialCrossValidation.ProbabilityOfBacktestOverfitting(GenuineEdge, 8).Should().BeLessThan(0.2);
    }

    [Fact]
    public void Mirror_strategies_have_high_pbo()
    {
        CombinatorialCrossValidation.ProbabilityOfBacktestOverfitting(MirrorOverfit, 8).Should().BeGreaterThan(0.4);
    }

    [Fact]
    public void AnalyzeGrid_scores_a_persistent_winner_robust()
    {
        var report = new BacktestIntegrityAnalyzer().AnalyzeGrid(GenuineEdge);
        report.ProbabilityOfBacktestOverfitting.Should().NotBeNull();
        report.ProbabilityOfBacktestOverfitting!.Value.Value.Should().BeLessThan(0.2);
        report.Verdict.Should().Be(Verdict.Robust);
        report.Trials.Should().Be(4);
    }

    [Fact]
    public void AnalyzeGrid_scores_mirror_strategies_overfit()
    {
        var report = new BacktestIntegrityAnalyzer().AnalyzeGrid(MirrorOverfit);
        report.Verdict.Should().Be(Verdict.Overfit);
        report.ProbabilityOfBacktestOverfitting!.Value.Value.Should().BeGreaterThan(0.4);
    }

    [Theory]
    [InlineData(1)]
    public void TrialSurface_requires_at_least_two_trials(int count)
    {
        var trials = Enumerable.Range(0, count).Select(_ => Trial(i => 0.01 + i * 0.0)).ToArray();
        var act = () => TrialSurface.From(trials);
        act.Should().Throw<DomainException>().Which.Code.Should().Be("domain.quant.trial_surface_invalid");
    }

    [Fact]
    public void TrialSurface_rejects_misaligned_or_tiny_series()
    {
        var mismatch = () => TrialSurface.From(new[] { Trial(i => 0.01, 16), Trial(i => 0.01, 12) });
        mismatch.Should().Throw<DomainException>().Which.Code.Should().Be("domain.quant.trial_surface_invalid");

        var tooShort = () => TrialSurface.From(new[] { Trial(i => 0.01, 3), Trial(i => 0.02, 3) });
        tooShort.Should().Throw<DomainException>().Which.Code.Should().Be("domain.quant.trial_surface_invalid");
    }
}
