using Core.Calendar;
using Core.Options;
using FluentAssertions;
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
}
