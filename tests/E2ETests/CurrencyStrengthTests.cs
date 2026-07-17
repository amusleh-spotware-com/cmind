using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

// Drives the AI macro currency-strength UI through the real browser: the opt-in dashboard widget (hidden by
// default, enabled from the user's own dashboard settings) and the full page. The base fixture has no AI key,
// so the page shows the keyless gate and an empty state — exactly the degraded path a user first sees.
[Collection(AppCollection.Name)]
public sealed class CurrencyStrengthTests(AppFixture app)
{
    private static async Task WaitDashboardAsync(IPage page)
    {
        await page.GotoAsync("/", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.Locator("[data-testid=kpi-active]").WaitForAsync(new() { Timeout = 15000 });
        await page.Locator("[data-testid=widget-backtests]").WaitForAsync(new() { Timeout = 15000 });
    }

    [Fact]
    public async Task Widget_is_hidden_by_default_and_can_be_enabled_from_settings()
    {
        var page = await app.NewAuthedPageAsync();
        await WaitDashboardAsync(page);

        // Hidden by default — the opt-in widget is not seeded onto the board.
        (await page.Locator("[data-testid=widget-currency-strength]").CountAsync())
            .Should().Be(0, "the currency-strength widget is opt-in and hidden by default");

        // Enable it from the user's own dashboard customize surface.
        await page.Locator("[data-testid=customize-dashboard]").ClickAsync();
        await page.Locator("[data-testid=customize-list]").WaitForAsync(new() { Timeout = 10000 });
        await page.Locator("[data-testid=toggle-currency-strength]").ClickAsync();
        await page.Locator("[data-testid=customize-save]").ClickAsync();

        // It now renders on the dashboard.
        await page.Locator("[data-testid=widget-currency-strength]").WaitForAsync(
            new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
        (await page.Locator("[data-testid=dash-currency-strength]").IsVisibleAsync()).Should().BeTrue();
        (await page.Locator("[data-testid=page-error]").IsVisibleAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Full_page_renders_with_controls_and_empty_state()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/ai/currency-strength", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForAppReadyAsync();

        // Controls render and the page does not crash.
        await page.Locator("[data-testid=cs-refresh]").WaitForAsync(new() { Timeout = 15000 });
        (await page.Locator("text=Macro Currency Strength").IsVisibleAsync()).Should().BeTrue();
        (await page.Locator(".blazor-error-ui").IsVisibleAsync()).Should().BeFalse();
        (await page.Locator("[data-testid=page-error]").IsVisibleAsync()).Should().BeFalse();

        // No AI key on the base fixture ⇒ the keyless gate notice is shown.
        (await page.Locator("[data-testid=ai-not-configured]").IsVisibleAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task Full_page_renders_on_mobile_without_horizontal_scroll()
    {
        var page = await app.NewAuthedMobilePageAsync();
        await page.GotoAsync("/ai/currency-strength", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.Locator("[data-testid=cs-refresh]").WaitForAsync(new() { Timeout = 15000 });

        var fits = await page.EvaluateAsync<bool>(
            "() => document.documentElement.scrollWidth <= window.innerWidth + 1");
        fits.Should().BeTrue("the currency-strength page must not scroll sideways on a phone");
    }
}
