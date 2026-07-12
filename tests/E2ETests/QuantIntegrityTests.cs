using System.Globalization;
using System.Linq;
using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

[Collection(AppCollection.Name)]
public sealed class QuantIntegrityTests(AppFixture app)
{
    private static readonly LocatorAssertionsToBeVisibleOptions Slow = new() { Timeout = 15000 };

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
        await page.ClickAsync("[data-testid=integrity-analyze]");

        await Assertions.Expect(page.Locator("[data-testid=integrity-verdict]")).ToBeVisibleAsync(Slow);
        await Assertions.Expect(page.Locator("[data-testid=integrity-verdict]")).ToContainTextAsync("Robust");
    }
}
