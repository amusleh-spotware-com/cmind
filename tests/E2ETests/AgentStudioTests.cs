using System;
using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

[Collection(AppCollection.Name)]
public sealed class AgentStudioTests(AppFixture app)
{
    private static readonly LocatorAssertionsToBeVisibleOptions Slow = new() { Timeout = 15000 };
    private static readonly LocatorAssertionsToContainTextOptions SlowText = new() { Timeout = 15000 };

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

    [Fact]
    public async Task Detail_dialog_opens_and_debate_reports_disabled()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/agent-studio");
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");

        var name = "Detail " + Guid.NewGuid().ToString("N")[..6];
        await page.ClickAsync("[data-testid=agent-new]");
        await page.GetByLabel("Agent name").FillAsync(name);
        await page.ClickAsync("[data-testid=agent-create-submit]");
        await Assertions.Expect(page.Locator("[data-testid=agents-table]")).ToContainTextAsync(name, SlowText);

        // Open the agent's Details dialog from its roster row.
        await page.Locator($"tr:has-text(\"{name}\")").Locator("[data-testid=agent-details]").ClickAsync();
        await Assertions.Expect(page.Locator(".mud-dialog")).ToBeVisibleAsync(Slow);

        // The Research Desk debate degrades to "not configured" when the AI key is absent (test env),
        // proving the UI -> /debate endpoint -> ResearchDesk path is wired without needing the key.
        await page.ClickAsync("text=Research desk");
        await page.ClickAsync("[data-testid=agent-debate]");
        await Assertions.Expect(page.Locator(".mud-dialog")).ToContainTextAsync("not configured", SlowText);
    }
}
