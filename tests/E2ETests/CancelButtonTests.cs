using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

// The Cancel button in create/edit dialogs must dismiss the dialog on both desktop and mobile. Regression
// guard after the mobile full-screen-dialog CSS.
[Collection(AppCollection.Name)]
public sealed class CancelButtonTests(AppFixture app)
{
    [Fact]
    public async Task Cancel_closes_dialog_on_desktop()
    {
        var page = await app.NewAuthedPageAsync();
        await Assert_cancel_closes(page);
    }

    [Fact]
    public async Task Cancel_closes_dialog_on_mobile()
    {
        var page = await app.NewAuthedMobilePageAsync();
        await Assert_cancel_closes(page);
    }

    private static async Task Assert_cancel_closes(IPage page)
    {
        await page.GotoAsync("/nodes", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForAppReadyAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "New Node" }).ClickAsync();
        var dialog = page.Locator(".mud-dialog").Last;
        await dialog.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });

        await dialog.GetByRole(AriaRole.Button, new() { Name = "Cancel" }).ClickAsync();
        await dialog.WaitForAsync(new() { State = WaitForSelectorState.Hidden, Timeout = 10000 });

        (await page.Locator(".mud-dialog").IsVisibleAsync()).Should().BeFalse("Cancel must dismiss the dialog");
    }
}
