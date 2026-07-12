using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

// Drives the economic-calendar page through the real UI on mobile emulation: it renders without tripping
// the Blazor error UI, and the filter action opens a dialog (mandate 7 — a dialog, never an inline form).
[Collection(AppCollection.Name)]
public sealed class EconomicCalendarE2ETests(AppFixture app)
{
    [Fact]
    public async Task Calendar_page_renders_on_mobile()
    {
        var page = await app.NewAuthedMobilePageAsync();
        await page.GotoAsync("/economic-calendar", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        (await page.Locator(".blazor-error-ui").IsVisibleAsync()).Should().BeFalse();
        (await page.Locator("[data-testid=page-error]").IsVisibleAsync()).Should().BeFalse();
        // The page body rendered: its Filters action (which only appears when the calendar is enabled) is visible.
        var filters = page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Filters" });
        await filters.WaitForAsync(new LocatorWaitForOptions { Timeout = 5000 });
        (await filters.IsVisibleAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task Filter_action_opens_a_dialog()
    {
        var page = await app.NewAuthedMobilePageAsync();
        await page.GotoAsync("/economic-calendar", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Filters" }).ClickAsync();

        var dialog = page.Locator(".mud-dialog");
        await dialog.WaitForAsync(new LocatorWaitForOptions { Timeout = 5000 });
        (await dialog.GetByText("Minimum impact").IsVisibleAsync()).Should().BeTrue();
    }
}
