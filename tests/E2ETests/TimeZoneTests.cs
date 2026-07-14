using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

// Drives the real time-zone UI: the settings Time-zone section + switcher are present, choosing a zone via
// the /set-timezone endpoint persists across a reload (cookie + re-issued claim), and the settings panel
// reflects the current zone. Each test resets the zone to UTC so it never leaks into other specs.
[Collection(AppCollection.Name)]
public sealed class TimeZoneTests(AppFixture app)
{
    private static async Task SetZoneAsync(IPage page, string zone) =>
        await page.GotoAsync($"/set-timezone?tz={Uri.EscapeDataString(zone)}&redirectUri=/",
            new PageGotoOptions { WaitUntil = WaitUntilState.Load });

    private static async Task OpenTimeZonePanelAsync(IPage page)
    {
        // On a phone the nav drawer is collapsed; open it so the Settings nav item is clickable.
        var settingsNav = page.Locator("[data-testid=nav-settings]");
        if (!await settingsNav.IsVisibleAsync())
            await page.Locator("[data-testid=nav-drawer-toggle]").ClickAsync();
        await settingsNav.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await settingsNav.ClickAsync();
        await page.Locator("[data-testid=settings-section-timezone]")
            .WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await page.Locator("[data-testid=settings-section-timezone]").ClickAsync();
        await page.Locator("[data-testid=settings-timezone-panel]")
            .WaitForAsync(new() { State = WaitForSelectorState.Visible });
    }

    [Fact]
    public async Task Settings_timezone_section_and_switcher_are_present()
    {
        var page = await app.NewAuthedPageAsync();
        try
        {
            await page.GotoAsync("/", new PageGotoOptions { WaitUntil = WaitUntilState.Load });
            await OpenTimeZonePanelAsync(page);

            (await page.Locator("[data-testid=settings-timezone-panel]").IsVisibleAsync())
                .Should().BeTrue("the settings dialog must expose a Time-zone section");
            (await page.Locator("[data-testid=timezone-switcher]").CountAsync())
                .Should().BeGreaterThan(0, "the Time-zone section must expose the switcher");
        }
        finally { await SetZoneAsync(page, "UTC"); }
    }

    [Fact]
    public async Task Choosing_a_zone_persists_across_a_reload_and_shows_in_settings()
    {
        var page = await app.NewAuthedPageAsync();
        try
        {
            await SetZoneAsync(page, "Asia/Tokyo");

            // Persists across a plain reload via the time-zone cookie + re-issued claim.
            await page.GotoAsync("/", new PageGotoOptions { WaitUntil = WaitUntilState.Load });
            await OpenTimeZonePanelAsync(page);

            (await page.Locator("[data-testid=timezone-current]").InnerTextAsync())
                .Should().Contain("Asia/Tokyo", "the settings panel must reflect the chosen zone after a reload");
        }
        finally { await SetZoneAsync(page, "UTC"); }
    }

    [Fact]
    public async Task Timezone_section_is_reachable_on_mobile()
    {
        var page = await app.NewAuthedMobilePageAsync();
        try
        {
            await page.GotoAsync("/", new PageGotoOptions { WaitUntil = WaitUntilState.Load });
            await OpenTimeZonePanelAsync(page);
            (await page.Locator("[data-testid=settings-timezone-panel]").IsVisibleAsync())
                .Should().BeTrue("the Time-zone section must be reachable on a phone");
        }
        finally { await SetZoneAsync(page, "UTC"); }
    }
}
