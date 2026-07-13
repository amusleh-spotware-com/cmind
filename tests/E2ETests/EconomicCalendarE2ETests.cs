using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

// Drives the economic-calendar page through the real UI on mobile emulation. The base fixture configures no
// FRED/BLS key, so the calendar is source-less: it renders the actionable "configure a source" notice (not
// empty values or a raw error) and hides the filter action (CLAUDE.md mandate 11 — dependency gating).
[Collection(AppCollection.Name)]
public sealed class EconomicCalendarE2ETests(AppFixture app)
{
    [Fact]
    public async Task Calendar_page_is_gated_and_renders_without_error_on_mobile()
    {
        var page = await app.NewAuthedMobilePageAsync();
        await page.GotoAsync("/economic-calendar", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        (await page.Locator(".blazor-error-ui").IsVisibleAsync()).Should().BeFalse();
        (await page.Locator("[data-testid=page-error]").IsVisibleAsync()).Should().BeFalse();

        // Source-less ⇒ the actionable notice is shown and the filter action is hidden.
        await page.Locator("[data-testid=calendar-source-required]")
            .WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        (await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Filters" }).CountAsync())
            .Should().Be(0, "the filter action is hidden until a data source is configured");

        // G-06: the page carries a HelpTip explaining what the calendar shows / how to configure a source.
        (await page.Locator("[data-testid=help-tip]").First.IsVisibleAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task Series_history_page_renders()
    {
        var page = await app.NewAuthedMobilePageAsync();
        await page.GotoAsync("/economic-calendar/series/US.CPI",
            new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        (await page.Locator(".blazor-error-ui").IsVisibleAsync()).Should().BeFalse();
        (await page.Locator("[data-testid=page-error]").IsVisibleAsync()).Should().BeFalse();
        (await page.GetByText("US.CPI").First.IsVisibleAsync()).Should().BeTrue();

        // G-02: source-less ⇒ the series page shows the same actionable notice as the main calendar,
        // not a neutral "no events" alert.
        await page.Locator("[data-testid=calendar-source-required]")
            .WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });

        // G-06: the series page carries a HelpTip too.
        (await page.Locator("[data-testid=help-tip]").First.IsVisibleAsync()).Should().BeTrue();
    }

    // I-06: the series page must never leave a permanent blank content area while data loads — it always
    // resolves to one of its known states (a loading indicator, then the source-required notice / empty
    // state / events). Assert one of those is present so a regression to a blank chart area is caught.
    [Fact]
    public async Task Series_page_never_shows_a_permanent_blank_content_area()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/economic-calendar/series/US.CPI",
            new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        (await page.Locator(".blazor-error-ui").IsVisibleAsync()).Should().BeFalse();

        // Exactly one resolved state must be visible — never a blank body with only the heading.
        var resolved = page.Locator(
            "[data-testid=calendar-source-required], [data-testid=series-loading], [data-testid=series-empty], .mud-card");
        await resolved.First.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
        (await resolved.CountAsync()).Should().BeGreaterThan(0,
            "the series page must resolve to a loading, notice, empty, or data state — never a permanent blank");
    }
}
