using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

// Drives the real time-zone UI: the switcher lists EVERY zone (not just the current one), selecting a zone
// through the switcher changes and persists it, and the section is reachable on a phone. Each test resets the
// zone to UTC so it never leaks into other specs.
[Collection(AppCollection.Name)]
public sealed class TimeZoneTests(AppFixture app)
{
    private static async Task SetZoneAsync(IPage page, string zone) =>
        await page.GotoAsync($"/set-timezone?tz={Uri.EscapeDataString(zone)}&redirectUri=/",
            new PageGotoOptions { WaitUntil = WaitUntilState.Load });

    private static async Task OpenTimeZonePanelAsync(IPage page)
    {
        // Wait for the app shell to mount (it may still be rendering right after a full reload) before
        // deciding whether the drawer needs opening.
        var settingsNav = page.Locator("[data-testid=nav-settings]");
        await settingsNav.WaitForAsync(new() { State = WaitForSelectorState.Attached });
        // On a phone the nav drawer starts collapsed (temporary variant); a single toggle may only flip its
        // internal state, so click until the Settings item is actually visible (a few attempts, then give up).
        for (var attempt = 0; attempt < 4 && !await settingsNav.IsVisibleAsync(); attempt++)
        {
            await page.Locator("[data-testid=nav-drawer-toggle]").ClickAsync();
            try { await settingsNav.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 3000 }); }
            catch (TimeoutException) { /* retry */ }
        }
        await settingsNav.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await settingsNav.ClickAsync();
        await page.Locator("[data-testid=settings-section-timezone]")
            .WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await page.Locator("[data-testid=settings-section-timezone]").ClickAsync();
        await page.Locator("[data-testid=settings-timezone-panel]")
            .WaitForAsync(new() { State = WaitForSelectorState.Visible });
    }

    [Fact]
    public async Task Switcher_lists_every_time_zone_not_just_the_current_one()
    {
        var page = await app.NewAuthedPageAsync();
        try
        {
            await page.GotoAsync("/", new PageGotoOptions { WaitUntil = WaitUntilState.Load });
            await OpenTimeZonePanelAsync(page);

            // Open the autocomplete and assert it offers the whole zone database — the regression that shipped
            // showed only the single current zone because the search seeded itself with the current selection.
            await page.Locator("[data-testid=settings-timezone-panel] input").ClickAsync();
            await page.Locator("[data-testid^='timezone-option-']").First
                .WaitForAsync(new() { State = WaitForSelectorState.Visible });
            (await page.Locator("[data-testid^='timezone-option-']").CountAsync())
                .Should().BeGreaterThan(50, "the switcher must list every zone, not only the current one");
        }
        finally { await SetZoneAsync(page, "UTC"); }
    }

    [Fact]
    public async Task Selecting_a_zone_in_the_switcher_changes_and_persists_it()
    {
        var page = await app.NewAuthedPageAsync();
        try
        {
            await page.GotoAsync("/", new PageGotoOptions { WaitUntil = WaitUntilState.Load });
            await OpenTimeZonePanelAsync(page);

            // Drive the real control: type to filter, pick a specific zone, and assert it actually took effect.
            var input = page.Locator("[data-testid=settings-timezone-panel] input");
            await input.ClickAsync();
            await input.FillAsync("Tokyo");
            var option = page.Locator("[data-testid='timezone-option-Asia/Tokyo']");
            await option.WaitForAsync(new() { State = WaitForSelectorState.Visible });
            // Selecting force-reloads the page (the circuit can't change zone live); wait for that reload to
            // tear the dialog down, then assert the choice actually took effect — the zone cookie now holds it.
            await option.ClickAsync();
            await page.Locator("[data-testid=settings-timezone-panel]")
                .WaitForAsync(new() { State = WaitForSelectorState.Detached });

            var cookies = await page.Context.CookiesAsync();
            var zoneCookie = cookies.FirstOrDefault(c => c.Name == ".Cmind.TimeZone");
            zoneCookie.Should().NotBeNull("picking a zone in the switcher must persist it");
            Uri.UnescapeDataString(zoneCookie!.Value).Should().Be("Asia/Tokyo",
                "the switcher selection must change the user's zone to the picked one");
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
