using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

// The former standalone Strategy Builder page folded into the AI Assistant's "Build Bot" tab.
[Collection(AppCollection.Name)]
public sealed class AssistantBuildBotTests(AppFixture app)
{
    private static readonly LocatorAssertionsToBeVisibleOptions Slow = new() { Timeout = 15000 };

    [Fact]
    public async Task Build_bot_tab_renders()
    {
        var page = await OpenAsync();
        await Assertions.Expect(page.Locator("button:has-text('Build my bot')")).ToBeVisibleAsync(Slow);
        await Assertions.Expect(page.GetByLabel("Describe your strategy")).ToBeVisibleAsync(Slow);
    }

    // No API key in E2E env -> the AI notice reports "turned off" and the build path surfaces a
    // graceful "not configured" result rather than erroring.
    [Fact]
    public async Task Build_button_drives_endpoint_and_surfaces_disabled_result()
    {
        var page = await OpenAsync();
        await page.GetByLabel("Describe your strategy").FillAsync("RSI mean reversion on EURUSD");

        var feedback = page.GetByText(new Regex("not configured", RegexOptions.IgnoreCase));
        var button = page.Locator("button:has-text('Build my bot')");
        for (var attempt = 0; attempt < 15; attempt++)
        {
            await button.ClickAsync();
            try
            {
                await feedback.First.WaitForAsync(new() { Timeout = 3000, State = WaitForSelectorState.Visible });
                break;
            }
            catch (TimeoutException) { /* circuit not interactive yet — retry */ }
            catch (PlaywrightException) { /* stale after reconnect — retry */ }
        }
        await Assertions.Expect(feedback.First).ToBeVisibleAsync(Slow);
    }

    private async Task<IPage> OpenAsync()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/assistant");
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");

        // AI is unconfigured in E2E, so a one-time "AI not configured" dialog pops on navigate; dismiss it.
        var later = page.Locator("button:has-text('Later')");
        try { await later.First.ClickAsync(new() { Timeout = 8000 }); }
        catch (TimeoutException) { /* dialog not shown (already dismissed / interactive late) */ }
        catch (PlaywrightException) { /* ignore */ }

        // The Build Bot tab is first; ensure it is selected.
        var tab = page.Locator("div.mud-tab:has-text('Build Bot')");
        try { await tab.First.ClickAsync(new() { Timeout = 8000 }); }
        catch (PlaywrightException) { /* already active */ }
        return page;
    }
}
