using Core.Constants;
using Core.Domain;
using Core.Options;
using FluentAssertions;
using Xunit;

namespace IntegrationTests;

public class ThemeBuildTests
{
    [Fact]
    public void Build_maps_branding_colours_into_palette()
    {
        var theme = Web.Components.Theme.Build(new BrandingOptions
        {
            PrimaryColor = "#101010",
            BackgroundColor = "#202020",
            ErrorColor = "#303030"
        });

        theme.PaletteDark.Primary.Value.Should().StartWith("#101010");
        theme.PaletteDark.Background.Value.Should().StartWith("#202020");
        theme.PaletteDark.Error.Value.Should().StartWith("#303030");
    }

    [Fact]
    public void Build_rejects_invalid_colour()
    {
        var act = () => Web.Components.Theme.Build(new BrandingOptions { PrimaryColor = "not-a-color" });
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.BrandingColorInvalid);
    }
}
