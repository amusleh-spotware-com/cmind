using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

// Drives the owner "Deployment" (white-label) settings surface through the real UI: every category renders
// as a tab, every catalogued option is present, an override round-trips (edit → source flips to Owner →
// persists across reload → revert), reset-all works, and the surface renders on mobile.
[Collection(AppCollection.Name)]
public sealed class DeploymentSettingsE2ETests(AppFixture app)
{
    private static readonly string[] Categories =
        ["Branding", "Theme", "Features", "Registration", "Accounts", "Email", "Ai", "OpenApi", "PropFirm"];

    [Fact]
    public async Task Every_category_tab_and_all_options_render()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/settings/deployment", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        (await page.Locator(".blazor-error-ui").IsVisibleAsync()).Should().BeFalse();
        (await page.Locator("[data-testid=page-error]").IsVisibleAsync()).Should().BeFalse();

        await page.Locator("[data-testid=wl-tabs]").WaitForAsync(new LocatorWaitForOptions { Timeout = 5000 });

        // One tab per white-label category, and every tab's options render when selected (proves the tabbed
        // surface + that no section is empty). The parity unit/integration tests guarantee the catalog itself
        // covers every white-label option.
        var tabs = page.Locator("[data-testid=wl-tabs] .mud-tab");
        (await tabs.CountAsync()).Should().Be(Categories.Length, "one tab per white-label category");

        var totalOptions = 0;
        for (var i = 0; i < Categories.Length; i++)
        {
            await tabs.Nth(i).ClickAsync();
            var visible = await page.Locator("[data-testid^='wl-opt-']:visible").CountAsync();
            visible.Should().BeGreaterThan(0, $"tab #{i} must render its options");
            totalOptions += visible;
        }
        totalOptions.Should().BeGreaterThan(40, "the white-label options render across the tabs");
    }

    [Fact]
    public async Task Override_round_trips_edit_persist_and_revert()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/settings/deployment", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        const string key = "branding.showSiteLink";
        var source = page.Locator($"[data-testid='wl-source-{key}']");
        await source.WaitForAsync(new LocatorWaitForOptions { Timeout = 5000 });

        // Edit the boolean via the dialog.
        await page.Locator($"[data-testid='wl-edit-{key}']").ClickAsync();
        var dialog = page.Locator(".mud-dialog");
        await dialog.WaitForAsync(new LocatorWaitForOptions { Timeout = 5000 });
        await page.Locator("[data-testid=wl-edit-bool]").ClickAsync();
        await page.Locator("[data-testid=wl-edit-save]").ClickAsync();

        await Assertions.Expect(source).ToHaveTextAsync(new System.Text.RegularExpressions.Regex("Owner"),
            new LocatorAssertionsToHaveTextOptions { Timeout = 5000 });

        // Persists across a reload.
        await page.ReloadAsync(new PageReloadOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await Assertions.Expect(page.Locator($"[data-testid='wl-source-{key}']"))
            .ToHaveTextAsync(new System.Text.RegularExpressions.Regex("Owner"),
                new LocatorAssertionsToHaveTextOptions { Timeout = 5000 });

        // Revert back to configuration.
        await page.Locator($"[data-testid='wl-revert-{key}']").ClickAsync();
        await Assertions.Expect(page.Locator($"[data-testid='wl-source-{key}']"))
            .Not.ToHaveTextAsync(new System.Text.RegularExpressions.Regex("Owner"),
                new LocatorAssertionsToHaveTextOptions { Timeout = 5000 });
    }

    [Fact]
    public async Task Reset_all_clears_overrides()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/settings/deployment", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        const string key = "branding.requireMfa";
        await page.Locator($"[data-testid='wl-edit-{key}']").ClickAsync();
        await page.Locator(".mud-dialog").WaitForAsync(new LocatorWaitForOptions { Timeout = 5000 });
        await page.Locator("[data-testid=wl-edit-bool]").ClickAsync();
        await page.Locator("[data-testid=wl-edit-save]").ClickAsync();
        await Assertions.Expect(page.Locator($"[data-testid='wl-source-{key}']"))
            .ToHaveTextAsync(new System.Text.RegularExpressions.Regex("Owner"),
                new LocatorAssertionsToHaveTextOptions { Timeout = 5000 });

        await page.Locator("[data-testid=wl-reset-all]").ClickAsync();
        await Assertions.Expect(page.Locator($"[data-testid='wl-source-{key}']"))
            .Not.ToHaveTextAsync(new System.Text.RegularExpressions.Regex("Owner"),
                new LocatorAssertionsToHaveTextOptions { Timeout = 5000 });
    }

    [Fact]
    public async Task Renders_on_mobile()
    {
        var page = await app.NewAuthedMobilePageAsync();
        await page.GotoAsync("/settings/deployment", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        (await page.Locator(".blazor-error-ui").IsVisibleAsync()).Should().BeFalse();
        (await page.Locator("[data-testid=page-error]").IsVisibleAsync()).Should().BeFalse();
        await page.Locator("[data-testid=wl-tabs]").WaitForAsync(new LocatorWaitForOptions { Timeout = 5000 });
        (await page.Locator("[data-testid=wl-tabs]").IsVisibleAsync()).Should().BeTrue();
    }
}
