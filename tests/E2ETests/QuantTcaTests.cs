using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

[Collection(AppCollection.Name)]
public sealed class QuantTcaTests(AppFixture app)
{
    private static readonly LocatorAssertionsToBeVisibleOptions Slow = new() { Timeout = 15000 };

    [Fact]
    public async Task Analyze_reports_slippage()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/quant/tca");
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");

        await page.GetByLabel("Fills (price, quantity per line)").FillAsync("1.1010, 100\n1.1020, 100");
        await page.ClickAsync("[data-testid=tca-analyze]");

        await Assertions.Expect(page.Locator("[data-testid=tca-result]")).ToBeVisibleAsync(Slow);
        await Assertions.Expect(page.Locator("[data-testid=tca-result]")).ToContainTextAsync("bps");
    }
}
