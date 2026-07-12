using Core.Features;
using Core.Options;
using FluentAssertions;
using Xunit;

namespace UnitTests.Features;

public class FeaturesOptionsTests
{
    [Fact]
    public void Product_features_default_to_enabled_except_registration()
    {
        var options = new FeaturesOptions();

        foreach (var flag in Enum.GetValues<FeatureFlag>())
        {
            // Self-service registration is the one feature that ships OFF — a deployment opts in explicitly.
            var expected = flag != FeatureFlag.Registration;
            options.IsEnabled(flag).Should().Be(expected, $"{flag} default");
        }
    }

    [Fact]
    public void Registration_defaults_to_disabled()
        => new FeaturesOptions().IsEnabled(FeatureFlag.Registration).Should().BeFalse();

    [Fact]
    public void IsEnabled_reflects_each_disabled_flag_independently()
    {
        new FeaturesOptions { CopyTrading = false }.IsEnabled(FeatureFlag.CopyTrading).Should().BeFalse();
        new FeaturesOptions { CopyTrading = false }.IsEnabled(FeatureFlag.Ai).Should().BeTrue();
        new FeaturesOptions { Ai = false }.IsEnabled(FeatureFlag.Ai).Should().BeFalse();
        new FeaturesOptions { Mcp = false }.IsEnabled(FeatureFlag.Mcp).Should().BeFalse();
    }

    [Fact]
    public void Every_flag_value_is_mapped()
    {
        var options = new FeaturesOptions();

        var act = () =>
        {
            foreach (var flag in Enum.GetValues<FeatureFlag>())
                _ = options.IsEnabled(flag);
        };

        act.Should().NotThrow("IsEnabled must handle every FeatureFlag value");
    }
}
