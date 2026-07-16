using System.Globalization;
using System.Linq;
using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

[Collection(AppCollection.Name)]
public sealed class QuantSizingTests(AppFixture app)
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
    public async Task Recommend_size_returns_an_exposure()
    {
        // std 0.02 → the 10% vol target binds to a positive, sub-cap exposure.
        double[] series = [.. Enumerable.Range(0, 12).Select(i => 0.002 + (i % 2 == 0 ? 0.02 : -0.02))];

        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/quant/sizing");
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");

        var recommendation = page.Locator("[data-testid=sizing-recommendation]");
        await page.RunUntilVisibleAsync(async () =>
        {
            await FillSeriesAsync(page, series);
            await page.ClickAsync("[data-testid=sizing-calculate]");
        }, recommendation);

        await Assertions.Expect(recommendation).ToBeVisibleAsync(Slow);
        await Assertions.Expect(recommendation).ToContainTextAsync("×");
    }

    [Fact]
    public async Task Equity_curve_mode_returns_an_exposure()
    {
        // A rising, wobbling equity curve → the returns derived from it size to a positive exposure.
        double[] equity = [.. Enumerable.Range(0, 12).Select(i => 1000.0 + (i * 5) + (i % 2 == 0 ? 8 : -8))];

        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/quant/sizing");
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");

        var recommendation = page.Locator("[data-testid=sizing-recommendation]");
        await page.RunUntilVisibleAsync(async () =>
        {
            await page.GetByText("Equity / balance curve").ClickAsync();
            await FillSeriesAsync(page, equity);
            await page.ClickAsync("[data-testid=sizing-calculate]");
        }, recommendation);

        await Assertions.Expect(recommendation).ToBeVisibleAsync(Slow);
        await Assertions.Expect(recommendation).ToContainTextAsync("×");
    }

    [Fact]
    public async Task Recommend_button_is_disabled_until_two_values_are_entered()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/quant/sizing");
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");

        // The two default rows are empty, so there are zero valid values and the action is disabled.
        var calculate = page.Locator("[data-testid=sizing-calculate]");
        await Assertions.Expect(calculate).ToBeDisabledAsync(new() { Timeout = 30000 });

        await page.RunUntilEnabledAsync(async () =>
        {
            await page.FillAsync("[data-testid=series-value-0]", "0.01");
            await page.FillAsync("[data-testid=series-value-1]", "0.02");
        }, calculate);
        await Assertions.Expect(calculate).ToBeEnabledAsync(new() { Timeout = 30000 });
    }
}
