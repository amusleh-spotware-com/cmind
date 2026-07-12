using Core.Ai.CurrencyStrength;
using FluentAssertions;
using Xunit;
using static UnitTests.Ai.CurrencyStrength.CurrencyTestData;

namespace UnitTests.Ai.CurrencyStrength;

public sealed class CurrencyStrengthCalculatorTests
{
    private readonly StrengthWeights _weights = StrengthWeights.Default();

    [Fact]
    public void Hawkish_hiker_outranks_dovish_cutter()
    {
        var usd = Major("USD");
        var jpy = Major("JPY");
        var panel = new[]
        {
            Indicators(usd, policyRate: 5.0, stance: PolicyStance.Hawkish),
            Indicators(jpy, policyRate: 0.5, stance: PolicyStance.Dovish)
        };

        var ranking = CurrencyStrengthCalculator.Compute(panel, _weights, AsOf);

        ranking.Rank(usd).Should().Be(1);
        ranking.Rank(jpy).Should().Be(2);
        ranking.Strongest.Currency.Should().Be(usd);
        ranking.AsOf.Should().Be(AsOf);
    }

    [Fact]
    public void Inflation_is_scored_inversely()
    {
        var low = Major("USD");
        var high = Major("GBP");
        var panel = new[]
        {
            Indicators(low, cpi: 2.0),
            Indicators(high, cpi: 8.0)
        };

        var ranking = CurrencyStrengthCalculator.Compute(panel, _weights, AsOf);

        ranking.Rank(low).Should().Be(1, "above-target inflation is a purchasing-power drag");
    }

    [Fact]
    public void Ties_break_deterministically_by_iso_code()
    {
        var eur = Major("EUR");
        var aud = Major("AUD");
        var panel = new[] { Indicators(aud), Indicators(eur) };

        var ranking = CurrencyStrengthCalculator.Compute(panel, _weights, AsOf);

        ranking.Scores[0].Currency.Code.Should().Be("AUD");
        ranking.Scores[1].Currency.Code.Should().Be("EUR");
    }

    [Fact]
    public void Winsorization_bounds_a_driver_z_score()
    {
        var panel = new[]
        {
            Indicators(Major("USD"), policyRate: 2.0),
            Indicators(Major("EUR"), policyRate: 2.1),
            Indicators(Major("GBP"), policyRate: 2.2),
            Indicators(Major("JPY"), policyRate: 200.0)
        };

        var ranking = CurrencyStrengthCalculator.Compute(panel, _weights, AsOf);

        ranking.Scores.SelectMany(s => s.Breakdown)
            .Should().OnlyContain(d => Math.Abs(d.Normalized) <= CurrencyStrengthCalculator.WinsorLimit + 1e-9);
    }

    [Fact]
    public void An_extreme_exotic_does_not_distort_the_majors_ranking()
    {
        var usd = Major("USD");
        var eur = Major("EUR");
        var gbp = Major("GBP");
        var majors = new[]
        {
            Indicators(usd, policyRate: 5.0),
            Indicators(eur, policyRate: 3.0),
            Indicators(gbp, policyRate: 4.0)
        };

        var majorsOnly = CurrencyStrengthCalculator.Compute(majors, _weights, AsOf);
        var withExotic = CurrencyStrengthCalculator.Compute(
            [.. majors, Indicators(Exotic("TRY"), policyRate: 50.0, cpi: 60.0, confidence: DataConfidence.Low)],
            _weights, AsOf);

        foreach (var code in new[] { "USD", "EUR", "GBP" })
        {
            var a = majorsOnly.Scores.Single(s => s.Currency.Code == code).Composite;
            var b = withExotic.Scores.Single(s => s.Currency.Code == code).Composite;
            b.Should().BeApproximately(a, 1e-9, "within-tier normalization isolates the majors from an exotic outlier");
        }
    }

    [Fact]
    public void Em_weighting_lifts_carry_between_two_emerging_currencies()
    {
        var highCarry = Em("MXN");
        var lowCarry = Em("ZAR");
        var panel = new[]
        {
            Indicators(highCarry, realYield: 6.0),
            Indicators(lowCarry, realYield: 0.5)
        };

        var ranking = CurrencyStrengthCalculator.Compute(panel, _weights, AsOf);

        ranking.Rank(highCarry).Should().Be(1);
    }

    [Fact]
    public void A_mixed_universe_ranks_deterministically_across_runs()
    {
        CurrencyIndicators[] Panel() =>
        [
            Indicators(Major("USD"), policyRate: 5.0, stance: PolicyStance.Hawkish),
            Indicators(Major("EUR"), policyRate: 3.0),
            Indicators(Em("MXN"), realYield: 6.0),
            Indicators(Exotic("TRY"), cpi: 60.0, realYield: -10.0, politicalRisk: 8.0, confidence: DataConfidence.Low)
        ];

        var first = CurrencyStrengthCalculator.Compute(Panel(), _weights, AsOf);
        var second = CurrencyStrengthCalculator.Compute(Panel(), _weights, AsOf);

        first.Scores.Select(s => s.Currency.Code)
            .Should().Equal(second.Scores.Select(s => s.Currency.Code));
    }
}
