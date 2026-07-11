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
        await Assertions.Expect(page.Locator("button:has-text('Generate & build project')")).ToBeVisibleAsync(Slow);
        await Assertions.Expect(page.GetByLabel("Strategy description")).ToBeVisibleAsync(Slow);
        await Assertions.Expect(page.GetByText("Review")).ToBeVisibleAsync(Slow);
        await Assertions.Expect(page.GetByText("Market Sentiment")).ToBeVisibleAsync(Slow);
        await Assertions.Expect(page.GetByText("Optimize")).ToBeVisibleAsync(Slow);
        await Assertions.Expect(page.GetByText("Tune Advisor")).ToBeVisibleAsync(Slow);
        await Assertions.Expect(page.GetByText("Portfolio Digest")).ToBeVisibleAsync(Slow);
        await Assertions.Expect(page.GetByText("Debate")).ToBeVisibleAsync(Slow);
        await Assertions.Expect(page.GetByText("Exposure Check")).ToBeVisibleAsync(Slow);
        await Assertions.Expect(page.GetByText("Build Bot")).ToBeVisibleAsync(Slow);
    }

    // No Anthropic API key is configured in the E2E environment, so AI features are gated:
    // the "not configured" notice is shown and the action buttons are dimmed/disabled. This proves
    // the whole gate: GET /api/ai/status -> AiFeatureNotice -> disabled actions + add-key banner.
    [Fact]
    public async Task Assistant_dims_actions_and_prompts_for_key_when_unconfigured()
    {
        var page = await OpenAssistantAsync();

        await Assertions.Expect(page.Locator("[data-testid=ai-not-configured]")).ToBeVisibleAsync(Slow);
        await Assertions.Expect(page.Locator("[data-testid=ai-add-key]")).ToBeVisibleAsync(Slow);
        await Assertions.Expect(page.Locator("button:has-text('Generate cBot')"))
            .ToBeDisabledAsync(new() { Timeout = 15000 });
    }

    private async Task<IPage> OpenAssistantAsync()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/assistant");
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");
        return page;
    }
}
