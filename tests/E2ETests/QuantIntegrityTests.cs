using System.Globalization;
using System.Linq;
using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

[Collection(AppCollection.Name)]
public sealed class QuantIntegrityTests(AppFixture app)
{
    private static readonly LocatorAssertionsToBeVisibleOptions Slow = new() { Timeout = 30000 };

    private static async Task OpenAsync(IPage page)
    {
        await page.GotoAsync("/quant/integrity");
        await page.WaitForAppReadyAsync();
    }

    // Fills the structured numeric series controls (adding rows beyond the two defaults) — no free-text entry.
    private static async Task FillSeriesAsync(IPage page, double[] values)
    {
        for (var i = 2; i < values.Length; i++)
            await page.ClickAsync("[data-testid=series-add]");
        for (var i = 0; i < values.Length; i++)
            await page.FillAsync($"[data-testid=series-value-{i}]",
                values[i].ToString("0.####", CultureInfo.InvariantCulture));
    }

    // Grows a trial row to `count` numeric fields and fills them (each trial starts with four fields).
    private static async Task FillTrialAsync(IPage page, int trial, double[] values)
    {
        for (var i = 4; i < values.Length; i++)
            await page.ClickAsync($"[data-testid=trial-{trial}-add]");
        for (var i = 0; i < values.Length; i++)
            await page.FillAsync($"[data-testid=trial-{trial}-value-{i}]",
                values[i].ToString("0.####", CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task Analyze_scores_a_strong_edge_as_robust()
    {
        // Eight low-variance ~+2% returns → very high Sharpe, t-stat ≫ 3 → deterministic "Robust".
        double[] series = [0.020, 0.019, 0.021, 0.020, 0.019, 0.021, 0.020, 0.021];

        var page = await app.NewAuthedPageAsync();
        await OpenAsync(page);

        var verdict = page.Locator("[data-testid=integrity-verdict]");
        await page.RunUntilVisibleAsync(async () =>
        {
            await FillSeriesAsync(page, series);
            await page.ClickAsync("[data-testid=integrity-analyze]");
        }, verdict);

        await Assertions.Expect(verdict).ToBeVisibleAsync(Slow);
        await Assertions.Expect(verdict).ToContainTextAsync("Robust");
    }

    [Fact]
    public async Task Assess_overfitting_flags_mirror_trials()
    {
        // Two mirror-image 16-point trials → high Probability of Backtest Overfitting → "Overfit".
        double[] Row(bool inverted) => [.. Enumerable.Range(0, 16).Select(i =>
        {
            var baseline = (i < 8) ^ inverted ? 0.01 : -0.01;
            var jitter = i % 2 == 0 ? 0.002 : -0.002;
            return baseline + jitter;
        })];

        var page = await app.NewAuthedPageAsync();
        await OpenAsync(page);

        await FillTrialAsync(page, 0, Row(false));
        await FillTrialAsync(page, 1, Row(true));

        var verdict = page.Locator("[data-testid=integrity-verdict]");
        await page.RunUntilVisibleAsync(async () => await page.ClickAsync("[data-testid=integrity-pbo]"), verdict);

        await Assertions.Expect(verdict).ToBeVisibleAsync(Slow);
        await Assertions.Expect(verdict).ToContainTextAsync("Overfit");
    }

    [Fact]
    public async Task Equity_curve_mode_produces_a_verdict()
    {
        // A steadily rising equity curve entered via the numeric controls → a deterministic verdict.
        double[] equity = [1000.0, 1006.0, 1005.0, 1012.0, 1011.0, 1018.0, 1017.0, 1024.0];

        var page = await app.NewAuthedPageAsync();
        await OpenAsync(page);

        await page.GetByText("Equity / balance curve").ClickAsync();
        var verdict = page.Locator("[data-testid=integrity-verdict]");
        await page.RunUntilVisibleAsync(async () =>
        {
            await FillSeriesAsync(page, equity);
            await page.ClickAsync("[data-testid=integrity-analyze]");
        }, verdict);

        await Assertions.Expect(verdict).ToBeVisibleAsync(Slow);
    }
}
