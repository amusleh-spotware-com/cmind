using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

// Navigation drawer behaviour: groups are collapsed by default and expand on click; the distinct/updated
// icons render as links.
[Collection(AppCollection.Name)]
public sealed class NavMenuTests(AppFixture app)
{
    [Fact]
    public async Task Groups_are_collapsed_by_default_and_expand_on_click()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");

        // The cBots group is collapsed, so its child links are not visible until expanded.
        var runLink = page.GetByRole(AriaRole.Link, new() { Name = "Run", Exact = true });
        (await runLink.IsVisibleAsync()).Should().BeFalse("nav groups must start collapsed");

        await page.Locator(".mud-nav-group button:has-text('cBots')").First.ClickAsync();
        await runLink.First.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
    }

    [Fact]
    public async Task Ai_and_cbots_optimize_use_distinct_links()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");

        // Expand both groups and confirm both Optimize entries exist and point at different routes.
        await page.Locator(".mud-nav-group button:has-text('cBots')").First.ClickAsync();
        await page.Locator(".mud-nav-group button:has-text('AI')").First.ClickAsync();

        (await page.Locator("a[href='/optimize']").CountAsync()).Should().Be(1, "cBots Optimize (coming soon)");
        (await page.Locator("a[href='/ai/optimize']").CountAsync()).Should().Be(1, "AI Optimize");
        (await page.Locator("a[href='/copy-trading']").CountAsync()).Should().BeGreaterThan(0);
    }
}
