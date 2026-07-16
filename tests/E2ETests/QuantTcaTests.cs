using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

[Collection(AppCollection.Name)]
public sealed class QuantTcaTests(AppFixture app)
{
    private static readonly LocatorAssertionsToBeVisibleOptions Slow = new() { Timeout = 30000 };

    // Fills the first two structured fill rows (price + quantity) — no free-text entry.
    private static async Task FillTwoFillsAsync(IPage page)
    {
        await page.FillAsync("[data-testid=fill-price-0]", "1.1010");
        await page.FillAsync("[data-testid=fill-qty-0]", "100");
        await page.FillAsync("[data-testid=fill-price-1]", "1.1020");
        await page.FillAsync("[data-testid=fill-qty-1]", "100");
    }

    [Fact]
    public async Task Analyze_reports_slippage()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/quant/tca");
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");

        var result = page.Locator("[data-testid=tca-result]");
        await page.RunUntilVisibleAsync(async () =>
        {
            await FillTwoFillsAsync(page);
            await page.ClickAsync("[data-testid=tca-analyze]");
        }, result);

        await Assertions.Expect(result).ToBeVisibleAsync(Slow);
        await Assertions.Expect(result).ToContainTextAsync("bps");
    }

    [Fact]
    public async Task Analyze_is_disabled_until_a_valid_fill_is_entered()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/quant/tca");
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");

        // The two default fill rows are empty → no ready fills → the action is disabled.
        var analyze = page.Locator("[data-testid=tca-analyze]");
        await Assertions.Expect(analyze).ToBeDisabledAsync(new() { Timeout = 30000 });

        await page.RunUntilEnabledAsync(async () =>
        {
            await page.FillAsync("[data-testid=fill-price-0]", "1.1010");
            await page.FillAsync("[data-testid=fill-qty-0]", "100");
        }, analyze);
        await Assertions.Expect(analyze).ToBeEnabledAsync(new() { Timeout = 30000 });
    }

    [Fact]
    public async Task Non_positive_quantity_keeps_the_action_disabled()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/quant/tca");
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");

        // A price with a non-positive quantity is not a ready fill; the Min=0 field also clamps a negative.
        await page.FillAsync("[data-testid=fill-price-0]", "1.1005");
        await page.FillAsync("[data-testid=fill-qty-0]", "-100");

        await Assertions.Expect(page.Locator("[data-testid=tca-analyze]")).ToBeDisabledAsync(new() { Timeout = 30000 });
        await Assertions.Expect(page.Locator("[data-testid=tca-result]")).Not.ToBeVisibleAsync();
    }

    [Fact]
    public async Task Compute_then_navigate_away_does_not_crash_the_circuit()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/quant/tca");
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");

        await FillTwoFillsAsync(page);
        await page.ClickAsync("[data-testid=tca-analyze]");
        await page.GotoAsync("/");

        await Assertions.Expect(page.Locator(".blazor-error-ui")).Not.ToBeVisibleAsync();
    }
}
