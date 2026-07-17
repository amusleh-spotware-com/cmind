using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

// A fresh app with NO AI configured, isolated in its own collection so activating the built-in demo does
// not leak into the shared unconfigured-gate tests. Proves the "Try demo AI" one-click flow: a user with
// no key can enable the built-in demo provider and see every AI feature work live (canned responses).
public sealed class DemoAppFixture : AppFixture;

[CollectionDefinition(Name)]
public sealed class AiDemoCollection : ICollectionFixture<DemoAppFixture>
{
    public const string Name = "ai-demo";
}

[Collection(AiDemoCollection.Name)]
public sealed class AiDemoLiveTests(DemoAppFixture app)
{
    private const string DemoMarker = "cMind Demo AI";
    private static readonly LocatorAssertionsToBeVisibleOptions Slow = new() { Timeout = 20000 };

    private async Task<IPage> OpenAsync(string route)
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync(route, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForAppReadyAsync();
        return page;
    }

    [Fact]
    public async Task Try_demo_ai_enables_features_and_renders_canned_output()
    {
        // 1) Enable the built-in demo from Settings → AI (no key, no endpoint).
        var settings = await OpenAsync("/settings/ai");
        await settings.Locator("[data-testid=ai-use-demo]").ClickAsync();
        await Assertions.Expect(settings.Locator("[data-testid=ai-active-chip]").First).ToBeVisibleAsync(Slow);

        // 2) A real AI feature now works live and renders the demo response.
        var review = await OpenAsync("/ai/review");
        var button = review.Locator("button:has-text('Review cBot')");
        await Assertions.Expect(button).ToBeEnabledAsync(new() { Timeout = 20000 });
        await review.GetByLabel("cBot source").FillAsync("public class Bot { }");
        await button.ClickAsync();

        var output = review.Locator("[data-testid=ai-output]");
        await Assertions.Expect(output).ToBeVisibleAsync(Slow);
        (await output.InnerTextAsync()).Should().Contain(DemoMarker);
    }
}
