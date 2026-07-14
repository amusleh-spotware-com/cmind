using System.Globalization;
using System.Linq;
using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

[Collection(AppCollection.Name)]
public sealed class QuantRegimesTests(AppFixture app)
{
    private static readonly LocatorAssertionsToBeVisibleOptions Slow = new() { Timeout = 30000 };

    [Fact]
    public async Task Analyze_renders_a_regime_breakdown()
    {
        // Calm first half, turbulent second half.
        var series = string.Join(", ", Enumerable.Range(0, 60).Select(i =>
        {
            var jitter = i < 30 ? 0.0005 : 0.02;
            return (i % 2 == 0 ? jitter : -jitter).ToString("0.0000", CultureInfo.InvariantCulture);
        }));

        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/quant/regimes");
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");

        var result = page.Locator("[data-testid=regimes-result]");
        await page.RunUntilVisibleAsync(async () =>
        {
            await page.GetByLabel("Returns or equity curve").FillAsync(series);
            await page.ClickAsync("[data-testid=regimes-analyze]");
        }, result);

        await Assertions.Expect(result).ToBeVisibleAsync(Slow);
        await Assertions.Expect(result).ToContainTextAsync("Hurst");
    }

    [Fact]
    public async Task Equity_curve_mode_renders_a_regime_breakdown()
    {
        // Calm-then-turbulent equity curve → derived returns produce the regime table.
        var equity = string.Join(", ", Enumerable.Range(0, 60).Select(i =>
        {
            var step = i < 30 ? 2.0 : (i % 2 == 0 ? 30.0 : -30.0);
            return (1000.0 + (i * 2) + step).ToString("0.0", CultureInfo.InvariantCulture);
        }));

        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/quant/regimes");
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");

        var result = page.Locator("[data-testid=regimes-result]");
        await page.RunUntilVisibleAsync(async () =>
        {
            await page.GetByLabel("Returns or equity curve").FillAsync(equity);
            await page.GetByText("Equity / balance curve").ClickAsync();
            await page.ClickAsync("[data-testid=regimes-analyze]");
        }, result);

        await Assertions.Expect(result).ToBeVisibleAsync(Slow);
        await Assertions.Expect(result).ToContainTextAsync("Hurst");
    }
}
