using Core.Calendar;
using Core.Options;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace UnitTests.Calendar;

public sealed class CalendarEnablementTests
{
    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public void Effective_state_is_the_and_of_both_tiers(bool branding, bool toggle, bool expected)
    {
        CalendarEnablement.IsEnabled(branding, toggle).Should().Be(expected);
    }

    [Fact]
    public void Runtime_toggle_hidden_when_white_label_hard_gate_is_off()
    {
        CalendarEnablement.IsRuntimeToggleVisible(new BrandingOptions { EnableEconomicCalendar = false })
            .Should().BeFalse();
        CalendarEnablement.IsRuntimeToggleVisible(new BrandingOptions { EnableEconomicCalendar = true })
            .Should().BeTrue();
    }

    [Fact]
    public void Default_branding_enables_the_calendar()
    {
        new BrandingOptions().EnableEconomicCalendar.Should().BeTrue();
    }

    [Fact]
    public void Source_is_configured_only_when_a_value_key_is_present()
    {
        CalendarEnablement.HasConfiguredSource(new CalendarOptions()).Should().BeFalse();
        CalendarEnablement.HasConfiguredSource(new CalendarOptions { FredApiKey = "k" }).Should().BeTrue();
        CalendarEnablement.HasConfiguredSource(new CalendarOptions { BlsApiKey = "k" }).Should().BeTrue();
        CalendarEnablement.HasConfiguredSource(new CalendarOptions { FredApiKey = "   " }).Should().BeFalse();
    }

    [Fact]
    public void Visible_requires_both_enablement_and_a_configured_source()
    {
        var branding = new BrandingOptions { EnableEconomicCalendar = true };
        CalendarEnablement.IsVisible(branding, Gate(true), new CalendarOptions())
            .Should().BeFalse("enabled but source-less stays hidden");
        CalendarEnablement.IsVisible(branding, Gate(true), new CalendarOptions { FredApiKey = "k" })
            .Should().BeTrue();
        CalendarEnablement.IsVisible(branding, Gate(false), new CalendarOptions { FredApiKey = "k" })
            .Should().BeFalse("feature toggle off ⇒ hidden");
    }

    private static Core.Features.IFeatureGate Gate(bool on)
    {
        var gate = NSubstitute.Substitute.For<Core.Features.IFeatureGate>();
        gate.IsEnabled(NSubstitute.Arg.Any<Core.Features.FeatureFlag>()).Returns(on);
        return gate;
    }
}
