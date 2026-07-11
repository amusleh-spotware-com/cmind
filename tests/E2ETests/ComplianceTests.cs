using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

[Collection(AppCollection.Name)]
public sealed class ComplianceTests(AppFixture app)
{
    private static readonly LocatorAssertionsToBeVisibleOptions Slow = new() { Timeout = 15000 };
    private static readonly LocatorAssertionsToContainTextOptions SlowText = new() { Timeout = 15000 };

    [Fact]
    public async Task Legal_page_renders_and_gdpr_export_returns_the_users_data()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/compliance");
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");

        await Assertions.Expect(page.GetByText("Your data (GDPR)")).ToBeVisibleAsync(Slow);

        await page.ClickAsync("[data-testid=export-btn]");
        await Assertions.Expect(page.Locator("[data-testid=export]")).ToContainTextAsync(AppFixture.OwnerEmail, SlowText);
    }
}
