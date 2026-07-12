using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

// Default (stock) deployment: NodesUi = Full, RestrictNodesToOwner = false. The owner sees the Nodes nav
// link, reaches the page and gets the manual "New Node" control.
[Collection(AppCollection.Name)]
public sealed class NodesUiTests(AppFixture app)
{
    private static readonly LocatorAssertionsToBeVisibleOptions Slow = new() { Timeout = 15000 };

    [Fact]
    public async Task Full_mode_shows_the_nodes_nav_page_and_add_button()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/");
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");

        await Assertions.Expect(page.Locator("a[href='/nodes']")).ToBeVisibleAsync(Slow);

        await page.GotoAsync("/nodes");
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");
        page.Url.Should().EndWith("/nodes");
        await Assertions.Expect(page.Locator("button:has-text('New Node')")).ToBeVisibleAsync(Slow);
    }
}
