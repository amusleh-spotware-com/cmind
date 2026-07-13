using System;
using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

// Regression coverage for the batch of bugs found by manual clicking (CLAUDE.md mandate 11): dependency
// gating (run/backtest need a trading account), state-correct + icon lifecycle controls, no GUIDs in the
// UI, detail/edit as a dialog, and the calendar's data-source gate. The base fixture's user has NO trading
// accounts and NO AI key/data source — exactly the first-run state a user sees.
[Collection(AppCollection.Name)]
public sealed class ManualFindingsTests(AppFixture app)
{
    private static readonly LocatorAssertionsToBeVisibleOptions Slow = new() { Timeout = 15000 };

    [Fact]
    public async Task Run_new_is_disabled_and_explained_without_a_trading_account()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/run", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");

        await Assertions.Expect(page.Locator("[data-testid=run-new]")).ToBeDisabledAsync(new() { Timeout = 15000 });
        await Assertions.Expect(page.Locator("[data-testid=run-no-accounts]")).ToBeVisibleAsync(Slow);
        (await page.Locator(".blazor-error-ui").IsVisibleAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Backtest_new_is_disabled_and_explained_without_a_trading_account()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/backtest", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");

        await Assertions.Expect(page.Locator("[data-testid=backtest-new]")).ToBeDisabledAsync(new() { Timeout = 15000 });
        await Assertions.Expect(page.Locator("[data-testid=backtest-no-accounts]")).ToBeVisibleAsync(Slow);
    }

    [Fact]
    public async Task Copy_trading_renders_without_a_pause_control()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/copy-trading", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");

        (await page.Locator("text=Copy Trading").First.IsVisibleAsync()).Should().BeTrue();
        // The pointless Pause control was removed; lifecycle controls are icon buttons.
        (await page.GetByRole(AriaRole.Button, new() { Name = "Pause" }).CountAsync())
            .Should().Be(0, "the copy-trading pause control was removed");
        (await page.Locator(".blazor-error-ui").IsVisibleAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Agent_studio_supports_account_assignment_edit_and_icon_controls()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/agent-studio");
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");

        // The create dialog exposes the managed-accounts selector (issue 4: assign accounts to an agent).
        await page.ClickAsync("[data-testid=agent-new]");
        await Assertions.Expect(page.Locator("[data-testid=agent-accounts]")).ToBeVisibleAsync(Slow);

        var name = "Edit " + Guid.NewGuid().ToString("N")[..6];
        await page.GetByLabel("Agent name").FillAsync(name);
        await page.ClickAsync("[data-testid=agent-create-submit]");
        await Assertions.Expect(page.Locator("[data-testid=agents-table]")).ToContainTextAsync(name, new() { Timeout = 15000 });

        var row = page.Locator($"tr:has-text(\"{name}\")");
        // Lifecycle controls are icon buttons; Stop is disabled for a fresh (Draft) agent.
        await Assertions.Expect(row.Locator("[data-testid=agent-start]")).ToBeVisibleAsync(Slow);
        await Assertions.Expect(row.Locator("[data-testid=agent-stop]")).ToBeDisabledAsync(new() { Timeout = 15000 });

        // The edit dialog opens and offers the managed-accounts selector.
        await row.Locator("[data-testid=agent-edit]").ClickAsync();
        await Assertions.Expect(page.Locator("[data-testid=agent-edit-accounts]")).ToBeVisibleAsync(Slow);
    }

    [Fact]
    public async Task Calendar_requires_a_configured_data_source()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/economic-calendar", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");

        // Enabled by branding but no FRED/BLS key ⇒ the actionable source-required notice, not empty values.
        await Assertions.Expect(page.Locator("[data-testid=calendar-source-required]")).ToBeVisibleAsync(Slow);
        (await page.Locator(".blazor-error-ui").IsVisibleAsync()).Should().BeFalse();
    }
}
