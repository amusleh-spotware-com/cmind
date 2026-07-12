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

        await page.ClickAsync("[data-testid=positioning-read]");

        // Default 50% long → balanced → Neutral. Proves the UI → endpoint → domain path is wired.
        await Assertions.Expect(page.Locator("[data-testid=positioning-bias]")).ToBeVisibleAsync(Slow);
        await Assertions.Expect(page.Locator("[data-testid=positioning-bias]")).ToContainTextAsync("Neutral");
    }
}
