using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

// The Open API page's old "Invite link" (which revealed the URL in a field) is now "Copy Invite Link",
// which copies to the clipboard. Configures an app, clicks the button, and verifies the URL lands on the
// clipboard.
[Collection(AppCollection.Name)]
public sealed class OpenApiInviteTests(AppFixture app)
{
    [Fact]
    public async Task Copy_invite_link_copies_url_to_clipboard()
    {
        var page = await app.NewAuthedPageAsync();
        var api = page.APIRequest;

        var configure = await api.PutAsync("/api/openapi/application",
            new() { DataObject = new { Name = "E2E OpenAPI App", ClientId = "12345_e2e", ClientSecret = "secret_e2e_abcdef" } });
        configure.Ok.Should().BeTrue($"configure app failed: {configure.Status} {await configure.TextAsync()}");
        try
        {
            await page.Context.GrantPermissionsAsync(
                ["clipboard-read", "clipboard-write"], new() { Origin = app.BaseUrl });
            await page.GotoAsync("/settings/openapi", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

            var copyBtn = page.GetByRole(AriaRole.Button, new() { Name = "Copy Invite Link" });
            await copyBtn.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
            await copyBtn.ClickAsync();

            await page.GetByText("Invite link copied to clipboard")
                .WaitForAsync(new() { Timeout = 8000 });

            var clip = await page.EvaluateAsync<string>("() => navigator.clipboard.readText()");
            clip.Should().NotBeNullOrWhiteSpace("the invite URL should be on the clipboard");
        }
        finally
        {
            await api.DeleteAsync("/api/openapi/application");
        }
    }
}
