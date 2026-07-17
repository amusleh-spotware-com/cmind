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
        await page.WaitForAppReadyAsync();

        // "AI suggest" opens the recommendation dialog; the recommendation renders inside it after the
        // "Suggest" action. AI is not configured in the test environment, so the recommender degrades to a
        // graceful message rendered in the dialog's recommendation alert — proving the UI -> endpoint -> AI
        // path is wired.
        await page.GetByTestId("ai-suggest").ClickAsync();
        await Assertions.Expect(page.GetByTestId("ai-risk-profile")).ToBeVisibleAsync(Slow);

        var recommendation = page.Locator("[data-testid=ai-recommendation]");
        await page.ClickUntilVisibleAsync("[data-testid=ai-suggest-confirm]", recommendation);

        await Assertions.Expect(recommendation).ToBeVisibleAsync(Slow);
        await Assertions.Expect(recommendation).ToContainTextAsync("not configured");
    }
}
