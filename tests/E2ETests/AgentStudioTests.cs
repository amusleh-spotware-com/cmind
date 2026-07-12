using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

[Collection(AppCollection.Name)]
public sealed class AgentStudioTests(AppFixture app)
{
    private static readonly LocatorAssertionsToBeVisibleOptions Slow = new() { Timeout = 15000 };

    [Fact]
    public async Task Create_agent_shows_it_in_the_roster()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/agent-studio");
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");

        await page.ClickAsync("[data-testid=agent-new]");
        await page.GetByLabel("Agent name").FillAsync("E2E Scalper");
        await page.ClickAsync("[data-testid=agent-create-submit]");

        await Assertions.Expect(page.Locator("[data-testid=agents-table]")).ToBeVisibleAsync(Slow);
        await Assertions.Expect(page.Locator("[data-testid=agents-table]")).ToContainTextAsync("E2E Scalper");
    }
}
