using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

// The former standalone Strategy Builder page folded into the AI Assistant's "Build Bot" tab.
[Collection(AppCollection.Name)]
public sealed class AssistantBuildBotTests(AppFixture app)
{
    private static readonly LocatorAssertionsToBeVisibleOptions Slow = new() { Timeout = 15000 };

    [Fact]
    public async Task Build_bot_tab_renders_and_is_gated_when_ai_unconfigured()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/assistant");
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");

        // AI is unconfigured in E2E: a one-time "AI not configured" dialog pops on navigate; dismiss it
        // so we can switch tabs, then the gate leaves the action disabled with the add-key banner shown.
        await DismissAiDialogAsync(page);

        var tab = page.Locator("div.mud-tab:has-text('Build Bot')");
        for (var attempt = 0; attempt < 15; attempt++)
        {
            try
            {
                await tab.First.ClickAsync(new() { Timeout = 3000 });
                break;
            }
            catch (PlaywrightException) { /* circuit not interactive yet — retry */ }
        }

        await Assertions.Expect(page.GetByLabel("Describe your strategy")).ToBeVisibleAsync(Slow);
        await Assertions.Expect(page.Locator("button:has-text('Build my bot')")).ToBeVisibleAsync(Slow);
        await Assertions.Expect(page.Locator("button:has-text('Build my bot')"))
            .ToBeDisabledAsync(new() { Timeout = 15000 });
        await Assertions.Expect(page.Locator("[data-testid=ai-not-configured]")).ToBeVisibleAsync(Slow);
    }

    private static async Task DismissAiDialogAsync(IPage page)
    {
        var later = page.Locator("button:has-text('Later')");
        try { await later.First.ClickAsync(new() { Timeout = 8000 }); }
        catch (TimeoutException) { /* dialog not shown yet — fine */ }
        catch (PlaywrightException) { /* ignore */ }
    }
}
