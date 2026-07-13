using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Options;
using Xunit;

namespace IntegrationTests;

public class BrandingHttpTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private WebApplicationFactory<Program> CreateApp(IReadOnlyDictionary<string, string?> branding) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development");
            b.UseSetting("ConnectionStrings:appdb", fixture.Container.GetConnectionString());
            b.UseSetting("App:OwnerEmail", "owner@brand.local");
            b.UseSetting("App:OwnerPassword", "Owner_Pass_123!");
            foreach (var (key, value) in branding) b.UseSetting(key, value);
        });

    [Fact]
    public async Task Custom_branding_renders_in_page_head()
    {
        await using var app = CreateApp(new Dictionary<string, string?>
        {
            ["App:Branding:ProductName"] = "AcmeFX",
            ["App:Branding:Description"] = "AcmeFX copy trading",
            ["App:Branding:BackgroundColor"] = "#0A0A0A"
        });
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var html = await (await client.GetAsync("/login")).Content.ReadAsStringAsync();

        html.Should().Contain("<title>AcmeFX</title>");
        html.Should().Contain("AcmeFX copy trading");
        // The custom background colour is surfaced in the head as the --app-bg CSS variable (the
        // theme-color meta deliberately tracks the app-bar colour, not the page background).
        html.Should().Contain("--app-bg:#0A0A0A");
    }

    [Fact]
    public async Task Default_branding_keeps_stock_product_name()
    {
        await using var app = CreateApp(new Dictionary<string, string?>());
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var html = await (await client.GetAsync("/login")).Content.ReadAsStringAsync();

        html.Should().Contain("<title>cMind</title>");
    }

    [Fact]
    public void Invalid_branding_colour_fails_startup()
    {
        using var app = CreateApp(new Dictionary<string, string?> { ["App:Branding:PrimaryColor"] = "nope" });
        var act = () => app.CreateClient();
        act.Should().Throw<OptionsValidationException>();
    }
}
