using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

[Collection(AppCollection.Name)]
public sealed class QuantExecutionTests(AppFixture app)
{
    private static readonly LocatorAssertionsToBeVisibleOptions Slow = new() { Timeout = 15000 };

    [Fact]
    public async Task Build_renders_a_schedule()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/quant/execution");
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");

        await page.ClickAsync("[data-testid=execution-build]");

        await Assertions.Expect(page.Locator("[data-testid=execution-result]")).ToBeVisibleAsync(Slow);
    }

    [Fact]
    public async Task Zero_quantity_is_prevented_by_the_minimum_and_the_build_guard()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/quant/execution");
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");

        // The Min="0.0001" numeric field clamps a literal 0 away — the user can never submit total 0 and
        // get the nonsensical all-zero schedule the bug produced. The field never reads back as "0".
        var total = page.GetByLabel("Total quantity (lots)");
        await total.FillAsync("0");
        await page.ClickAsync("[data-testid=execution-build]");

        await Assertions.Expect(total).Not.ToHaveValueAsync("0");
    }

    [Fact]
    public async Task Compute_then_navigate_away_does_not_crash_the_circuit()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/quant/execution");
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");

        await page.ClickAsync("[data-testid=execution-build]");
        await page.GotoAsync("/"); // navigate away immediately (disposes the component mid-request)

        await Assertions.Expect(page.Locator(".blazor-error-ui")).Not.ToBeVisibleAsync();
    }
}
