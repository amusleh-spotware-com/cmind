using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

// Drives the customizable dashboard: open the Customize dialog, hide a widget, save, and verify the
// widget disappears and the choice survives a full page reload (persisted server-side per user).
[Collection(AppCollection.Name)]
public sealed class DashboardCustomizeTests(AppFixture app)
{
    private static async Task WaitLoadedAsync(IPage page)
    {
        await page.GotoAsync("/", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.Locator("[data-testid=kpi-active]").WaitForAsync(new() { Timeout = 15000 });
        // The layout fetch mounts the widget grid after the overview.
        await page.Locator("[data-testid=widget-backtests]").WaitForAsync(new() { Timeout = 15000 });
    }

    [Fact]
    public async Task Hiding_a_widget_persists_across_a_reload()
    {
        var page = await app.NewAuthedPageAsync();
        await WaitLoadedAsync(page);

        await page.Locator("[data-testid=customize-dashboard]").ClickAsync();
        await page.Locator("[data-testid=customize-list]").WaitForAsync(new() { Timeout = 10000 });

        // Turn the backtests widget off, then save.
        await page.Locator("[data-testid=toggle-backtests]").ClickAsync();
        await page.Locator("[data-testid=customize-save]").ClickAsync();

        // Widget is gone immediately after save.
        await page.Locator("[data-testid=widget-backtests]").WaitForAsync(
            new() { State = WaitForSelectorState.Hidden, Timeout = 10000 });

        // And still gone after a full reload — the choice was persisted to the account.
        await page.GotoAsync("/", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.Locator("[data-testid=kpi-active]").WaitForAsync(new() { Timeout = 15000 });
        (await page.Locator("[data-testid=widget-backtests]").CountAsync())
            .Should().Be(0, "the hidden widget must not come back after reload");

        (await page.Locator("[data-testid=page-error]").IsVisibleAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Reset_to_default_brings_a_hidden_widget_back()
    {
        var page = await app.NewAuthedPageAsync();
        await WaitLoadedAsync(page);

        // Hide it first.
        await page.Locator("[data-testid=customize-dashboard]").ClickAsync();
        await page.Locator("[data-testid=customize-list]").WaitForAsync(new() { Timeout = 10000 });
        await page.Locator("[data-testid=toggle-copy-profiles]").ClickAsync();
        await page.Locator("[data-testid=customize-save]").ClickAsync();
        await page.Locator("[data-testid=widget-copy-profiles]").WaitForAsync(
            new() { State = WaitForSelectorState.Hidden, Timeout = 10000 });

        // Reset restores every default widget.
        await page.Locator("[data-testid=customize-dashboard]").ClickAsync();
        await page.Locator("[data-testid=customize-list]").WaitForAsync(new() { Timeout = 10000 });
        await page.Locator("[data-testid=customize-reset]").ClickAsync();

        await page.Locator("[data-testid=widget-copy-profiles]").WaitForAsync(
            new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
        (await page.Locator("[data-testid=page-error]").IsVisibleAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Customize_dashboard_works_on_mobile()
    {
        var page = await app.NewAuthedMobilePageAsync();
        await page.GotoAsync("/", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.Locator("[data-testid=kpi-active]").WaitForAsync(new() { Timeout = 15000 });

        await page.Locator("[data-testid=customize-dashboard]").ClickAsync();
        await page.Locator("[data-testid=customize-list]").WaitForAsync(new() { Timeout = 10000 });
        (await page.Locator("[data-testid=customize-save]").IsVisibleAsync()).Should().BeTrue();

        var fits = await page.EvaluateAsync<bool>(
            "() => document.documentElement.scrollWidth <= window.innerWidth + 1");
        fits.Should().BeTrue("the customize dialog must not scroll sideways on a phone");
    }
}
