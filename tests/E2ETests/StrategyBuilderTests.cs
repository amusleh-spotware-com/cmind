using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

[Collection(AppCollection.Name)]
public sealed class StrategyBuilderTests(AppFixture app)
{
    private static readonly LocatorAssertionsToBeVisibleOptions Slow = new() { Timeout = 15000 };

    [Fact]
    public async Task Strategy_builder_page_renders()
    {
        var page = await OpenAsync();
        await Assertions.Expect(page.Locator("button:has-text('Build my bot')")).ToBeVisibleAsync(Slow);
        await Assertions.Expect(page.GetByLabel("Describe your strategy")).ToBeVisibleAsync(Slow);
    }

    // No API key in E2E env -> the whole path (button -> POST /api/ai/build-strategy -> AiFeatureService
    // -> disabled IAiClient) must surface the graceful "not configured" alert.
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
        await page.GotoAsync("/strategy-builder");
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");
        return page;
    }
}
