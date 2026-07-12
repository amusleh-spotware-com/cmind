using Core.Ai.CurrencyStrength;
using FluentAssertions;
using Xunit;
using static UnitTests.Ai.CurrencyStrength.CurrencyTestData;

namespace UnitTests.Ai.CurrencyStrength;

public sealed class ForwardOutlookCalculatorTests
{
    private readonly ForwardWeights _weights = ForwardWeights.Default();

    private static CurrencyStrengthRanking Ranking(params (Currency Currency, double Composite)[] scores) =>
        new([.. scores.Select(s => new CurrencyStrengthScore(s.Currency, s.Composite, [], DataConfidence.High))], AsOf);

    [Fact]
    public void A_hiking_currency_appreciates_against_a_cutting_currency()
    {
        var eur = Major("EUR");
        var usd = Major("USD");
        var current = Ranking((eur, 0.0), (usd, 0.0));
        var trajectories = new[]
        {
            Trajectory(eur, ratePathBp: 100),   // ECB on hold-to-hiking
            Trajectory(usd, ratePathBp: -100)   // Fed cutting
        };

        var (_, matrix) = ForwardOutlookCalculator.Project(current, trajectories, _weights, Horizon.ThreeMonths, AsOf);

        matrix.For(eur, usd)!.Bias.Should().Be(DirectionalBias.Appreciate);
        matrix.For(usd, eur)!.Bias.Should().Be(DirectionalBias.Depreciate);
    }

    [Fact]
    public void A_tiny_differential_reads_neutral_not_false_conviction()
    {
        var eur = Major("EUR");
        var usd = Major("USD");
        var current = Ranking((eur, 0.01), (usd, 0.0));
        var trajectories = new[] { Trajectory(eur), Trajectory(usd) };

        var (_, matrix) = ForwardOutlookCalculator.Project(current, trajectories, _weights, Horizon.ThreeMonths, AsOf);

        matrix.For(eur, usd)!.Bias.Should().Be(DirectionalBias.Neutral);
    }

    [Fact]
    public void The_pair_bias_is_the_exact_inverse_of_its_reverse()
    {
        var eur = Major("EUR");
        var usd = Major("USD");
        var current = Ranking((eur, 0.5), (usd, 0.0));
        var trajectories = new[] { Trajectory(eur, ratePathBp: 50), Trajectory(usd, ratePathBp: -50) };

        var (_, matrix) = ForwardOutlookCalculator.Project(current, trajectories, _weights, Horizon.SixMonths, AsOf);

        var forward = matrix.For(eur, usd)!;
        var reverse = matrix.For(usd, eur)!;
        ((int)forward.Bias).Should().Be(-(int)reverse.Bias);
        forward.Conviction.Should().BeApproximately(reverse.Conviction, 1e-9);
        forward.ProjectedDifferential.Should().BeApproximately(-reverse.ProjectedDifferential, 1e-9);
    }

    [Fact]
    public void A_longer_horizon_lets_the_trajectory_flip_a_pair_the_current_level_would_not()
    {
        var eur = Major("EUR");
        var usd = Major("USD");
        // USD leads on current level; EUR has the stronger forward trajectory.
        var current = Ranking((eur, 0.0), (usd, 0.3));
        var trajectories = new[]
        {
            Trajectory(eur, ratePathBp: 150),
            Trajectory(usd, ratePathBp: -150)
        };

        var near = ForwardOutlookCalculator.Project(current, trajectories, _weights, Horizon.OneMonth, AsOf).Matrix;
        var far = ForwardOutlookCalculator.Project(current, trajectories, _weights, Horizon.TwelveMonths, AsOf).Matrix;

        near.For(eur, usd)!.Bias.Should().NotBe(DirectionalBias.Appreciate, "the current level still dominates near-term");
        far.For(eur, usd)!.Bias.Should().Be(DirectionalBias.Appreciate, "the trajectory dominates the long horizon");
    }

    [Fact]
    public void Conviction_is_monotonic_in_the_absolute_differential()
    {
        var a = Major("USD");
        var b = Major("EUR");
        var c = Major("GBP");
        var current = Ranking((a, 1.0), (b, 0.0), (c, 0.5));
        var trajectories = new[] { Trajectory(a), Trajectory(b), Trajectory(c) };

        var (_, matrix) = ForwardOutlookCalculator.Project(current, trajectories, _weights, Horizon.ThreeMonths, AsOf);

        var wide = matrix.For(a, b)!;   // |diff| = 1.0
        var narrow = matrix.For(a, c)!; // |diff| = 0.5
        wide.Conviction.Should().BeGreaterThan(narrow.Conviction);
    }

    [Fact]
    public void Every_ordered_cross_is_present()
    {
        var current = Ranking((Major("USD"), 0.0), (Major("EUR"), 0.0), (Major("GBP"), 0.0));
        var trajectories = new[] { Trajectory(Major("USD")), Trajectory(Major("EUR")), Trajectory(Major("GBP")) };

        var (_, matrix) = ForwardOutlookCalculator.Project(current, trajectories, _weights, Horizon.ThreeMonths, AsOf);

        matrix.Pairs.Should().HaveCount(3 * 2);
        matrix.AsOf.Should().Be(AsOf);
    }

    [Fact]
    public void A_risk_off_geopolitical_trajectory_lifts_the_safe_havens()
    {
        var usd = Major("USD");
        var jpy = Major("JPY");
        var aud = Major("AUD");
        var current = Ranking((usd, 0.0), (jpy, 0.0), (aud, 0.0));
        var trajectories = new[]
        {
            Trajectory(usd, geopoliticalDelta: 1.5),
            Trajectory(jpy, geopoliticalDelta: 1.2),
            Trajectory(aud, geopoliticalDelta: -1.5)
        };

        var (forecasts, _) = ForwardOutlookCalculator.Project(current, trajectories, _weights, Horizon.TwelveMonths, AsOf);

        forecasts[0].Currency.Should().BeOneOf(usd, jpy);
        forecasts[^1].Currency.Should().Be(aud);
    }

    [Fact]
    public void A_pegged_currency_pair_clamps_toward_neutral_and_is_flagged()
    {
        var hkd = Pegged("HKD", "USD");
        var jpy = Major("JPY");
        var current = Ranking((hkd, 1.0), (jpy, 0.0));
        var trajectories = new[]
        {
            Trajectory(hkd, ratePathBp: 200),
            Trajectory(jpy, ratePathBp: -200)
        };

        var (_, matrix) = ForwardOutlookCalculator.Project(current, trajectories, _weights, Horizon.TwelveMonths, AsOf);

        var pair = matrix.For(hkd, jpy)!;
        pair.Pegged.Should().BeTrue();
        pair.Confidence.Should().Be(DataConfidence.Low);
        pair.Conviction.Should().BeLessThan(0.6, "a peg is not read as a free-floating signal");
    }
}
