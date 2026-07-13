using System.Globalization;
using System.Linq;
using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

[Collection(AppCollection.Name)]
public sealed class QuantIntegrityTests(AppFixture app)
{
    private static readonly LocatorAssertionsToBeVisibleOptions Slow = new() { Timeout = 30000 };

    [Fact]
    public async Task Analyze_scores_a_strong_edge_as_robust()
    {
        // 40 alternating returns: mean 0.005, std 0.001 → Sharpe ≈ 5, t-stat ≫ 3 → deterministic "Robust".
        var series = string.Join(", ", Enumerable.Range(0, 40)
            .Select(i => (0.005 + (i % 2 == 0 ? 0.001 : -0.001)).ToString("0.000", CultureInfo.InvariantCulture)));

        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/quant/integrity");
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");

        await page.GetByLabel("Returns or equity curve").FillAsync(series);
        var verdict = page.Locator("[data-testid=integrity-verdict]");
        await page.ClickUntilVisibleAsync("[data-testid=integrity-analyze]", verdict);

        await Assertions.Expect(verdict).ToBeVisibleAsync(Slow);
        await Assertions.Expect(verdict).ToContainTextAsync("Robust");
    }

    [Fact]
    public async Task Assess_overfitting_flags_mirror_trials()
    {
        // Two mirror-image trials → high Probability of Backtest Overfitting → "Overfit".
        string Row(bool inverted) => string.Join(", ", Enumerable.Range(0, 16).Select(i =>
        {
            var baseline = (i < 8) ^ inverted ? 0.01 : -0.01;
            var jitter = i % 2 == 0 ? 0.002 : -0.002;
            return (baseline + jitter).ToString("0.000", CultureInfo.InvariantCulture);
        }));
        var grid = $"{Row(false)}\n{Row(true)}";

        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/quant/integrity");
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");

        await page.GetByLabel("Trial grid (one return series per line)").FillAsync(grid);
        var verdict = page.Locator("[data-testid=integrity-verdict]");
        await page.ClickUntilVisibleAsync("[data-testid=integrity-pbo]", verdict);

        await Assertions.Expect(verdict).ToBeVisibleAsync(Slow);
        await Assertions.Expect(verdict).ToContainTextAsync("Overfit");
    }

    [Fact]
    public async Task Equity_curve_mode_produces_a_verdict()
    {
        // A steadily rising equity curve → derived returns yield a deterministic verdict.
        var equity = string.Join(", ", Enumerable.Range(0, 40)
            .Select(i => (1000.0 + (i * 6) + (i % 2 == 0 ? 1 : -1)).ToString("0.0", CultureInfo.InvariantCulture)));

        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/quant/integrity");
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");

        await page.GetByTestId("integrity-series").FillAsync(equity);
        await page.GetByText("Equity / balance curve").ClickAsync();
        var verdict = page.Locator("[data-testid=integrity-verdict]");
        await page.ClickUntilVisibleAsync("[data-testid=integrity-analyze]", verdict);

        await Assertions.Expect(verdict).ToBeVisibleAsync(Slow);
    }
}
