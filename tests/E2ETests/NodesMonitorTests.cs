using System.Diagnostics;
using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

// White-label deployment that keeps a read-only Nodes view: NodesUi = Monitor. The nav link and page stay,
// but the manual "New Node" control is gone — the cluster is grown by auto-discovery only.
public sealed class NodesMonitorFixture : AppFixture
{
    protected override void ConfigureApp(ProcessStartInfo psi)
    {
        psi.Environment["App__Branding__NodesUi"] = "Monitor";
    }
}

[CollectionDefinition(Name)]
public sealed class NodesMonitorCollection : ICollectionFixture<NodesMonitorFixture>
{
    public const string Name = "nodes-monitor";
}

[Collection(NodesMonitorCollection.Name)]
public sealed class NodesMonitorTests(NodesMonitorFixture app)
{
    private static readonly LocatorAssertionsToBeVisibleOptions Slow = new() { Timeout = 15000 };

    [Fact]
    public async Task Monitor_mode_shows_the_page_but_hides_the_add_button()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/");
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");

        // The nav link and page are still reachable...
        await Assertions.Expect(page.Locator("a[href='/nodes']")).ToBeVisibleAsync(Slow);

        await page.GotoAsync("/nodes");
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");
        page.Url.Should().EndWith("/nodes");
        await Assertions.Expect(page.GetByText("Nodes")).ToBeVisibleAsync(Slow);

        // ...but the manual add control is gone.
        (await page.Locator("button:has-text('New Node')").CountAsync()).Should().Be(0);
    }
}
