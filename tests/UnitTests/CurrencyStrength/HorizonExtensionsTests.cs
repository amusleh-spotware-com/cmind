using Core.Ai.CurrencyStrength;
using Core.Constants;
using Core.Domain;
using FluentAssertions;
using Xunit;

namespace UnitTests.CurrencyStrength;

// The forward-horizon scale/label/parse helpers. (WS-1 Core backfill.)
public class HorizonExtensionsTests
{
    [Theory]
    [InlineData(Horizon.OneMonth, 0.25, "1M")]
    [InlineData(Horizon.ThreeMonths, 0.50, "3M")]
    [InlineData(Horizon.SixMonths, 0.75, "6M")]
    [InlineData(Horizon.TwelveMonths, 1.00, "12M")]
    public void Scale_and_label_are_defined_for_every_horizon(Horizon horizon, double scale, string label)
    {
        horizon.Scale().Should().Be(scale);
        horizon.Label().Should().Be(label);
        HorizonExtensions.Parse(label).Should().Be(horizon, "label parses back to the same horizon");
    }

    [Fact]
    public void Parse_defaults_null_to_three_months_and_rejects_unknown()
    {
        HorizonExtensions.Parse(null).Should().Be(Horizon.ThreeMonths);
        HorizonExtensions.Parse(" 6m ").Should().Be(Horizon.SixMonths, "parsing trims and upper-cases");

        var bad = () => HorizonExtensions.Parse("99Y");
        bad.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.CurrencyHorizonUnknown);
    }
}
