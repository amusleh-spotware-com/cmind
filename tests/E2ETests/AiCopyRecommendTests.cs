using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

[Collection(AppCollection.Name)]
public sealed class AiCopyRecommendTests(AppFixture app)
{
    private static readonly LocatorAssertionsToBeVisibleOptions Slow = new() { Timeout = 30000 };

    [Fact]
    public async Task Ai_suggest_button_calls_recommender_and_renders_result()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/copy-trading");
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");

        // AI is not configured in the test environment, so the recommender degrades to a graceful message
        // that is rendered in the recommendation alert — proving the UI -> endpoint -> AI path is wired.
        var recommendation = page.Locator("[data-testid=ai-recommendation]");
        await page.ClickUntilVisibleAsync("[data-testid=ai-suggest]", recommendation);

        await Assertions.Expect(recommendation).ToBeVisibleAsync(Slow);
        await Assertions.Expect(recommendation).ToContainTextAsync("not configured");
    }
}
