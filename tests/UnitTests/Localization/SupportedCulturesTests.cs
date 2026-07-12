using Core.Constants;
using FluentAssertions;
using Xunit;

namespace UnitTests.Localization;

public class SupportedCulturesTests
{
    [Fact]
    public void English_is_the_default_and_first_in_the_list()
    {
        SupportedCultures.Default.Should().Be("en");
        SupportedCultures.All[0].Should().Be("en");
    }

    [Fact]
    public void Ships_all_twenty_three_ctrader_languages_with_no_duplicates()
    {
        SupportedCultures.All.Should().HaveCount(23);
        SupportedCultures.All.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Every_culture_has_a_native_name()
    {
        foreach (var culture in SupportedCultures.All)
            SupportedCultures.NativeNames.Should().ContainKey(culture)
                .WhoseValue.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData("ar")]
    [InlineData("ar-SA")]
    public void Arabic_is_right_to_left(string culture) =>
        SupportedCultures.IsRightToLeft(culture).Should().BeTrue();

    [Theory]
    [InlineData("en")]
    [InlineData("de")]
    [InlineData("zh-Hans")]
    [InlineData("ja")]
    [InlineData(null)]
    [InlineData("")]
    public void Non_rtl_cultures_are_left_to_right(string? culture) =>
        SupportedCultures.IsRightToLeft(culture).Should().BeFalse();

    [Fact]
    public void Direction_maps_rtl_and_ltr()
    {
        SupportedCultures.Direction("ar").Should().Be("rtl");
        SupportedCultures.Direction("en").Should().Be("ltr");
    }

    [Theory]
    [InlineData("en", true)]
    [InlineData("pt-BR", true)]
    [InlineData("zh-Hans", true)]
    [InlineData("xx", false)]
    [InlineData("en-US", false)]
    [InlineData(null, false)]
    public void IsSupported_matches_only_exact_supported_cultures(string? culture, bool expected) =>
        SupportedCultures.IsSupported(culture).Should().Be(expected);
}
