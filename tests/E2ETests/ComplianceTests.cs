using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

[Collection(AppCollection.Name)]
public sealed partial class ComplianceTests(AppFixture app)
{
    private static readonly LocatorAssertionsToBeVisibleOptions Slow = new() { Timeout = 15000 };
    private static readonly LocatorAssertionsToContainTextOptions SlowText = new() { Timeout = 15000 };

    [GeneratedRegex("[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}")]
    private static partial Regex GuidRegex();

    [Fact]
    public async Task Legal_page_renders_and_gdpr_export_returns_the_users_data()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/settings/legal");
        await page.WaitForAppReadyAsync();

        await Assertions.Expect(page.GetByText("Your data (GDPR)")).ToBeVisibleAsync(Slow);

        await page.ClickAsync("[data-testid=export-btn]");
        await Assertions.Expect(page.Locator("[data-testid=export]")).ToContainTextAsync(AppFixture.OwnerEmail, SlowText);

        // D-11: the exported JSON must not leak the internal user UUID.
        var exportText = await page.Locator("[data-testid=export]").InnerTextAsync();
        GuidRegex().IsMatch(exportText).Should().BeFalse("the GDPR export must not expose the internal user UUID");
    }

    [Fact]
    public async Task Erase_my_account_asks_for_confirmation_first()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/settings/legal");
        await page.WaitForAppReadyAsync();

        await Assertions.Expect(page.GetByText("Your data (GDPR)")).ToBeVisibleAsync(Slow);

        // D-05: erase opens a confirm dialog before firing the destructive call.
        await page.ClickAsync("button:has-text('Erase my account')");
        await Assertions.Expect(page.Locator("[data-testid=confirm-accept]")).ToBeVisibleAsync(Slow);
        // Cancel — do NOT erase the owner account (it would break the shared fixture).
        await page.ClickAsync("[data-testid=confirm-cancel]");
    }
}
