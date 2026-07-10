using Core.Features;
using Core.Options;
using FluentAssertions;
using Xunit;

namespace UnitTests.Features;

public class FeaturesOptionsTests
{
    [Fact]
    public void All_features_default_to_enabled()
    {
        var options = new FeaturesOptions();

        foreach (var flag in Enum.GetValues<FeatureFlag>())
            options.IsEnabled(flag).Should().BeTrue($"{flag} must default to enabled");
    }

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
