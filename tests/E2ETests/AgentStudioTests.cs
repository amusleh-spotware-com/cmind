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
        // An agent needs a managed account at creation — seed one before loading the page so the dialog's
        // account selector is populated.
        var (accountNumber, _) = await AgentTestHelpers.SeedTradingAccountAsync(page, app.BaseUrl);
        await page.GotoAsync("/agent-studio");
        await page.WaitForAppReadyAsync();

        // Retry the dialog-open and submit until the roster row appears: a click issued before the Blazor
        // circuit is interactive is dropped, so a single-shot open/submit flakes under parallel-boot CI load.
        var nameField = page.GetByLabel("Agent name");
        await page.ClickUntilVisibleAsync("[data-testid=agent-new]", nameField);
        await nameField.FillAsync("E2E Scalper");
        await AgentTestHelpers.SelectManagedAccountAsync(page, accountNumber);
        var row = page.Locator("[data-testid=agents-table]").GetByText("E2E Scalper").First;
        await page.ClickUntilVisibleAsync("[data-testid=agent-create-submit]", row);

        await Assertions.Expect(row).ToBeVisibleAsync(Slow);
    }

    [Fact]
    public async Task Create_is_disabled_when_the_name_duplicates_an_existing_agent()
    {
        var page = await app.NewAuthedPageAsync();
        var (accountNumber, _) = await AgentTestHelpers.SeedTradingAccountAsync(page, app.BaseUrl);
        await page.GotoAsync("/agent-studio");
        await page.WaitForAppReadyAsync();

        var name = "Dup " + Guid.NewGuid().ToString("N")[..6];

        // Create the first agent with this name.
        var nameField = page.GetByLabel("Agent name");
        await page.ClickUntilVisibleAsync("[data-testid=agent-new]", nameField);
        await nameField.FillAsync(name);
        await AgentTestHelpers.SelectManagedAccountAsync(page, accountNumber);
        var row = page.Locator("[data-testid=agents-table]").GetByText(name).First;
        await page.ClickUntilVisibleAsync("[data-testid=agent-create-submit]", row);
        await Assertions.Expect(row).ToBeVisibleAsync(Slow);

        // Reopen the dialog: the same name is now taken, so Create stays disabled even with an account picked.
        await page.ClickUntilVisibleAsync("[data-testid=agent-new]", nameField);
        await nameField.FillAsync(name);
        await AgentTestHelpers.SelectManagedAccountAsync(page, accountNumber);
        await Assertions.Expect(page.Locator("[data-testid=agent-create-submit]")).ToBeDisabledAsync(new() { Timeout = 15000 });

        // A unique name enables it again.
        await nameField.FillAsync(name + " v2");
        await Assertions.Expect(page.Locator("[data-testid=agent-create-submit]")).ToBeEnabledAsync(new() { Timeout = 15000 });
    }

    [Fact]
    public async Task Create_is_disabled_until_a_managed_account_is_selected()
    {
        var page = await app.NewAuthedPageAsync();
        var (accountNumber, _) = await AgentTestHelpers.SeedTradingAccountAsync(page, app.BaseUrl);
        await page.GotoAsync("/agent-studio");
        await page.WaitForAppReadyAsync();

        var nameField = page.GetByLabel("Agent name");
        await page.ClickUntilVisibleAsync("[data-testid=agent-new]", nameField);
        await nameField.FillAsync("NeedsAccount");

        // No account picked yet -> an agent can't be created (mandate 11: no doomed action).
        await Assertions.Expect(page.Locator("[data-testid=agent-create-submit]")).ToBeDisabledAsync(new() { Timeout = 15000 });

        // Selecting an account enables Create.
        await AgentTestHelpers.SelectManagedAccountAsync(page, accountNumber);
        await Assertions.Expect(page.Locator("[data-testid=agent-create-submit]")).ToBeEnabledAsync(new() { Timeout = 15000 });
    }

    [Fact]
    public async Task Detail_dialog_opens_and_debate_reports_disabled()
    {
        var page = await app.NewAuthedPageAsync();
        var (accountNumber, _) = await AgentTestHelpers.SeedTradingAccountAsync(page, app.BaseUrl);
        await page.GotoAsync("/agent-studio");
        await page.WaitForAppReadyAsync();

        var name = "Detail " + Guid.NewGuid().ToString("N")[..6];
        var nameField = page.GetByLabel("Agent name");
        await page.ClickUntilVisibleAsync("[data-testid=agent-new]", nameField);
        await nameField.FillAsync(name);
        await AgentTestHelpers.SelectManagedAccountAsync(page, accountNumber);
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
