using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

[Collection(AppCollection.Name)]
public sealed class AgentTests(AppFixture app)
{
    private static readonly LocatorAssertionsToBeVisibleOptions Slow = new() { Timeout = 15000 };

    [Fact]
    public async Task Agent_page_renders_mandate_form_and_decision_journal()
    {
        var page = await OpenAgentAsync();

        await Assertions.Expect(page.GetByText("New mandate")).ToBeVisibleAsync(Slow);
        await Assertions.Expect(page.GetByText("Decision journal")).ToBeVisibleAsync(Slow);
        await Assertions.Expect(page.Locator("button:has-text('Create mandate')")).ToBeVisibleAsync(Slow);
    }

    [Fact]
    public async Task Agent_lists_load_without_error_when_empty()
    {
        var page = await OpenAgentAsync();

        // Both /api/agent/mandates and /api/agent/proposals are fetched on init; empty-state text
        // proves the endpoints returned 200 and the page bound the results.
        await Assertions.Expect(page.GetByText("No mandates yet.")).ToBeVisibleAsync(Slow);
        await Assertions.Expect(page.GetByText(
            "No proposals yet. The agent runs on a schedule when a mandate is enabled.")).ToBeVisibleAsync(Slow);
    }

    private async Task<IPage> OpenAgentAsync()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/agent");
        await page.WaitForAppReadyAsync();
        return page;
    }
}
