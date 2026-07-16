using System.Text.Json;
using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

// The instance detail page must reflect state changes live (no manual reload): a run stopped from the page,
// or a backtest that finishes, should update the status chip, swap Stop→Start and (for a backtest) reveal
// the equity curve. Uses the dev-only /api/testseed to create a running instance without Docker.
[Collection(AiLocalCollection.Name)]
public sealed class InstanceLiveUpdateTests(AiLocalFixture app)
{
    private static readonly LocatorAssertionsToBeVisibleOptions Slow = new() { Timeout = 30000 };

    private static async Task<Guid> SeedRunningInstanceAsync(IPage page)
    {
        var response = await page.APIRequest.PostAsync("/api/testseed/ai-portfolio", new() { DataObject = new { } });
        response.Ok.Should().BeTrue($"seed failed: {response.Status} {await response.TextAsync()}");
        using var doc = JsonDocument.Parse(await response.TextAsync());
        return doc.RootElement.GetProperty("runningInstanceId").GetGuid();
    }

    [Fact]
    public async Task Stopping_from_the_detail_page_updates_it_live_without_reload()
    {
        var page = await app.NewAuthedPageAsync();
        var runningId = await SeedRunningInstanceAsync(page);

        await page.GotoAsync($"/instance/{runningId}", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");

        // A running instance shows Stop.
        await Assertions.Expect(page.Locator("[data-testid=instance-detail-stop]")).ToBeVisibleAsync(Slow);

        await page.ClickAsync("[data-testid=instance-detail-stop]");

        // Without a page reload, the instance becomes terminal: the Stop icon is replaced by Start.
        await Assertions.Expect(page.Locator("[data-testid=instance-detail-start]")).ToBeVisibleAsync(Slow);
        (await page.Locator("[data-testid=instance-detail-stop]").IsVisibleAsync())
            .Should().BeFalse("a stopped instance must not still show the Stop control");
    }

    // The real bug: an instance transitions with NO user action on this page (a backtest self-completes via
    // the poller). The detail page's background poll must follow the lineage and update the UI — reproduced
    // here by transitioning the instance from outside the page.
    [Fact]
    public async Task Detail_page_follows_a_transition_triggered_elsewhere_via_polling()
    {
        var page = await app.NewAuthedPageAsync();
        var runningId = await SeedRunningInstanceAsync(page);

        await page.GotoAsync($"/instance/{runningId}", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");
        await Assertions.Expect(page.Locator("[data-testid=instance-detail-stop]")).ToBeVisibleAsync(Slow);

        // Transition the instance from OUTSIDE this page (simulating the completion poller / another client).
        var stop = await page.APIRequest.PostAsync($"/api/instances/{runningId}/stop", new());
        stop.Ok.Should().BeTrue($"external stop failed: {stop.Status}");

        // The page's background poll must follow the lineage to the new terminal instance and show Start —
        // no manual refresh.
        await Assertions.Expect(page.Locator("[data-testid=instance-detail-start]"))
            .ToBeVisibleAsync(new() { Timeout = 30000 });
    }
}
