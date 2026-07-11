using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

// The Settings nav item now opens a full-screen dialog with a section list (Open API / AI / Features /
// Legal) instead of separate nav links. Verifies it opens, shows sections for the owner, and switches.
[Collection(AppCollection.Name)]
public sealed class SettingsDialogTests(AppFixture app)
{
    [Fact]
    public async Task Settings_opens_full_screen_dialog_and_switches_sections()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");

        await page.Locator("[data-testid=nav-settings]").ClickAsync();

        var dialog = page.Locator("[data-testid=settings-dialog]");
        await dialog.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
        (await page.Locator("[data-testid=page-error]").IsVisibleAsync()).Should().BeFalse();

        // Owner sees the owner-only sections.
        (await page.Locator("[data-testid=settings-section-features]").IsVisibleAsync()).Should().BeTrue();
        (await page.Locator("[data-testid=settings-section-ai]").IsVisibleAsync()).Should().BeTrue();

        // Switching to Features renders that panel inside the dialog.
        await page.Locator("[data-testid=settings-section-features]").ClickAsync();
        await page.GetByText("Turn main product features").WaitForAsync(new() { Timeout = 8000 });
    }

    [Fact]
    public async Task Every_settings_section_renders_and_close_dismisses()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");

        await page.Locator("[data-testid=nav-settings]").ClickAsync();
        var dialog = page.Locator("[data-testid=settings-dialog]");
        await dialog.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });

        await page.Locator("[data-testid=settings-section-ai]").ClickAsync();
        await page.GetByLabel("Anthropic API key").WaitForAsync(new() { Timeout = 8000 });

        await page.Locator("[data-testid=settings-section-openapi]").ClickAsync();
        await page.GetByText("Open API application", new() { Exact = false }).First
            .WaitForAsync(new() { Timeout = 8000 });

        await page.Locator("[data-testid=settings-section-legal]").ClickAsync();
        await page.GetByText("Agreements").WaitForAsync(new() { Timeout = 8000 });

        (await page.Locator("[data-testid=page-error]").IsVisibleAsync()).Should().BeFalse();

        await page.GetByRole(AriaRole.Button, new() { Name = "Close settings" }).ClickAsync();
        await dialog.WaitForAsync(new() { State = WaitForSelectorState.Hidden, Timeout = 8000 });
    }
}
