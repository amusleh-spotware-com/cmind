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

        // 2) A real AI feature now works live and renders the demo response. Review is a list page:
        // start a run from the dialog, then its detail view renders the demo output in the background.
        var review = await OpenAsync("/ai/review");
        await review.Locator("[data-testid=ai-run-new]").ClickAsync();
        await Assertions.Expect(review.Locator("[data-testid=ai-run-source]")).ToBeVisibleAsync(Slow);
        await review.GetByTestId("ai-run-name").FillAsync("e2e");
        await review.GetByTestId("ai-run-source").FillAsync("public class Bot { }");
        var create = review.Locator("[data-testid=ai-run-start-create]");
        await Assertions.Expect(create).ToBeEnabledAsync(new() { Timeout = 20000 });
        await create.ClickAsync();

        var output = review.Locator("[data-testid=ai-run-output]");
        await Assertions.Expect(output).ToBeVisibleAsync(new() { Timeout = 30000 });
        (await output.InnerTextAsync()).Should().Contain(DemoMarker);
    }
}
