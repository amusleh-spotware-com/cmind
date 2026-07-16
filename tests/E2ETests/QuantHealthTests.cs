using System.Globalization;
using System.Linq;
using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

[Collection(AppCollection.Name)]
public sealed class QuantHealthTests(AppFixture app)
{
    private static readonly LocatorAssertionsToBeVisibleOptions Slow = new() { Timeout = 30000 };

    // Fills the structured numeric series controls (adding rows beyond the two defaults) — no free-text entry.
    private static async Task FillSeriesAsync(IPage page, double[] values)
    {
        for (var i = 2; i < values.Length; i++)
            await page.ClickAsync("[data-testid=series-add]");
        for (var i = 0; i < values.Length; i++)
            await page.FillAsync($"[data-testid=series-value-{i}]",
                values[i].ToString("0.####", CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task Assess_flags_a_decayed_edge()
    {
        // Strong first half (Sharpe ~5) collapsing to flat noise (Sharpe ~0) → "Decayed".
        double[] series = [.. Enumerable.Range(0, 40).Select(i =>
        {
            var (m, j) = i < 20 ? (0.01, 0.002) : (0.0, 0.02);
            return m + (i % 2 == 0 ? j : -j);
        })];

        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/quant/health");
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");

        var verdict = page.Locator("[data-testid=health-verdict]");
        await page.RunUntilVisibleAsync(async () =>
        {
            await FillSeriesAsync(page, series);
            await page.ClickAsync("[data-testid=health-assess]");
        }, verdict);

        await Assertions.Expect(verdict).ToBeVisibleAsync(Slow);
        await Assertions.Expect(verdict).ToContainTextAsync("Decayed");
    }

    [Fact]
    public async Task Equity_curve_mode_produces_a_verdict()
    {
        // Rising-then-flat equity curve → the derived returns still yield a health verdict.
        double[] equity = [.. Enumerable.Range(0, 40).Select(i =>
            i < 20 ? 1000.0 + (i * 10) : 1200.0 + (i % 2 == 0 ? 4 : -4))];

        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/quant/health");
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");

        var verdict = page.Locator("[data-testid=health-verdict]");
        await page.RunUntilVisibleAsync(async () =>
        {
            await page.GetByText("Equity / balance curve").ClickAsync();
            await FillSeriesAsync(page, equity);
            await page.ClickAsync("[data-testid=health-assess]");
        }, verdict);

        await Assertions.Expect(verdict).ToBeVisibleAsync(Slow);
    }
}
