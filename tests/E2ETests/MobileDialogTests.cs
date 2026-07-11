using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

// Create/edit dialogs must go near-full-screen on a phone so forms are usable. Opens a real dialog on a
// mobile viewport and asserts it spans essentially the full width.
[Collection(AppCollection.Name)]
public sealed class MobileDialogTests(AppFixture app)
{
    [Fact]
    public async Task New_node_dialog_is_full_width_on_mobile()
    {
        var page = await app.NewAuthedMobilePageAsync();
        await page.GotoAsync("/nodes", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        await page.GetByRole(AriaRole.Button, new() { Name = "New Node" }).ClickAsync();

        var dialog = page.Locator(".mud-dialog");
        await dialog.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        // MudBlazor scales the dialog in; wait for the entrance transform to settle before measuring width.
        await page.WaitForTimeoutAsync(600);

        var box = await dialog.BoundingBoxAsync();
        var viewport = await page.EvaluateAsync<int>("() => window.innerWidth");
        box.Should().NotBeNull();
        box!.Width.Should().BeGreaterThanOrEqualTo(viewport * 0.95f, "dialogs should be near-full-width on a phone");
    }
}
