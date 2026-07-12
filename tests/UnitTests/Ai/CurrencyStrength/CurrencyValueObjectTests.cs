using Core.Ai.CurrencyStrength;
using Core.Constants;
using Core.Domain;
using FluentAssertions;
using Xunit;
using static UnitTests.Ai.CurrencyStrength.CurrencyTestData;

namespace UnitTests.Ai.CurrencyStrength;

public sealed class CurrencyValueObjectTests
{
    [Theory]
    [InlineData("usd")]
    [InlineData("EUR")]
    [InlineData("cnh")]
    public void Currency_accepts_and_uppercases_a_valid_iso_code(string code)
    {
        var currency = new Currency(code, CurrencyTier.Major);
        currency.Code.Should().Be(code.ToUpperInvariant());
    }

    [Theory]
    [InlineData("US")]
    [InlineData("USDD")]
    [InlineData("")]
    [InlineData("   ")]
    public void Currency_rejects_a_malformed_code(string code)
    {
        var act = () => new Currency(code, CurrencyTier.Major);
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.CalendarCurrencyCodeInvalid);
    }

    [Fact]
    public void Currency_carries_tier_and_peg_metadata()
    {
        var hkd = Pegged("HKD", "USD");
        hkd.Tier.Should().Be(CurrencyTier.Exotic);
        hkd.IsPegged.Should().BeTrue();
        hkd.PegAnchor.Should().Be("USD");
    }

    [Fact]
    public void CurrencyUniverse_resolves_a_configured_code_and_rejects_an_unknown_one()
    {
        var universe = CurrencyUniverse.Of([Major("USD"), Em("CNH"), Exotic("TRY")]);

        universe.Contains("cnh").Should().BeTrue();
        universe.Resolve("try").Tier.Should().Be(CurrencyTier.Exotic);
        universe.OfTier(CurrencyTier.Major).Should().ContainSingle(c => c.Code == "USD");

        var act = () => universe.Resolve("JPY");
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.CurrencyNotInUniverse);
    }

    [Fact]
    public void CurrencyUniverse_rejects_empty_and_duplicate()
    {
        var empty = () => CurrencyUniverse.Of([]);
        empty.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.CurrencyUniverseEmpty);

        var dup = () => CurrencyUniverse.Of([Major("USD"), Major("USD")]);
        dup.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.CurrencyUniverseDuplicate);
    }

    [Fact]
    public void Indicators_reject_a_non_finite_value()
    {
        var act = () => Indicators(Major("USD"), policyRate: double.NaN);
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.CurrencyIndicatorOutOfRange);
    }

    [Fact]
    public void Indicators_accept_an_exotic_hyperinflation_edge_case()
    {
        var act = () => Indicators(Exotic("TRY"), cpi: 80.0, confidence: DataConfidence.Low);
        act.Should().NotThrow();
    }

    [Fact]
    public void Trajectory_rejects_an_out_of_range_rate_path()
    {
        var act = () => Trajectory(Major("USD"), ratePathBp: 999_999);
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.CurrencyTrajectoryOutOfRange);
    }

    [Fact]
    public void Horizon_scale_is_monotonic_and_labels_round_trip()
    {
        Horizon.OneMonth.Scale().Should().BeLessThan(Horizon.TwelveMonths.Scale());
        HorizonExtensions.Parse("6M").Should().Be(Horizon.SixMonths);
        Horizon.ThreeMonths.Label().Should().Be("3M");
    }

    [Fact]
    public void Weights_reject_a_non_normalized_tier()
    {
        var act = () => StrengthWeights.Create(new Dictionary<CurrencyTier, IReadOnlyDictionary<MacroDriver, double>>
        {
            [CurrencyTier.Major] = new Dictionary<MacroDriver, double> { [MacroDriver.PolicyRate] = 0.7 }
        });
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.CurrencyWeightsNotNormalized);
    }

    [Fact]
    public void Default_weights_are_normalized_for_every_tier()
    {
        var act = () => { _ = StrengthWeights.Default(); _ = ForwardWeights.Default(); };
        act.Should().NotThrow();
    }
}
