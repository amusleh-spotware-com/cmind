using Core.Options;
using FluentAssertions;
using Web.Branding;
using Xunit;

namespace IntegrationTests;

public class BrandingOptionsValidatorTests
{
    private static bool Validate(BrandingOptions branding) =>
        new BrandingOptionsValidator().Validate(null, new AppOptions { Branding = branding }).Succeeded;

    [Fact]
    public void Default_branding_is_valid() => Validate(new BrandingOptions()).Should().BeTrue();

    [Fact]
    public void Invalid_colour_fails() =>
        Validate(new BrandingOptions { PrimaryColor = "not-a-color" }).Should().BeFalse();

    [Fact]
    public void CustomCss_with_angle_bracket_fails() =>
        Validate(new BrandingOptions { CustomCss = "</style><script>alert(1)</script>" }).Should().BeFalse();

    [Fact]
    public void Plain_custom_css_is_valid() =>
        Validate(new BrandingOptions { CustomCss = ".mud-appbar { letter-spacing: 1px; }" }).Should().BeTrue();
}
