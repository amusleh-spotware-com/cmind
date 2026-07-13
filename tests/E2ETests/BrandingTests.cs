using System.Net.Http.Json;
using FluentAssertions;
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

    // H-03: an owner changing the brand product name in Settings → Deployment must reflect in the app-bar
    // of an already-open circuit on the next in-session navigation — no full page reload. The singleton
    // BrandingThemeProvider raises Changed on IOptionsMonitor.OnChange; MainLayout subscribes and re-renders.
    [Fact]
    public async Task Product_name_reflects_owner_override_live_in_open_circuit()
    {
        var page = await app.NewAuthedPageAsync();
        try
        {
            await page.GotoAsync("/settings/deployment", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
            await page.WaitForFunctionAsync("() => window.Blazor !== undefined");
            await Assertions.Expect(page.Locator("[data-testid=app-product-name]")).ToHaveTextAsync("cMind", new() { Timeout = 15000 });

            // Apply the override through the same authenticated owner context (no reload of the circuit).
            var put = await page.APIRequest.PutAsync(
                $"{app.BaseUrl}/api/whitelabel/branding.productName",
                new APIRequestContextOptions { DataObject = new { Value = "AcmeTrade" } });
            put.Ok.Should().BeTrue($"setting the product-name override should succeed (status {put.Status})");

            // In-session client-side navigation keeps the circuit alive; the app-bar must show the new name.
            await page.Locator("a[href='/accounts']").First.ClickAsync();
            await Assertions.Expect(page.Locator("[data-testid=app-product-name]"))
                .ToHaveTextAsync("AcmeTrade", new() { Timeout = 15000 });
        }
        finally
        {
            // Reset the global override so sibling tests (which assert "cMind") are not polluted.
            await page.APIRequest.DeleteAsync($"{app.BaseUrl}/api/whitelabel/branding.productName");
        }
    }
}
