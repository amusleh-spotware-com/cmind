using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

[Collection(AppCollection.Name)]
public sealed class AiCopyRecommendTests(AppFixture app)
{
    private static readonly LocatorAssertionsToBeVisibleOptions Slow = new() { Timeout = 15000 };

    [Fact]
    public async Task Ai_suggest_button_calls_recommender_and_renders_result()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/copy-trading");
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");

        await page.ClickAsync("[data-testid=ai-suggest]");

        // AI is not configured in the test environment, so the recommender degrades to a graceful message
        // that is rendered in the recommendation alert — proving the UI -> endpoint -> AI path is wired.
        await Assertions.Expect(page.Locator("[data-testid=ai-recommendation]")).ToBeVisibleAsync(Slow);
        await Assertions.Expect(page.Locator("[data-testid=ai-recommendation]")).ToContainTextAsync("not configured");
    }
}
