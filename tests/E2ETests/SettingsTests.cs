using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

// Navigating to settings and changing them: adding a provider on /settings/ai enables AI (a real
// round-trip through the encrypted provider store), and deleting it disables AI again. The single-key
// flow was replaced by the multi-provider UI (Add provider dialog + provider cards). Feature-toggle
// settings are covered by FeatureToggleTests.
[Collection(AppCollection.Name)]
public sealed class SettingsTests(AppFixture app)
{
    private static readonly LocatorAssertionsToBeVisibleOptions Slow = new() { Timeout = 15000 };

    [Fact]
    public async Task Ai_settings_add_provider_enables_ai_then_delete_disables()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/settings/ai");
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");

        // No provider yet → AI disabled.
        await Assertions.Expect(page.Locator("[data-testid=ai-no-providers]")).ToBeVisibleAsync(Slow);

        // Add an Anthropic provider (kind 0 is the default) with a dummy key → a provider card appears.
        await page.Locator("[data-testid=ai-add-provider]").First.ClickAsync();
        await page.Locator("[data-testid=ai-dlg-key]").WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });
        await page.FillAsync("[data-testid=ai-dlg-key]", "sk-ant-e2e-dummy-key-0123456789");
        await ClickUntilAsync(page, "[data-testid=ai-dlg-save]", "[data-testid=ai-provider-card]");
        await Assertions.Expect(page.Locator("[data-testid=ai-provider-card]").First).ToBeVisibleAsync(Slow);

        // Delete it → back to the "no provider / AI disabled" state.
        await ClickUntilAsync(page, "[data-testid=ai-delete-provider]", "[data-testid=ai-no-providers]");
        await Assertions.Expect(page.Locator("[data-testid=ai-no-providers]")).ToBeVisibleAsync(Slow);
    }

    // Clicks a control (retrying across circuit reconnects) until the expected element appears.
    private static async Task ClickUntilAsync(IPage page, string clickSelector, string expectSelector)
    {
        var target = page.Locator(expectSelector).First;
        for (var attempt = 0; attempt < 15; attempt++)
        {
            try { await page.Locator(clickSelector).First.ClickAsync(new() { Timeout = 2000 }); }
            catch (PlaywrightException) { await Task.Delay(400); continue; }
            try { await target.WaitForAsync(new() { Timeout = 2500, State = WaitForSelectorState.Visible }); return; }
            catch (TimeoutException) { }
            catch (PlaywrightException) { }
        }
        throw new TimeoutException($"'{expectSelector}' not shown after clicking '{clickSelector}'.");
    }
}
