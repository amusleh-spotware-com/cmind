using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

// The cMind Identity System: the new symbol (c-arc + rising candlesticks + AI nodes) is the favicon, the
// login + app-bar brand mark, and the PWA icons.
[Collection(AppCollection.Name)]
public sealed class BrandIdentityTests(AppFixture app)
{
    [Fact]
    public async Task Favicon_is_the_new_mark()
    {
        var page = await app.NewAnonymousPageAsync();
        var response = await page.GotoAsync("/favicon.svg");
        response!.Status.Should().Be(200);
        var body = await response.TextAsync();
        // Assert brand-stable traits (a mint SVG mark), not an exact arc — so a logo tweak that keeps the
        // brand doesn't break this. The app↔docs favicon byte-identity is enforced by
        // ArchitectureGuardTests.App_and_docs_favicon_are_byte_identical.
        body.Should().Contain("<svg", "the favicon is an SVG mark");
        body.Should().Contain("#26C281", "mint accent");
        body.Should().NotContain("A18 18", "the previous simple c-arc mark must be gone");
        body.Should().NotContain("36 30", "the old robot-head rect must be gone");
    }

    [Fact]
    public async Task Login_shows_the_brand_mark()
    {
        var page = await app.NewAnonymousPageAsync();
        await page.GotoAsync("/login", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        (await page.Locator(".app-brand-mark").First.IsVisibleAsync())
            .Should().BeTrue("the login hero shows the cMind symbol");
    }

    [Fact]
    public async Task App_bar_shows_the_brand_mark()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        (await page.Locator(".mud-appbar .app-brand-mark").First.IsVisibleAsync())
            .Should().BeTrue("the app bar shows the cMind symbol next to the product name");
    }

    [Theory]
    [InlineData("/icons/icon-192.png")]
    [InlineData("/icons/icon-512.png")]
    [InlineData("/icons/icon-512-maskable.png")]
    [InlineData("/icons/apple-touch-icon.png")]
    [InlineData("/branding/logo-symbol.svg")]
    public async Task Brand_assets_are_served(string path)
    {
        var page = await app.NewAnonymousPageAsync();
        var response = await page.GotoAsync(path);
        response!.Status.Should().Be(200, $"{path} must be served for the PWA/brand");
    }
}
