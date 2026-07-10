using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

[Collection(AppCollection.Name)]
public sealed class BrandingTests(AppFixture app)
{
    private static readonly LocatorAssertionsToBeVisibleOptions Slow = new() { Timeout = 15000 };

    [Fact]
    public async Task Product_name_and_title_render_from_branding()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/");
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");

        await Assertions.Expect(page.Locator("[data-testid=app-product-name]")).ToBeVisibleAsync(Slow);
        await Assertions.Expect(page.Locator("[data-testid=app-product-name]")).ToHaveTextAsync("cMind");
    }
}
