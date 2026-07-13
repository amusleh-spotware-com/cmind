using System.Globalization;
using System.Linq;
using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

[Collection(AppCollection.Name)]
public sealed class QuantHealthTests(AppFixture app)
{
    private static readonly LocatorAssertionsToBeVisibleOptions Slow = new() { Timeout = 15000 };

    [Fact]
    public async Task Assess_flags_a_decayed_edge()
    {
        // Strong first half (Sharpe ~5) collapsing to flat noise (Sharpe ~0) → "Decayed".
        var series = string.Join(", ", Enumerable.Range(0, 40).Select(i =>
        {
            var (m, j) = i < 20 ? (0.01, 0.002) : (0.0, 0.02);
            return (m + (i % 2 == 0 ? j : -j)).ToString("0.000", CultureInfo.InvariantCulture);
        }));

        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/quant/health");
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");

        await page.GetByLabel("Returns or equity curve").FillAsync(series);
        await page.ClickAsync("[data-testid=health-assess]");

        await Assertions.Expect(page.Locator("[data-testid=health-verdict]")).ToBeVisibleAsync(Slow);
        await Assertions.Expect(page.Locator("[data-testid=health-verdict]")).ToContainTextAsync("Decayed");
    }

    [Fact]
    public async Task Equity_curve_mode_produces_a_verdict()
    {
        // Rising-then-flat equity curve → the derived returns still yield a health verdict.
        var equity = string.Join(", ", Enumerable.Range(0, 40).Select(i =>
        {
            var value = i < 20 ? 1000.0 + (i * 10) : 1200.0 + (i % 2 == 0 ? 4 : -4);
            return value.ToString("0.0", CultureInfo.InvariantCulture);
        }));

        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/quant/health");
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");

        await page.GetByLabel("Returns or equity curve").FillAsync(equity);
        await page.GetByText("Equity / balance curve").ClickAsync();
        await page.ClickAsync("[data-testid=health-assess]");

        await Assertions.Expect(page.Locator("[data-testid=health-verdict]")).ToBeVisibleAsync(Slow);
    }
}
