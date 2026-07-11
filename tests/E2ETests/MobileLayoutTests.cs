using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

// Drives the app on real mobile-device emulation (viewport + touch + DPR + UA) and asserts the
// mobile-first shell: bottom navigation is present, the page renders without the Blazor error UI,
// and shell-simple pages do not scroll sideways. Table-heavy pages are added here as they are
// converted to the responsive card layout (Phase 2+).
[Collection(AppCollection.Name)]
public sealed class MobileLayoutTests(AppFixture app)
{
    public static IEnumerable<object[]> MobileRoutes() => new[]
    {
        "/", "/cbots", "/run", "/backtest", "/accounts", "/copy-trading",
        "/assistant", "/nodes", "/users", "/account", "/settings/features",
    }.Select(r => new object[] { r });

    // Shell-simple pages that must never scroll sideways on a phone right now. Grows every phase.
    public static IEnumerable<object[]> NoOverflowRoutes() => new[]
    {
        "/account", "/settings/features", "/nodes", "/users", "/cbots", "/run", "/backtest",
        "/mcp", "/prop-firm", "/settings/legal", "/copy-trading",
    }.Select(r => new object[] { r });

    [Theory]
    [MemberData(nameof(MobileRoutes))]
    public async Task Mobile_shell_renders_with_bottom_nav(string route)
    {
        var page = await app.NewAuthedMobilePageAsync();
        var response = await page.GotoAsync(route, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        response.Should().NotBeNull();
        response!.Status.Should().BeLessThan(500, $"GET {route} returned {response.Status} on mobile");

        (await page.Locator(".blazor-error-ui").IsVisibleAsync())
            .Should().BeFalse($"{route} tripped the Blazor error UI on mobile");

        await page.Locator("[data-testid=bottom-nav]").WaitForAsync(new() { State = WaitForSelectorState.Visible });
        (await page.Locator("[data-testid=bottom-nav]").IsVisibleAsync())
            .Should().BeTrue($"{route} did not show the mobile bottom navigation");
    }

    [Theory]
    [MemberData(nameof(NoOverflowRoutes))]
    public async Task Mobile_page_has_no_horizontal_overflow(string route)
    {
        var page = await app.NewAuthedMobilePageAsync();
        await page.GotoAsync(route, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        var fits = await page.EvaluateAsync<bool>(
            "() => document.documentElement.scrollWidth <= window.innerWidth + 1");
        fits.Should().BeTrue($"{route} scrolls horizontally on a phone (content wider than viewport)");
    }

    [Fact]
    public async Task Bottom_nav_hidden_on_desktop()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        // The element exists in the DOM but CSS hides it at the tablet+ breakpoint.
        (await page.Locator("[data-testid=bottom-nav]").IsVisibleAsync())
            .Should().BeFalse("bottom navigation must be mobile-only");
    }
}
