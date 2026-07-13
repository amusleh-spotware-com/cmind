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

    [Fact]
    public async Task Negative_fill_quantity_is_rejected_with_a_validation_message()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/quant/tca");
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");

        await page.GetByLabel("Fills (price, quantity per line)").FillAsync("1.1005, -100");
        await page.ClickAsync("[data-testid=tca-analyze]");

        // A warning snackbar appears and no result is computed (negative quantity rejected client-side).
        await Assertions.Expect(page.Locator(".mud-snackbar.mud-alert-filled-warning")).ToBeVisibleAsync(Slow);
        await Assertions.Expect(page.Locator("[data-testid=tca-result]")).Not.ToBeVisibleAsync();
    }

    [Fact]
    public async Task Compute_then_navigate_away_does_not_crash_the_circuit()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/quant/tca");
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");

        await page.GetByLabel("Fills (price, quantity per line)").FillAsync("1.1010, 100\n1.1020, 100");
        await page.ClickAsync("[data-testid=tca-analyze]");
        await page.GotoAsync("/");

        await Assertions.Expect(page.Locator(".blazor-error-ui")).Not.ToBeVisibleAsync();
    }
}
