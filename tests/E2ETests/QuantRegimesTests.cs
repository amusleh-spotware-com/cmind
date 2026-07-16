using System.Globalization;
using System.Linq;
using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

[Collection(AppCollection.Name)]
public sealed class QuantRegimesTests(AppFixture app)
{
    private static readonly LocatorAssertionsToBeVisibleOptions Slow = new() { Timeout = 30000 };

    // Fills the structured numeric series controls (adding rows beyond the six defaults) — no free-text entry.
    private static async Task FillSeriesAsync(IPage page, double[] values)
    {
        for (var i = 6; i < values.Length; i++)
            await page.ClickAsync("[data-testid=series-add]");
        for (var i = 0; i < values.Length; i++)
            await page.FillAsync($"[data-testid=series-value-{i}]",
                values[i].ToString("0.####", CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task Analyze_renders_a_regime_breakdown()
    {
        // Calm first half, turbulent second half.
        double[] series = [.. Enumerable.Range(0, 30).Select(i =>
        {
            var jitter = i < 15 ? 0.0005 : 0.02;
            return i % 2 == 0 ? jitter : -jitter;
        })];

        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/quant/regimes");
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");

        var result = page.Locator("[data-testid=regimes-result]");
        await page.RunUntilVisibleAsync(async () =>
        {
            await FillSeriesAsync(page, series);
            await page.ClickAsync("[data-testid=regimes-analyze]");
        }, result);

        await Assertions.Expect(result).ToBeVisibleAsync(Slow);
        await Assertions.Expect(result).ToContainTextAsync("Hurst");
    }

    [Fact]
    public async Task Equity_curve_mode_renders_a_regime_breakdown()
    {
        // Calm-then-turbulent equity curve → derived returns produce the regime table.
        double[] equity = [.. Enumerable.Range(0, 30).Select(i =>
        {
            var step = i < 15 ? 2.0 : (i % 2 == 0 ? 30.0 : -30.0);
            return 1000.0 + (i * 2) + step;
        })];

        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/quant/regimes");
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");

        var result = page.Locator("[data-testid=regimes-result]");
        await page.RunUntilVisibleAsync(async () =>
        {
            await page.GetByText("Equity / balance curve").ClickAsync();
            await FillSeriesAsync(page, equity);
            await page.ClickAsync("[data-testid=regimes-analyze]");
        }, result);

        await Assertions.Expect(result).ToBeVisibleAsync(Slow);
        await Assertions.Expect(result).ToContainTextAsync("Hurst");
    }
}
