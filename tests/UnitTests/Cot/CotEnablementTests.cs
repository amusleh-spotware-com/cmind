using Core.Cot;
using Core.Features;
using Core.Options;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace UnitTests.Cot;

public class CotEnablementTests
{
    private static IFeatureGate Gate(bool enabled)
    {
        var gate = Substitute.For<IFeatureGate>();
        gate.IsEnabled(Arg.Any<FeatureFlag>()).Returns(enabled);
        return gate;
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public void Two_tier_gate_requires_both(bool branding, bool toggle, bool expected)
    {
        CotEnablement.IsEnabled(branding, toggle).Should().Be(expected);
        var options = new BrandingOptions { EnableCot = branding };
        CotEnablement.IsEnabled(options, Gate(toggle)).Should().Be(expected);
        CotEnablement.IsVisible(options, Gate(toggle)).Should().Be(expected);
    }

    [Fact]
    public void Runtime_toggle_visible_only_when_branding_allows()
    {
        CotEnablement.IsRuntimeToggleVisible(new BrandingOptions { EnableCot = true }).Should().BeTrue();
        CotEnablement.IsRuntimeToggleVisible(new BrandingOptions { EnableCot = false }).Should().BeFalse();
    }
}
