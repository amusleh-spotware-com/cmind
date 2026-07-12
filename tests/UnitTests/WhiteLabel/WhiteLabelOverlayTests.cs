using Core.Nodes;
using Core.Options;
using Core.WhiteLabel;
using FluentAssertions;
using Xunit;

namespace UnitTests.WhiteLabel;

public class WhiteLabelOverlayTests
{
    private static WhiteLabelOption Opt(string key) =>
        WhiteLabelCatalog.All.Single(o => o.Key == key);

    [Fact]
    public void Apply_with_no_overrides_returns_baseline_unchanged()
    {
        var baseline = new AppOptions();
        WhiteLabelOverlay.Apply(baseline, new Dictionary<string, string>())
            .Should().BeSameAs(baseline);
    }

    [Fact]
    public void String_override_applies()
    {
        var result = WhiteLabelOverlay.Apply(new AppOptions(),
            new Dictionary<string, string> { ["branding.productName"] = "Acme Trade" });
        result.Branding.ProductName.Should().Be("Acme Trade");
    }

    [Fact]
    public void Bool_override_applies()
    {
        var result = WhiteLabelOverlay.Apply(new AppOptions(),
            new Dictionary<string, string> { ["branding.requireMfa"] = "True" });
        result.Branding.RequireMfa.Should().BeTrue();
    }

    [Fact]
    public void Enum_override_applies_and_bad_value_keeps_baseline()
    {
        var good = WhiteLabelOverlay.Apply(new AppOptions(),
            new Dictionary<string, string> { ["branding.nodesUi"] = "Hidden" });
        good.Branding.NodesUi.Should().Be(NodesUiMode.Hidden);

        var bad = WhiteLabelOverlay.Apply(new AppOptions(),
            new Dictionary<string, string> { ["branding.nodesUi"] = "Nonsense" });
        bad.Branding.NodesUi.Should().Be(NodesUiMode.Full);
    }

    [Fact]
    public void List_int_number_timespan_overrides_apply()
    {
        var result = WhiteLabelOverlay.Apply(new AppOptions(), new Dictionary<string, string>
        {
            ["accounts.allowedBrokers"] = "Pepperstone, IC Markets",
            ["email.port"] = "2525",
            ["propFirm.drawdownWarnThresholdPercent"] = "65",
            ["accounts.brokerProbeTimeout"] = "00:02:00"
        });

        result.Accounts.AllowedBrokers.Should().Equal("Pepperstone", "IC Markets");
        result.Email.Port.Should().Be(2525);
        result.PropFirm.DrawdownWarnThresholdPercent.Should().Be(65);
        result.Accounts.BrokerProbeTimeout.Should().Be(TimeSpan.FromMinutes(2));
    }

    [Fact]
    public void Nested_registration_overrides_apply()
    {
        var result = WhiteLabelOverlay.Apply(new AppOptions(), new Dictionary<string, string>
        {
            ["registration.enabled"] = "True",
            ["registration.mode"] = "Open",
            ["registration.captcha.enabled"] = "True",
            ["registration.attributes.country"] = "Required"
        });

        result.Registration.Enabled.Should().BeTrue();
        result.Registration.Mode.Should().Be(RegistrationMode.Open);
        result.Registration.Captcha.Enabled.Should().BeTrue();
        result.Registration.Attributes.Country.Should().Be(AttributePolicy.Required);
    }

    [Fact]
    public void ReadRaw_formats_each_kind()
    {
        var options = new AppOptions();
        WhiteLabelOverlay.ReadRaw(options, Opt("branding.requireMfa")).Should().Be("False");
        WhiteLabelOverlay.ReadRaw(options, Opt("branding.nodesUi")).Should().Be("Full");
        WhiteLabelOverlay.ReadRaw(options, Opt("branding.productName")).Should().Be("cMind");
        WhiteLabelOverlay.ReadRaw(options, Opt("accounts.allowedBrokers")).Should().BeEmpty();
    }
}
