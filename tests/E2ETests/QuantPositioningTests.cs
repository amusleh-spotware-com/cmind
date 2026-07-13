using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

[Collection(AppCollection.Name)]
public sealed class QuantPositioningTests(AppFixture app)
{
    private static readonly LocatorAssertionsToBeVisibleOptions Slow = new() { Timeout = 15000 };

    [Fact]
    public async Task Read_shows_a_contrarian_bias()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/quant/positioning");
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");

        // Default 50% long → balanced → Neutral. Proves the UI → endpoint → domain path is wired.
        var bias = page.Locator("[data-testid=positioning-bias]");
        await page.ClickUntilVisibleAsync("[data-testid=positioning-read]", bias);

        await Assertions.Expect(bias).ToBeVisibleAsync(Slow);
        await Assertions.Expect(bias).ToContainTextAsync("Neutral");
    }
}
