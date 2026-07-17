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
        await page.WaitForAppReadyAsync();

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
        await page.WaitForAppReadyAsync();

        // Expand both groups and confirm both Optimize entries exist and point at different routes.
        await page.Locator(".mud-nav-group button:has-text('cBots')").First.ClickAsync();
        await page.Locator(".mud-nav-group button:has-text('AI')").First.ClickAsync();

        (await page.Locator("a[href='/optimize']").CountAsync()).Should().Be(1, "cBots Optimize (coming soon)");
        (await page.Locator("a[href='/ai/optimize']").CountAsync()).Should().Be(1, "AI Optimize");
        (await page.Locator("a[href='/copy-trading']").CountAsync()).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Tools_group_holds_the_quant_analytics_pages()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForAppReadyAsync();

        // The quant analytics pages now live under a dedicated "Tools" group, moved out of cBots. Assert
        // structurally (the links are inside the Tools group and no longer under cBots) — independent of the
        // collapse-animation timing.
        var toolsGroup = page.Locator(".mud-nav-group:has(button:has-text('Tools'))");
        (await toolsGroup.Locator("a[href='/quant/integrity']").CountAsync()).Should().Be(1);
        (await toolsGroup.Locator("a[href='/quant/sizing']").CountAsync()).Should().Be(1);
        (await toolsGroup.Locator("a[href='/quant/execution']").CountAsync()).Should().Be(1);

        var cbotsGroup = page.Locator(".mud-nav-group:has(button:has-text('cBots'))");
        (await cbotsGroup.Locator("a[href='/quant/integrity']").CountAsync())
            .Should().Be(0, "the quant pages moved from cBots to Tools");
    }

    [Fact]
    public async Task Tools_group_is_ordered_below_copy_trading()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForAppReadyAsync();

        // The Tools group is pinned below the Copy trading link in the drawer. Assert DOM order:
        // Copy trading precedes the Tools group (Node.DOCUMENT_POSITION_FOLLOWING == 4).
        var copyPrecedesTools = await page.EvaluateAsync<bool>(
            """
            () => {
                const copy = document.querySelector("a[href='/copy-trading']");
                const groups = [...document.querySelectorAll('.mud-nav-group')];
                const tools = groups.find(g => g.querySelector("button")?.textContent?.includes('Tools'));
                if (!copy || !tools) return false;
                return (copy.compareDocumentPosition(tools) & Node.DOCUMENT_POSITION_FOLLOWING) !== 0;
            }
            """);
        copyPrecedesTools.Should().BeTrue("the Tools group must render below the Copy trading link");
    }
}
