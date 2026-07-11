using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

// Navigating to settings and changing them: saving an Anthropic API key on /settings/ai flips the page to
// the "Enabled" state (a real settings round-trip through the encrypted AppSetting store). Feature-toggle
// settings are covered by FeatureToggleTests.
[Collection(AppCollection.Name)]
public sealed class SettingsTests(AppFixture app)
{
    private static readonly LocatorAssertionsToBeVisibleOptions Slow = new() { Timeout = 15000 };

    [Fact]
    public async Task Ai_settings_save_key_enables_ai_then_clear_disables()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/settings/ai");
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");

        // Save a key -> the page shows the Enabled chip.
        await page.GetByLabel("Anthropic API key").FillAsync("sk-ant-e2e-dummy-key-0123456789");
        await ClickUntilAsync(page, "[data-testid=ai-key-save]", "[data-testid=ai-enabled-chip]");
        await Assertions.Expect(page.Locator("[data-testid=ai-enabled-chip]")).ToBeVisibleAsync(Slow);

        // Clear it -> back to "Not configured".
        await ClickUntilAsync(page, "[data-testid=ai-key-clear]", "[data-testid=ai-disabled-chip]");
        await Assertions.Expect(page.Locator("[data-testid=ai-disabled-chip]")).ToBeVisibleAsync(Slow);
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
