using System.Globalization;
using System.Linq;
using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

[Collection(AppCollection.Name)]
public sealed class QuantSizingTests(AppFixture app)
{
    private static readonly LocatorAssertionsToBeVisibleOptions Slow = new() { Timeout = 15000 };

    [Fact]
    public async Task Recommend_size_returns_an_exposure()
    {
        // std 0.02 → the 10% vol target binds to a positive, sub-cap exposure.
        var series = string.Join(", ", Enumerable.Range(0, 60)
            .Select(i => (0.002 + (i % 2 == 0 ? 0.02 : -0.02)).ToString("0.000", CultureInfo.InvariantCulture)));

        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/quant/sizing");
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");

        await page.GetByLabel("Returns or equity curve").FillAsync(series);
        await page.ClickAsync("[data-testid=sizing-calculate]");

        await Assertions.Expect(page.Locator("[data-testid=sizing-recommendation]")).ToBeVisibleAsync(Slow);
        await Assertions.Expect(page.Locator("[data-testid=sizing-recommendation]")).ToContainTextAsync("×");
    }
}
