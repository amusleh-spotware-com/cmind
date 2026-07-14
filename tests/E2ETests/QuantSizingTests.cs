using System.Globalization;
using System.Linq;
using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

[Collection(AppCollection.Name)]
public sealed class QuantSizingTests(AppFixture app)
{
    private static readonly LocatorAssertionsToBeVisibleOptions Slow = new() { Timeout = 30000 };

    [Fact]
    public async Task Recommend_size_returns_an_exposure()
    {
        // std 0.02 → the 10% vol target binds to a positive, sub-cap exposure.
        var series = string.Join(", ", Enumerable.Range(0, 60)
            .Select(i => (0.002 + (i % 2 == 0 ? 0.02 : -0.02)).ToString("0.000", CultureInfo.InvariantCulture)));

        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/quant/sizing");
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");

        var recommendation = page.Locator("[data-testid=sizing-recommendation]");
        // Re-fill inside the retried action: a fill dropped before the Blazor circuit is interactive would
        // otherwise leave the field empty forever while only the calculate click is retried.
        await page.RunUntilVisibleAsync(async () =>
        {
            await page.GetByLabel("Returns or equity curve").FillAsync(series);
            await page.ClickAsync("[data-testid=sizing-calculate]");
        }, recommendation);

        await Assertions.Expect(recommendation).ToBeVisibleAsync(Slow);
        await Assertions.Expect(recommendation).ToContainTextAsync("×");
    }

    [Fact]
    public async Task Equity_curve_mode_returns_an_exposure()
    {
        // A rising, wobbling equity curve → the returns derived from it size to a positive exposure.
        var equity = string.Join(", ", Enumerable.Range(0, 60)
            .Select(i => (1000.0 + (i * 5) + (i % 2 == 0 ? 8 : -8)).ToString("0.0", CultureInfo.InvariantCulture)));

        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/quant/sizing");
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");

        var recommendation = page.Locator("[data-testid=sizing-recommendation]");
        // Fill + mode-toggle + calculate all inside the retried action: any one dropped before the circuit
        // is interactive (empty field, still in returns mode) would otherwise never render a recommendation.
        await page.RunUntilVisibleAsync(async () =>
        {
            await page.GetByLabel("Returns or equity curve").FillAsync(equity);
            await page.GetByText("Equity / balance curve").ClickAsync();
            await page.ClickAsync("[data-testid=sizing-calculate]");
        }, recommendation);

        await Assertions.Expect(recommendation).ToBeVisibleAsync(Slow);
        await Assertions.Expect(recommendation).ToContainTextAsync("×");
    }
}
