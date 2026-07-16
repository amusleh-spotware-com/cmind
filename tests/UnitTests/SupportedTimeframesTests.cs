using Core;
using Core.Constants;
using FluentAssertions;
using Xunit;

namespace UnitTests;

// The timeframe dropdowns are populated from Core.Constants.Timeframes.Supported, which must mirror the
// cTrader Console `periods` command output in its CANONICAL casing (m1/h1 lowercase, D1/W1/Month1 and the
// Renko/Range/Heikin periods capitalised). The Timeframe value object must preserve that casing (the CLI
// period names are case-sensitive) — lowercasing "D1" would produce an invalid period.
public class SupportedTimeframesTests
{
    [Fact]
    public void Supported_list_covers_every_period_family_in_canonical_casing()
    {
        var supported = Timeframes.Supported;

        supported.Should().OnlyHaveUniqueItems();
        // The standard time-based families.
        supported.Should().Contain(["t1", "m1", "m5", "m15", "m30", "h1", "h4", "h12", "D1", "W1", "Month1"]);
        // The specialised chart-period families cTrader also supports.
        supported.Should().Contain(["Re1", "Ra1", "Hm1", "Hh1", "Hd1", "Hw1", "HMonth1"]);
        // Casing is canonical — daily/weekly/monthly are NOT lowercased.
        supported.Should().NotContain("d1").And.NotContain("w1").And.NotContain("month1");
        // The default is a valid member.
        supported.Should().Contain(Timeframes.Default);
    }

    [Theory]
    [InlineData("D1", "D1")]      // canonical uppercase preserved
    [InlineData("Month1", "Month1")]
    [InlineData(" h1 ", "h1")]    // trimmed, not altered
    [InlineData("m5", "m5")]
    public void Timeframe_preserves_canonical_casing(string input, string expected)
    {
        new Timeframe(input).Value.Should().Be(expected);
    }
}
