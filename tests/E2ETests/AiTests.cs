using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

[Collection(AppCollection.Name)]
public sealed class AiTests(AppFixture app)
{
    private static readonly LocatorAssertionsToBeVisibleOptions Slow = new() { Timeout = 15000 };

    [Fact]
    public async Task Assistant_page_renders_all_tabs_and_controls()
    {
        var page = await OpenAssistantAsync();

        await Assertions.Expect(page.Locator("button:has-text('Generate cBot')")).ToBeVisibleAsync(Slow);
        await Assertions.Expect(page.GetByLabel("Strategy description")).ToBeVisibleAsync(Slow);
        await Assertions.Expect(page.GetByText("Review")).ToBeVisibleAsync(Slow);
        await Assertions.Expect(page.GetByText("Market Sentiment")).ToBeVisibleAsync(Slow);
    }

    [Fact]
    public async Task Assistant_generate_button_drives_ai_endpoint_end_to_end()
    {
        // No Anthropic API key is configured in the E2E environment, so a fully-wired
        // click must surface the graceful "not configured" result via the snackbar.
        // This proves: button -> HttpClient POST /api/ai/generate -> endpoint ->
        // AiFeatureService -> IAiClient (disabled) -> UI feedback.
        var page = await OpenAssistantAsync();

        var feedback = page.GetByText(new Regex("not configured", RegexOptions.IgnoreCase));
        var button = page.Locator("button:has-text('Generate cBot')");

        for (var attempt = 0; attempt < 15; attempt++)
        {
            await button.ClickAsync();
            try
            {
                await feedback.First.WaitForAsync(new() { Timeout = 2000, State = WaitForSelectorState.Visible });
                break;
            }
            catch (TimeoutException) { /* circuit not interactive yet — retry */ }
            catch (PlaywrightException) { /* stale locator after circuit reconnect — retry */ }
        }

        await Assertions.Expect(feedback.First).ToBeVisibleAsync(Slow);
    }

    private async Task<IPage> OpenAssistantAsync()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/assistant");
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");
        return page;
    }
}
