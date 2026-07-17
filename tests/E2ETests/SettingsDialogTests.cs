using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

// The Settings nav item opens a centered, windowed dialog (Claude-settings style, not a full-screen
// takeover) with a section list (Open API / AI / Features / Legal) instead of separate nav links.
// Verifies it opens windowed, shows sections for the owner, and switches.
[Collection(AppCollection.Name)]
public sealed class SettingsDialogTests(AppFixture app)
{
    [Fact]
    public async Task Settings_opens_windowed_dialog_and_switches_sections()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForAppReadyAsync();

        await page.Locator("[data-testid=nav-settings]").ClickAsync();

        var dialog = page.Locator("[data-testid=settings-dialog]");
        await dialog.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
        (await page.Locator("[data-testid=page-error]").IsVisibleAsync()).Should().BeFalse();

        // Windowed, not full-screen: MudBlazor only adds mud-dialog-fullscreen for a full-screen dialog.
        (await page.Locator(".mud-dialog-fullscreen").CountAsync())
            .Should().Be(0, "settings is a centered window on desktop, not a full-screen takeover");

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
        await page.WaitForAppReadyAsync();

        await page.Locator("[data-testid=nav-settings]").ClickAsync();
        var dialog = page.Locator("[data-testid=settings-dialog]");
        await dialog.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });

        await page.Locator("[data-testid=settings-section-ai]").ClickAsync();
        // Multi-provider AI: the section shows the provider list / add-provider control, not a single key field.
        await page.Locator("[data-testid=ai-add-provider], [data-testid=ai-no-providers]").First
            .WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 8000 });

        await page.Locator("[data-testid=settings-section-openapi]").ClickAsync();
        await page.GetByText("Open API application", new() { Exact = false }).First
            .WaitForAsync(new() { Timeout = 8000 });

        await page.Locator("[data-testid=settings-section-legal]").ClickAsync();
        await page.GetByText("Agreements").WaitForAsync(new() { Timeout = 8000 });

        (await page.Locator("[data-testid=page-error]").IsVisibleAsync()).Should().BeFalse();

        await page.GetByRole(AriaRole.Button, new() { Name = "Close settings" }).ClickAsync();
        await dialog.WaitForAsync(new() { State = WaitForSelectorState.Hidden, Timeout = 8000 });
    }

    [Fact]
    public async Task Time_zone_section_does_not_scroll_horizontally()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForAppReadyAsync();

        await page.Locator("[data-testid=nav-settings]").ClickAsync();
        var dialog = page.Locator("[data-testid=settings-dialog]");
        await dialog.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });

        await page.Locator("[data-testid=settings-section-timezone]").ClickAsync();
        await page.Locator("[data-testid=settings-timezone-panel]")
            .WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 8000 });

        // The full-width time-zone autocomplete used to push the windowed dialog wide, giving it a
        // horizontal scrollbar; the content panel must contain itself now.
        var fits = await page.EvaluateAsync<bool>(
            "() => { const c = document.querySelector('.mud-dialog-content'); return !!c && c.scrollWidth <= c.clientWidth + 1; }");
        fits.Should().BeTrue("the time-zone settings panel must not scroll horizontally");

        (await page.Locator("[data-testid=page-error]").IsVisibleAsync()).Should().BeFalse();
    }
}
