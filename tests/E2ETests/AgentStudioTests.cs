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
        await page.WaitForAppReadyAsync();

        // Retry the dialog-open and submit until the roster row appears: a click issued before the Blazor
        // circuit is interactive is dropped, so a single-shot open/submit flakes under parallel-boot CI load.
        var nameField = page.GetByLabel("Agent name");
        await page.ClickUntilVisibleAsync("[data-testid=agent-new]", nameField);
        await nameField.FillAsync("E2E Scalper");
        var row = page.Locator("[data-testid=agents-table]").GetByText("E2E Scalper").First;
        await page.ClickUntilVisibleAsync("[data-testid=agent-create-submit]", row);

        await Assertions.Expect(row).ToBeVisibleAsync(Slow);
    }

    [Fact]
    public async Task Detail_dialog_opens_and_debate_reports_disabled()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/agent-studio");
        await page.WaitForAppReadyAsync();

        var name = "Detail " + Guid.NewGuid().ToString("N")[..6];
        var nameField = page.GetByLabel("Agent name");
        await page.ClickUntilVisibleAsync("[data-testid=agent-new]", nameField);
        await nameField.FillAsync(name);
        var createdRow = page.Locator("[data-testid=agents-table]").GetByText(name).First;
        await page.ClickUntilVisibleAsync("[data-testid=agent-create-submit]", createdRow);
        await Assertions.Expect(createdRow).ToBeVisibleAsync(Slow);

        // Open the agent's Details dialog from its roster row (retry until it appears — parallel-boot race).
        await page.ClickUntilVisibleAsync($"tr:has-text(\"{name}\") [data-testid=agent-details]", page.Locator(".mud-dialog"));
        await Assertions.Expect(page.Locator(".mud-dialog")).ToBeVisibleAsync(Slow);

        // The Research Desk debate degrades to "not configured" when the AI key is absent (test env),
        // proving the UI -> /debate endpoint -> ResearchDesk path is wired without needing the key.
        await page.ClickAsync("text=Research desk");
        await page.ClickAsync("[data-testid=agent-debate]");
        await Assertions.Expect(page.Locator(".mud-dialog")).ToContainTextAsync("not configured", SlowText);
    }
}
