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
}
